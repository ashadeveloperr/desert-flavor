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
    // 전염병 감염·회복 모형(결정 EV1~EV7) 측정: dotnet run --project Tools/SettlementSim -- epidemic [맵 수]
    //   같은 맵·다중 봇 건설 목록으로 '재해 없음 / 전염병 1회(유예 직후 시작) / + 방역 장비 / + 병원 / + 둘 다'를
    //   돌려 매각 후 순이익(경매 직전 현금 + 중립 V × 1.05 − 2000) 중앙값과 전염병 지속·최대 아픈 비율을 비교한다.
    // 후보 비교: -- epidemic sweep [맵 수] — 동역학 표(맵 없음, 주민 수 고정) + 맵 손해(재해 없음 대비).
    public static partial class Program
    {
        private static int EpidemicTuning(string[] args)
        {
            if (args.Length > 0 && args[0] == "sweep") return EpidemicSweep(args.Skip(1).ToArray());
            var config = LoadConfig();
            var proc = LoadProceduralConfig();
            int maps = args.Length > 0 ? int.Parse(args[0]) : 20;
            Header("전염병 측정 — 다중 봇, 시작 자금 2000, 전염병은 유예 직후 시작하도록 고정");
            string[] variants = { "재해 없음", "전염병", "+방역 장비", "+병원", "+둘 다" };
            foreach (double d in new[] { 0.5, 1.0 })
            {
                List<MapDefinition> defs = EpidemicMaps(proc, d, maps);
                Line();
                Line($"  ▸ D {d:0.0} ({defs.Count}장) | 순이익 중앙값 | 전염병 지속(h, 경매 전에 끝난 맵만) | 최대 아픈 비율");
                foreach (string v in variants)
                {
                    var runs = defs.Select(def => RunEpidemic(config, config.Disasters.Epidemic, def, v != "재해 없음",
                        v.Contains("방역") || v.Contains("둘"), v.Contains("병원") || v.Contains("둘"), out _)).ToList();
                    Line($"    {v,-10} | {Median(runs.Select(r => r.profit).ToList()),8:+0;-0} | {Median(runs.Where(r => r.duration >= 0).Select(r => (double)r.duration).ToList()),6:0}" +
                         $" | {Median(runs.Select(r => r.peak).ToList()),6:P0}");
                }
            }
            string outDir = Path.Combine(FindRepoRoot(), "Tools", "balance_output");
            File.WriteAllText(Path.Combine(outDir, "epidemic_report.txt"), Report.ToString(), new UTF8Encoding(false));
            return 0;
        }

        private static List<MapDefinition> EpidemicMaps(ProceduralMapConfig proc, double d, int maps) =>
            Enumerable.Range(0, maps)
                .Select(i => ProceduralMapGenerator.Generate(DifficultyParams.For(d, proc.Difficulty), proc.Generator, 95000 + (int)(d * 1000) + i))
                .Where(g => g != null).Select(g => g.Map).ToList();

        /// <summary>전염병 수치 후보. 이름 · 시작 아픈 비율 · 전파율 · 집 회복률 · 병원 회복률.</summary>
        private sealed class EpidemicCandidate
        {
            public string Name;
            public double I0, Beta, Home, Hospital;
            public EpidemicSettings ToSettings(EpidemicSettings src) => new EpidemicSettings
            {
                InitialInfectedShare = I0, TransmissionPerHour = Beta, HomeRecoveryPerHour = Home, HospitalRecoveryPerHour = Hospital,
                ImmunityCarryover = src.ImmunityCarryover, EndBelowSickShare = src.EndBelowSickShare,
            };
        }

        private static readonly EpidemicCandidate[] EpidemicCandidates =
        {
            new EpidemicCandidate { Name = "A 빠름(R0 3.8)", I0 = 0.05, Beta = 0.3, Home = 0.08, Hospital = 0.3 },
            new EpidemicCandidate { Name = "B 중간(R0 3.3)", I0 = 0.05, Beta = 0.2, Home = 0.06, Hospital = 0.3 },
            new EpidemicCandidate { Name = "C 느림(R0 3)", I0 = 0.05, Beta = 0.15, Home = 0.05, Hospital = 0.25 },
            new EpidemicCandidate { Name = "D 약함(R0 2.5)", I0 = 0.05, Beta = 0.15, Home = 0.06, Hospital = 0.3 },
            new EpidemicCandidate { Name = "E 느리고 셈(R0 4)", I0 = 0.03, Beta = 0.16, Home = 0.04, Hospital = 0.25 },
            new EpidemicCandidate { Name = "F 셈·병원 2배", I0 = 0.05, Beta = 0.35, Home = 0.08, Hospital = 0.16 },
            new EpidemicCandidate { Name = "G 중간·병원 2배", I0 = 0.05, Beta = 0.25, Home = 0.06, Hospital = 0.12 },
            new EpidemicCandidate { Name = "H 셈·병원 3배", I0 = 0.08, Beta = 0.4, Home = 0.08, Hospital = 0.24 },
        };

        /// <summary>
        /// Core DisasterManager.EndHour와 같은 식(주민 수 고정). 정점 아픈 비율·정점 시각·지속·일 못한 사람×시간(주민 대비).
        /// </summary>
        private static (int hours, double peak, int peakHour, double area) EpidemicDynamics(EpidemicCandidate c, double end, double residents, double beds, double immuneShare)
        {
            double immune = immuneShare * residents;
            double sick = Math.Max(0, residents - immune) * c.I0, peak = 0, area = 0;
            int peakHour = 0;
            for (int h = 1; h <= 1000; h++)
            {
                double susceptible = Math.Max(0, residents - sick - immune);
                double inHospital = Math.Min(sick, beds);
                double newCases = Math.Min(susceptible, c.Beta * susceptible * sick / residents);
                double recovered = Math.Min(sick, inHospital * c.Hospital + (sick - inHospital) * c.Home);
                sick = Math.Max(0, sick + newCases - recovered);
                immune += recovered;
                if (sick / residents > peak) { peak = sick / residents; peakHour = h; }
                area += sick / residents;
                if (sick / residents < end) return (h, peak, peakHour, area);
            }
            return (-1, peak, peakHour, area);
        }

        private static int EpidemicSweep(string[] args)
        {
            var config = LoadConfig();
            var proc = LoadProceduralConfig();
            int maps = args.Length > 0 ? int.Parse(args[0]) : 20;
            EpidemicSettings src = config.Disasters.Epidemic;
            double vac = config.Equipment.Single(e => e.Id == "vaccination").Effects.EpidemicImmuneShare;
            double beds = config.GetLevel(BuildingKinds.Hospital, 1).Beds ?? 0;

            Header($"전염병 동역학(맵 없음, 주민 20명) — 지속(h) / 정점 아픈 비율 @시각 / 일 못한 시간(주민 1명 기준 h). 끝 = 아픈 비율 {src.EndBelowSickShare:P0} 아래");
            Line($"  {"후보",-18} | {"장비 없음",-22} | {"+병원 2병상",-22} | {"+병원 4병상",-22} | {$"+방역 {vac:P0}",-22}");
            foreach (EpidemicCandidate c in EpidemicCandidates)
            {
                string F((int hours, double peak, int peakHour, double area) x) => $"{x.hours,3}h {x.peak,4:P0}@{x.peakHour,2}h {x.area,4:0.0}h";
                Line($"  {c.Name,-18} | {F(EpidemicDynamics(c, src.EndBelowSickShare, 20, 0, 0)),-22} | {F(EpidemicDynamics(c, src.EndBelowSickShare, 20, 2, 0)),-22} | {F(EpidemicDynamics(c, src.EndBelowSickShare, 20, 4, 0)),-22} | {F(EpidemicDynamics(c, src.EndBelowSickShare, 20, 0, vac))}");
            }

            foreach (double d in new[] { 0.5, 1.0 })
            {
                List<MapDefinition> defs = EpidemicMaps(proc, d, maps);
                Header($"맵 측정 D {d:0.0} ({defs.Count}장) — 재해 없음 대비 순이익 손해 중앙값(전염병은 유예 직후)");
                Line($"  {"후보",-18} | {"손해",8} | {"+방역",8} | {"+병원",8}");
                foreach (EpidemicCandidate c in EpidemicCandidates)
                {
                    EpidemicSettings ep = c.ToSettings(src);
                    string Loss(bool vaccine, bool hospital) => $"{Median(defs.Select(def => RunEpidemic(config, ep, def, true, vaccine, hospital, out _).profit - RunEpidemic(config, ep, def, false, false, false, out _).profit).ToList()),8:+0;-0}";
                    Line($"  {c.Name,-18} | {Loss(false, false)} | {Loss(true, false)} | {Loss(false, true)}");
                }
            }
            string outDir = Path.Combine(FindRepoRoot(), "Tools", "balance_output");
            File.WriteAllText(Path.Combine(outDir, "epidemic_sweep.txt"), Report.ToString(), new UTF8Encoding(false));
            return 0;
        }

        private static double Median(List<double> xs) => xs.Count == 0 ? double.NaN : xs.OrderBy(x => x).ElementAt(xs.Count / 2);

        /// <summary>auctionHour: 첫 경매 시각(경매가 안 열리면 -1). peak = 최대 아픈 주민 비율.</summary>
        private static (double profit, long duration, double peak) RunEpidemic(EconomyConfig config, EpidemicSettings epidemicSettings, MapDefinition def,
            bool epidemic, bool vaccinated, bool hospital, out long auctionHour)
        {
            auctionHour = -1;
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
                    CountWeights = new List<double> { 0, 1 }, Types = src.Types.Where(t => t.Id == DisasterManager.EpidemicId).ToList(), Epidemic = epidemicSettings,
                }, 1);
            }
            var withPlan = new MapDefinition { Id = def.Id, Radius = def.Radius, Terrain = def.Terrain, SampleLayout = plan };
            if (withPlan.RequestSampleLayout(s).Count > 0) return (-2000, -1, 0);
            if (hospital)
            {
                HexCoord? at = s.Map.Tiles.Select(t => (HexCoord?)t.Coord).FirstOrDefault(c => s.CanBuild(BuildingKinds.Hospital, c.Value).Success);
                if (at.HasValue) s.RequestBuild(BuildingKinds.Hospital, at.Value);
            }
            double peak = 0;
            for (int h = 0; h < 600 && !a.IsOpen && !a.IsFinished; h++)
            {
                ReferenceBot.UnblockStone(s);
                s.AdvanceHour();
                peak = Math.Max(peak, s.History.Last().EpidemicSickShare);
            }
            long duration = dm?.Events.FirstOrDefault()?.EndHour is long end && end >= 0 ? end - dm.Events[0].StartHour : -1;
            if (!a.IsOpen) return (-2000, duration, peak);
            auctionHour = a.CurrentAuction.Hour;
            double v = SettlementValuation.Evaluate(s, a.CurrentAuction.Hour).Value;
            return (s.Cash + v * 1.05 - 2000, duration, peak);
        }
    }
}
