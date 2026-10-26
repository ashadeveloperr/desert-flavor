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
            if (args.Length > 0 && args[0] == "sweep") return EpidemicSweep(args.Skip(1).ToArray());
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
            => RunEpidemic(config, config.Disasters.Epidemic, def, epidemic, vaccinated, hospital, 0, out _);

        // 재해 밸런스 B1~B3 후보 비교: dotnet run --project Tools/SettlementSim -- epidemic sweep [맵 수]
        // 1) 동역학 표(맵 없음): 후보 수치별 지속·최대 피해·누적 피해(피해 × 시간), 방역 장비(R0 30%)·규칙 후보(설정의 저항 상승 배율, R ≥ S 유지 시간) 포함.
        // 2) 맵 측정: 후보 수치별 순이익 손해(재해 없음 대비), 방역 장비·병원 효과, 전염병 시작 시각(48h 고정 / 평가 창 겹침).
        private sealed class EpidemicCandidate
        {
            public string Name;
            public double S0, Rise, Fall = 0.01, RBase = 0.002, RDmg = 0.06, Ratio = 1; // Ratio: R ≥ Ratio × S이면 감소(1 = 예전 규칙)
            public EpidemicSettings ToSettings(EpidemicSettings src) => new EpidemicSettings
            {
                InitialSeverity = S0, SeverityRisePerHour = Rise, SeverityFallPerHour = Fall, SeverityFallResistanceRatio = Ratio,
                ResistanceBasePerHour = RBase, ResistancePerDamage = RDmg,
                ResistanceDecayPerHour = src.ResistanceDecayPerHour, HospitalDamageFactor = src.HospitalDamageFactor, HospitalRadius = src.HospitalRadius,
            };
        }

        private static readonly EpidemicCandidate[] EpidemicCandidates =
        {
            new EpidemicCandidate { Name = "옛 값(S0 5%)", S0 = 0.05, Rise = 0.01 },
            new EpidemicCandidate { Name = "S0 35%", S0 = 0.35, Rise = 0.01 },
            new EpidemicCandidate { Name = "S0 50%", S0 = 0.50, Rise = 0.01 },
            new EpidemicCandidate { Name = "S0 35% 저항↓", S0 = 0.35, Rise = 0.01, RDmg = 0.04 },
            new EpidemicCandidate { Name = "S0 20% 상승↑", S0 = 0.20, Rise = 0.015 },
            new EpidemicCandidate { Name = "S0 5% 저항↓", S0 = 0.05, Rise = 0.01, RDmg = 0.03 },
            // 세지만 2~3일 안에 끝나도록 S 감소를 빠르게.
            new EpidemicCandidate { Name = "B3 결정(35%·0.04·0.03)", S0 = 0.35, Rise = 0.01, RDmg = 0.04, Fall = 0.03 },
            new EpidemicCandidate { Name = "S0 50% 감소↑", S0 = 0.50, Rise = 0.01, Fall = 0.03 },
            new EpidemicCandidate { Name = "S0 40% 감소↑↑", S0 = 0.40, Rise = 0.01, RDmg = 0.03, Fall = 0.04 },
            // 2026-10-26 이슈 #1(EP1): 목표 = 피해 정점 24~36h·약 35%·2~3일. 감소 조건을 R ≥ 0.7 × S로(후보 ③), 낮게 시작해 빨리 오름.
            new EpidemicCandidate { Name = "EP1 결정(10%·1.5%p·R≥0.7S)", S0 = 0.10, Rise = 0.015, RDmg = 0.04, Fall = 0.03, Ratio = 0.7 },
            new EpidemicCandidate { Name = "EP1 비율 0.6", S0 = 0.10, Rise = 0.015, RDmg = 0.04, Fall = 0.03, Ratio = 0.6 },
            new EpidemicCandidate { Name = "EP1 비율 0.8", S0 = 0.10, Rise = 0.015, RDmg = 0.05, Fall = 0.03, Ratio = 0.8 },
        };

        // Core DisasterManager.EndHour와 같은 식. growth = 저항 상승 배율(방역 장비 후보 b), hold = R ≥ S가 몇 시간 이어져야 S가 줄기 시작하는지(후보 c).
        private static (int hours, double peak, double area) EpidemicDynamics(EpidemicCandidate c, double r0, double growth = 1, int hold = 0)
        {
            double s = c.S0, r = r0, peak = 0, area = 0;
            int streak = 0;
            for (int h = 1; h <= 1000; h++)
            {
                double dmg = s * (1 - r);
                peak = Math.Max(peak, dmg);
                area += dmg;
                r = Math.Min(1, r + (c.RBase + c.RDmg * dmg) * growth);
                streak = r >= c.Ratio * s ? streak + 1 : 0;
                s = streak > hold ? Math.Max(0, s - c.Fall) : Math.Min(1, s + c.Rise);
                if (s <= 1e-9) return (h, peak, area);
            }
            return (-1, peak, area);
        }

        private static int EpidemicSweep(string[] args)
        {
            var config = LoadConfig();
            var proc = LoadProceduralConfig();
            int maps = args.Length > 0 ? int.Parse(args[0]) : 20;
            EpidemicSettings src = config.Disasters.Epidemic;
            double vac = config.Equipment.Single(e => e.Id == "vaccination").Effects.EpidemicResistanceGrowthMultiplier;

            Header("전염병 동역학(맵 없음) — 지속(h) / 최대 피해 / 누적 피해(피해×h, 생산 시설 1곳의 '잃은 가동 시간')");
            // '방역 R0 30%'는 B1 결정(2026-10-23) 전 방역 장비, '방역 = 저항 상승 ×k'가 지금 방역 장비(설정 값, EP1 ×1.3). 맵 측정의 +방역도 설정의 방역 장비를 쓴다.
            Line($"  {"후보",-16} | {"장비 없음",-20} | {"옛 방역 R0 30%",-20} | {$"방역 = 저항 상승 ×{vac:0.##}",-20} | {"R≥S 12h 유지 후 감소",-20}");
            foreach (EpidemicCandidate c in EpidemicCandidates)
            {
                string F((int hours, double peak, double area) x) => $"{x.hours,3}h {x.peak,4:P0} {x.area,5:0.0}h";
                Line($"  {c.Name,-16} | {F(EpidemicDynamics(c, 0)),-20} | {F(EpidemicDynamics(c, 0.3)),-20} | {F(EpidemicDynamics(c, 0, vac)),-20} | {F(EpidemicDynamics(c, 0, 1, 12))} / 방역 {F(EpidemicDynamics(c, 0.3, 1, 12))}");
            }

            foreach (double d in new[] { 0.5, 1.0 })
            {
                var defs = Enumerable.Range(0, maps)
                    .Select(i => ProceduralMapGenerator.Generate(DifficultyParams.For(d, proc.Difficulty), proc.Generator, 95000 + (int)(d * 1000) + i))
                    .Where(g => g != null).Select(g => g.Map).ToList();
                var auctionHours = defs.Select(def => { RunEpidemic(config, src, def, false, false, false, 0, out long ah); return (double)ah; }).ToList();
                Header($"맵 측정 D {d:0.0} ({defs.Count}장) — 재해 없음 대비 순이익 손해 중앙값. 경매 시각 중앙값 {Median(auctionHours):0}h");
                Line("  시작 48h = 유예 직후 고정 / 창 겹침 = 경매 직전 24h 평가 창 가운데에 전염병 한가운데가 오도록(최악에 가까움)");
                Line($"  {"후보",-16} | {"48h 손해",8} | {"+방역",8} | {"+병원",8} | {"창 겹침 손해",10} | {"+방역",8} | {"+병원",8}");
                foreach (EpidemicCandidate c in EpidemicCandidates)
                {
                    EpidemicSettings ep = c.ToSettings(src);
                    int mid = Math.Max(1, EpidemicDynamics(c, 0).hours / 2);
                    string Loss(bool vac, bool hosp, bool window)
                    {
                        var losses = defs.Select((def, i) =>
                        {
                            double baseProfit = RunEpidemic(config, ep, def, false, false, false, 0, out _).profit;
                            long startAt = window ? Math.Max(0, (long)auctionHours[i] - 12 - mid - 48) : 0; // 유예 48h 뒤로 미룰 시간
                            return RunEpidemic(config, ep, def, true, vac, hosp, startAt, out _).profit - baseProfit;
                        }).ToList();
                        return $"{Median(losses),8:+0;-0}";
                    }
                    Line($"  {c.Name,-16} | {Loss(false, false, false)} | {Loss(true, false, false)} | {Loss(false, true, false)} | {Loss(false, false, true),10} | {Loss(true, false, true)} | {Loss(false, true, true)}");
                }
            }

            // B2 병원 후보: 비용(재해 없이 병원만 지었을 때 손해)과 전염병 때 이득(병원 있음 − 병원 없음, 같은 전염병).
            // 이득 열: 설정의 전염병(B3 결정 수치)과 더 센 후보 C. 유지비만 0 / 식량만 0은 손해의 원인을 가르는 진단용.
            // 유지비·식량은 절대값. B2 결정(2026-10-23) = 유지 0.5·식량 0·반경 1.
            var hospitalVariants = new (string name, double upkeep, double food, int radius)[]
            {
                ("옛 값(유지 1.0·식량 0.5)", 1, 0.5, 1), ("옛 값, 유지비 0(진단)", 0, 0.5, 1), ("옛 값, 식량 0(진단)", 1, 0, 1),
                ("유지·식량 절반", 0.5, 0.25, 1), ("B2 결정(유지 0.5·식량 0)", 0.5, 0, 1), ("B2 결정 + 반경 2", 0.5, 0, 2),
            };
            foreach (double d in new[] { 0.5, 1.0 })
            {
                var defs = Enumerable.Range(0, maps)
                    .Select(i => ProceduralMapGenerator.Generate(DifficultyParams.For(d, proc.Difficulty), proc.Generator, 95000 + (int)(d * 1000) + i))
                    .Where(g => g != null).Select(g => g.Map).ToList();
                Header($"병원 후보 D {d:0.0} — 비용 = 재해 없이 병원만 지은 손해, 이득 = 같은 전염병(48h 시작)에서 병원 있음 − 없음 (중앙값)");
                Line($"  {"병원",-26} | {"비용",7} | {"이득: 설정 수치",14} | {"이득: 더 센 C",14}");
                foreach (var hv in hospitalVariants)
                {
                    EconomyConfig cfg = LoadConfig();
                    BuildingLevel lv = cfg.BuildingLevels[BuildingKinds.Hospital][0];
                    lv.Upkeep = hv.upkeep;
                    lv.FoodUse = hv.food;
                    EpidemicSettings cur = new EpidemicCandidate
                    {
                        S0 = src.InitialSeverity, Rise = src.SeverityRisePerHour, Fall = src.SeverityFallPerHour, RBase = src.ResistanceBasePerHour, RDmg = src.ResistancePerDamage, Ratio = src.SeverityFallResistanceRatio,
                    }.ToSettings(src);
                    EpidemicSettings strong = EpidemicCandidates.Single(c => c.Name.StartsWith("S0 40%")).ToSettings(src);
                    cur.HospitalRadius = strong.HospitalRadius = hv.radius;
                    double cost = Median(defs.Select(def =>
                        RunEpidemic(cfg, cur, def, false, false, true, 0, out _).profit - RunEpidemic(cfg, cur, def, false, false, false, 0, out _).profit).ToList());
                    double Gain(EpidemicSettings ep) => Median(defs.Select(def =>
                        RunEpidemic(cfg, ep, def, true, false, true, 0, out _).profit - RunEpidemic(cfg, ep, def, true, false, false, 0, out _).profit).ToList());
                    HospitalCoverage = new List<(int, int, int)>();
                    double gainCur = Gain(cur);
                    var cov = HospitalCoverage;
                    HospitalCoverage = null;
                    double gainStrong = Gain(strong);
                    Line($"  {hv.name,-26} | {cost,7:+0;-0} | {gainCur,14:+0;-0} | {gainStrong,14:+0;-0}"
                        + $"   병원 세운 맵 {cov.Count(c => c.hospitals > 0)}/{cov.Count}, 덮은 생산 시설 평균 {cov.Average(c => c.covered):0.0}/{cov.Average(c => c.production):0.0}");
                }
            }
            string outDir = Path.Combine(FindRepoRoot(), "Tools", "balance_output");
            File.WriteAllText(Path.Combine(outDir, "epidemic_sweep.txt"), Report.ToString(), new UTF8Encoding(false));
            return 0;
        }

        // 진단용: 병원 개수·병원 반경 안 생산 시설 수·전체 생산 시설 수(설정되어 있을 때만 기록).
        private static List<(int hospitals, int covered, int production)> HospitalCoverage;

        private static double Median(List<double> xs) => xs.Count == 0 ? double.NaN : xs.OrderBy(x => x).ElementAt(xs.Count / 2);

        // delay: 유예(48h) 뒤로 전염병 시작을 미루는 시간. auctionHour: 첫 경매 시각(경매가 안 열리면 -1).
        private static (double profit, long duration, double peak) RunEpidemic(EconomyConfig config, EpidemicSettings epidemicSettings, MapDefinition def,
            bool epidemic, bool vaccinated, bool hospital, long delay, out long auctionHour)
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
                    GraceHours = src.GraceHours + (int)delay, WarningHours = src.WarningHours, ScheduleWindowHours = 1,
                    CountWeights = new List<double> { 0, 1 }, Types = src.Types.Where(t => t.Id == DisasterManager.EpidemicId).ToList(), Epidemic = epidemicSettings,
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
            auctionHour = a.CurrentAuction.Hour;
            if (hospital && HospitalCoverage != null)
            {
                var hs = s.OperatingOf(BuildingKinds.Hospital).ToList();
                var prod = new[] { BuildingKinds.Farm, BuildingKinds.Quarry, BuildingKinds.Mine }.SelectMany(k => s.OperatingOf(k)).ToList();
                HospitalCoverage.Add((hs.Count, prod.Count(f => hs.Any(hp => hp.Tile.DistanceTo(f.Tile) <= epidemicSettings.HospitalRadius)), prod.Count));
            }
            double v = SettlementValuation.Evaluate(s, a.CurrentAuction.Hour).Value;
            return (s.Cash + v * 1.05 - 2000, duration, peak);
        }
    }
}
