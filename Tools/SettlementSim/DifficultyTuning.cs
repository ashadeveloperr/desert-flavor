using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DesertSettlement.Core.Progression;

namespace SettlementSim
{
    // 난이도 곡선 조정용 실행 모드: dotnet run --project Tools/SettlementSim -- tune
    // 목표(2026-10-23 확정): 숙련 다중 봇 기준, D=1에서 첫 경매 매각 순이익 +300~600, D가 오를수록 단조 감소,
    // D가 충분히 크면 적자, 캠페인에서 6번째 정착지쯤 D=1 도달.
    public static partial class Program
    {
        private static readonly double[] TuneDs = { 0, 0.25, 0.5, 0.75, 1, 1.5, 2, 3 };

        private static int DifficultyTuning(string[] args)
        {
            var candidates = TuningCandidates();
            if (args.Length > 0) candidates = candidates.Where(c => args.Contains(c.name)).ToList();
            var config = LoadConfig();
            var rules = LoadProceduralConfig().Generator;
            const int mapsPerD = 24;

            Header($"난이도 곡선 후보 — 다중 봇 첫 경매 매각 순이익 중앙값 (시작 자금 2000, D마다 {mapsPerD}장)");
            Line("  후보 | " + string.Join(" | ", TuneDs.Select(d => $"D{d:0.##}")));
            foreach (var (name, curve) in candidates)
            {
                var cells = new List<string>();
                var detail = new List<string>();
                foreach (double d in TuneDs)
                {
                    DifficultyParams p = DifficultyParams.For(d, curve);
                    var profits = new List<double>();
                    int fail = 0, bankrupt = 0;
                    for (int i = 0; i < mapsPerD; i++)
                    {
                        GeneratedMap g = ProceduralMapGenerator.Generate(p, rules, 50000 + (int)(d * 1000) + i);
                        if (g == null) { fail++; continue; }
                        var r = ReferenceBot.Run(config, g.Map, x => x.StartingCash = 2000, seed: g.Seed, strategy: BotStrategy.Multi);
                        if (r.AuctionHour >= 0) profits.Add(r.FinalCash - 2000);
                        else { profits.Add(-2000); if (r.Bankrupt) bankrupt++; } // 파산·시간 초과 = 시작 자금 전부 잃음으로 친다
                    }
                    profits.Sort();
                    string med = profits.Count == 0 ? "생성불가" : $"{profits[profits.Count / 2],5:+0;-0}";
                    cells.Add(med + (bankrupt > 0 ? $"(파{bankrupt})" : "") + (fail > 0 ? $"(실{fail})" : ""));
                    detail.Add($"D{d:0.##} R{p.Radius} 우{p.WellCount} 광{p.OreCount} 장{p.ObstacleDensity:P0} 거{p.TargetWellOreDistance}");
                }
                Line($"  {name} | " + string.Join(" | ", cells));
                Line("        " + string.Join(" · ", detail));
            }

            foreach (var (name, curve) in candidates) TuningCampaign(name, curve, config, rules);

            string outDir = Path.Combine(FindRepoRoot(), "Tools", "balance_output");
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "difficulty_tuning.txt"), Report.ToString(), new UTF8Encoding(false));
            return 0;
        }

        /// <summary>다중 봇 캠페인(정착지 1 = 고정 맵 1): 몇 번째 정착지에서 D=1에 닿는지, 언제 적자·파산이 나는지.</summary>
        private static void TuningCampaign(string name, DifficultyCurve curve, DesertSettlement.Core.Config.EconomyConfig config, GeneratorRules rules)
        {
            var map1 = LoadMaps()[0];
            var reach = new List<int>();
            foreach (int campaignSeed in new[] { 1, 2, 3, 4, 5 })
            {
                double cash = config.Prototype.StartingCash, wm = 1.0;
                var line = new StringBuilder($"  [{name}] 캠페인 {campaignSeed}: ");
                int reached = -1;
                for (int n = 1; n <= 12; n++)
                {
                    var def = map1;
                    string label = "고정1";
                    if (n > 1)
                    {
                        DifficultyParams p = DifficultyParams.FromCash(cash, curve);
                        if (p.D >= 1 && reached < 0) reached = n;
                        var g = ProceduralMapGenerator.Generate(p, rules, campaignSeed * 100 + n);
                        if (g == null) { line.Append($"| #{n} 생성 실패 "); break; }
                        def = g.Map;
                        label = $"D{p.D:0.00}";
                    }
                    double startCash = cash, mult = wm;
                    var r = ReferenceBot.Run(config, def, x => { x.StartingCash = startCash; x.WellSupplyMultiplier = mult; },
                        seed: campaignSeed * 31 + n, strategy: BotStrategy.Multi);
                    if (r.AuctionHour < 0) { line.Append($"| #{n} {label} {r.Failure} "); break; }
                    cash = r.FinalCash;
                    if (n == 1 && cash >= 1400) { cash -= 400; wm = 1.1; }
                    line.Append($"| #{n} {label} → {cash:0} ");
                }
                if (reached > 0) reach.Add(reached);
                Line(line.ToString());
            }
            Line($"  [{name}] D=1 도달 정착지: {(reach.Count > 0 ? string.Join(", ", reach) : "없음")}");
        }

        /// <summary>비교할 곡선 후보. 이름을 인수로 주면 그 후보만 돈다(예: -- tune B C).</summary>
        private static List<(string name, DifficultyCurve curve)> TuningCandidates()
        {
            var list = new List<(string, DifficultyCurve)>();
            DifficultyCurve Make(Action<DifficultyCurve> f) { var c = new DifficultyCurve(); f(c); return c; }
            list.Add(("현재", LoadProceduralConfig().Difficulty));   // = procedural_map_config.json(후보 E에서 CashPerD 10500, 2026-10-23 확정)
            // 아래는 2026-10-23 조정 때 비교한 후보(기록용). 당시 기본값은 CashPerD 4000, 반경 첫 증가 D 0.5, 37칸당 광맥 D1 = 2.
            DifficultyCurve Old(Action<DifficultyCurve> f) => Make(c => { c.CashPerD = 4000; c.FirstRadiusStepD = 0.5; c.OresPer37AtD1 = 2; f(c); });
            list.Add(("이전", Old(c => { })));
            list.Add(("A", Old(c => { c.CashPerD = 14000; c.OresPer37AtD1 = 1; })));
            list.Add(("B", Old(c => { c.CashPerD = 14000; c.OresPer37AtD1 = 1.2; c.DistanceAtD1 = 5; })));
            list.Add(("C", Old(c => { c.CashPerD = 14000; c.OresPer37AtD1 = 1.5; c.DistanceAtD1 = 6; })));
            list.Add(("D", Old(c => { c.CashPerD = 14000; c.OresPer37AtD1 = 0.9; c.WellsPer37AtD1 = 0.9; c.DistanceAtD1 = 5; })));
            // E·F: 반경을 D=1에서 처음 키워(D<1은 반경 3) 반경이 커질 때 광맥 수가 늘어 순서가 뒤집히는 문제를 없앤다. E105 = 현재 기본값.
            list.Add(("E", Old(c => { c.CashPerD = 14000; c.FirstRadiusStepD = 1; c.OresPer37AtD1 = 0.9; })));
            list.Add(("F", Old(c => { c.CashPerD = 14000; c.FirstRadiusStepD = 1; c.OresPer37AtD1 = 0.9; c.DistanceAtD1 = 3; c.ObstacleAtD1 = 0.35; })));
            list.Add(("E11", Old(c => { c.CashPerD = 11000; c.FirstRadiusStepD = 1; c.OresPer37AtD1 = 0.9; })));
            return list;
        }
    }
}
