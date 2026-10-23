using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DesertSettlement.Core.Auction;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;
using DesertSettlement.Core.Progression;

namespace SettlementSim
{
    // 작업 목록 4번(전염병·병원·방역 장비) 측정: dotnet run --project Tools/SettlementSim -- epidemic [맵 수]
    // 같은 맵·다중 봇 건설 목록으로 '재해 없음 / 전염병 1회(유예 직후 시작) / + 방역 장비 / + 병원(첫 광산 옆)'을 돌려
    // 매각 후 순이익(경매 직전 현금 + 중립 V × 1.05 − 2000) 중앙값과 전염병 지속·최대 피해를 비교한다.
    public static partial class Program
    {
        private static int EpidemicTuning(string[] args)
        {
            var config = LoadConfig();
            var proc = LoadProceduralConfig();
            int maps = args.Length > 0 ? int.Parse(args[0]) : 20;
            Header("전염병 측정 — 다중 봇, 시작 자금 2000, 전염병은 유예(48h) 직후 시작하도록 고정");
            string[] variants = { "재해 없음", "전염병", "+방역 장비", "+병원(광산 옆)", "+둘 다" };
            foreach (double d in new[] { 0.5, 1.0 })
            {
                var defs = Enumerable.Range(0, maps)
                    .Select(i => ProceduralMapGenerator.Generate(DifficultyParams.For(d, proc.Difficulty), proc.Generator, 95000 + (int)(d * 1000) + i))
                    .Where(g => g != null).Select(g => g.Map).ToList();
                Line();
                Line($"  ▸ D {d:0.0} ({defs.Count}장) | 순이익 중앙값 | 전염병 지속(h) | 최대 피해");
                foreach (string v in variants)
                {
                    var profits = new List<double>();
                    var durations = new List<double>();
                    var peaks = new List<double>();
                    foreach (MapDefinition def in defs)
                    {
                        (double profit, long duration, double peak) = RunEpidemic(config, def, v != "재해 없음", v.Contains("방역") || v.Contains("둘"), v.Contains("병원") || v.Contains("둘"));
                        profits.Add(profit);
                        if (duration >= 0) durations.Add(duration);
                        peaks.Add(peak);
                    }
                    double Med(List<double> xs) => xs.Count == 0 ? double.NaN : xs.OrderBy(x => x).ElementAt(xs.Count / 2);
                    Line($"    {v,-14} | {Med(profits),8:+0;-0} | {Med(durations),6:0} | {Med(peaks),6:P0}");
                }
            }
            string outDir = Path.Combine(FindRepoRoot(), "Tools", "balance_output");
            File.WriteAllText(Path.Combine(outDir, "epidemic_report.txt"), Report.ToString(), new UTF8Encoding(false));
            return 0;
        }

        private static (double profit, long duration, double peak) RunEpidemic(EconomyConfig config, MapDefinition def, bool epidemic, bool vaccinated, bool hospital)
        {
            List<MapBuildEntry> plan = ReferenceBot.Plan(def, BotStrategy.Multi);
            if (plan == null) return (-2000, -1, 0);
            SettlementRules rules = SettlementRules.FromConfig(config);
            rules.StartingCash = 2000;
            if (vaccinated) rules.ApplyEffects(config.Equipment.Single(e => e.Id == "vaccination").Effects);
            if (hospital) rules.FreeBuilds[BuildingKinds.Hospital] = 1; // 측정용: 병원을 공짜로 바로 세워 효과만 본다
            var s = new Settlement(def.CreateMap(), rules);
            var a = new AuctionManager(s, 1);
            DisasterManager dm = null;
            if (epidemic)
            {
                DisasterSettings src = config.Disasters;
                dm = DisasterManager.Attach(s, new DisasterSettings
                {
                    GraceHours = src.GraceHours, WarningHours = src.WarningHours, ScheduleWindowHours = 1,
                    CountWeights = new List<double> { 0, 1 }, Types = src.Types, Epidemic = src.Epidemic,
                }, 1);
            }
            var withPlan = new MapDefinition { Id = def.Id, Radius = def.Radius, Terrain = def.Terrain, SampleLayout = plan };
            if (withPlan.RequestSampleLayout(s).Count > 0) return (-2000, -1, 0);
            if (hospital)
            {
                // 첫 광산 타일의 이웃 사막 중 비어 있는 곳에 병원.
                MapBuildEntry mine = plan.First(b => b.Kind == BuildingKinds.Mine);
                var at = new HexCoord(mine.Q, mine.R);
                foreach (HexCoord n in ProceduralMapGenerator.Neighbors(at))
                    if (s.CanBuild(BuildingKinds.Hospital, n).Success) { s.RequestBuild(BuildingKinds.Hospital, n); break; }
            }
            double peak = 0;
            for (int h = 0; h < 600 && !a.IsOpen && !a.IsFinished; h++)
            {
                s.AdvanceHour();
                peak = Math.Max(peak, s.History.Last().EpidemicDamage);
            }
            long duration = dm?.Events.FirstOrDefault()?.EndHour is long end && end >= 0 ? end - dm.Events[0].StartHour : -1;
            if (!a.IsOpen) return (-2000, duration, peak);
            double v = SettlementValuation.Evaluate(s, a.CurrentAuction.Hour).Value;
            return (s.Cash + v * 1.05 - 2000, duration, peak);
        }
    }
}
