using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DesertSettlement.Core.Auction;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;
using DesertSettlement.Core.Progression;

namespace SettlementSim
{
    /// <summary>
    /// 새 시스템 밸런스 QA(주택·노동력·식품·생활 수준·향신료, 결정 LB·FD·LS·SP). `-- qa [static|marginal] [D마다 맵 수]`:
    /// - static: 시설 L1~L3의 비용·유지비·일손·물과 시간당 만드는 것(순 식량 가치·판매가 기준 부가가치)을 설정에서 읽어 한 표로,
    ///   재화마다 시장 하나로 오래 팔 때 시간당 최대 수입(판매 압력이 회복과 맞서는 정상 상태).
    /// - marginal: 생성 맵(D0.5~3.5)마다 다중 봇 계획을 끝낸 뒤 시설(+ 주택)을 더 짓고 첫 경매까지 돌려,
    ///   더 짓지 않은 같은 맵과 최종 자금(매각가 + 남은 재화 일괄 매도 뒤 현금) 차이를 잰다.
    ///   재화 처리: '먹기'(창고가 넘칠 때만 팖) · '팔기'(판매가가 기준의 절반 이상이면 매시간 팖). 모든 갈래에서 광산을 우선 고용한다.
    /// </summary>
    public static partial class Program
    {
        private const double QaStartingCash = 3000;
        private const int QaAddBy = 40;
        /// <summary>갈래의 Builds에 넣으면 시설 대신 운영 중인 우물을 모두 한 단계 개량한다.</summary>
        private const string QaWellUpgrade = "wellup";

        private sealed class QaArm
        {
            public string Name;
            public string[] Builds = Array.Empty<string>();
            public bool SellAll;
            public string Compare = "base";
            public Action<EconomyConfig> Tweak;
        }

        private sealed class QaRun
        {
            public bool Ok;
            public double Final, Bid, Sold, Liquidated, Living, Target, LivingMin, LivingNegHours, Ore24, FoodMult, Residents, Idle, MineStaff;
            public double ShortFoodHours, ShortWaterHours; // 생활 수준 목표가 음수였던 시간: 식량 부족 때문 · 주택 물 부족 때문
            public double NegFirst24;                       // 처음 24시간 중 생활 수준이 음수였던 시간
            public double SpareWater, UnusedSources;        // 경매 때 우물이 낼 수 있는데 안 나간 물(시간당), 우물 없는 수원 칸 수
            public double AddedStaff = double.NaN, AddedWater = double.NaN;
            public int Missing;
            public string Winner;
        }

        private static int BalanceQa(string[] args)
        {
            var config = LoadConfig();
            string part = args.Length > 0 ? args[0] : "all";
            int perD = args.Length > 1 && int.TryParse(args[1], out int n) ? n : 8;
            bool water2 = args.Contains("water2"); // 우물 방출·수로 용량 ×2: 물 경쟁 없이 시설 자체의 값
            string lab = args.FirstOrDefault(a => a.StartsWith("lab="))?.Substring(4); // 수치 후보(LaborCandidates), 모든 갈래에 건다
            if (part == "static" || part == "all") QaStatic(config);
            if (part == "marginal" || part == "all") QaMarginal(perD, water2, lab);
            string outDir = Path.Combine(FindRepoRoot(), "Tools", "balance_output");
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "balance_qa.txt"), Report.ToString(), new UTF8Encoding(false));
            return 0;
        }

        // ───────────────────────── static ─────────────────────────

        /// <summary>판매 기준가(곡물은 식량 판매가, 나머지는 goods.*.sellPrice).</summary>
        private static double QaSellPrice(EconomyConfig c, Goods g) =>
            g == Goods.Grain ? c.Purchase.Food.BaseCost * c.Purchase.Food.SellRatio : GoodsCatalog.Settings(c, g).SellPrice;

        private static double QaSellLimit(EconomyConfig c, Goods g) =>
            g == Goods.Grain ? c.Purchase.Food.SellLimit : GoodsCatalog.Settings(c, g).SellLimit;

        /// <summary>
        /// 시장 하나에서 매시간 r개씩 오래 팔 때의 시간당 수입이 가장 큰 r과 그 수입.
        /// 정상 상태 압력 P0 = r × keep ÷ (1 − keep), 그 시간 단가 ≈ 기준가 × (1 − ((P0 + r/2) ÷ 한도)²).
        /// </summary>
        private static (double Rate, double Income) QaBestSellRate(EconomyConfig c, Goods g)
        {
            double price = QaSellPrice(c, g), limit = QaSellLimit(c, g), keep = 1 - c.Purchase.Trade.RecoveryPerHour;
            (double, double) best = (0, 0);
            for (double r = 0.05; r <= 20; r += 0.05)
            {
                double p0 = r * keep / (1 - keep);
                double x = (p0 + r / 2) / limit;
                double income = r * price * Math.Max(0, 1 - x * x);
                if (income > best.Item2) best = (r, income);
            }
            return best;
        }

        private static void QaStatic(EconomyConfig config)
        {
            var rules = SettlementRules.FromConfig(config);
            Header("시설 수치표 — 시간당, 물·일손 100%, 생활 수준 0 (판매 부가가치 = 만든 것 판매가 − 원료 판매가, 곡물 판매가 = 식량 기준가 × 판매 비율)");
            Line("  시설        L  석재 시간 유지비 일손 공사 물   만드는 것          원료         순식량  판매부가  /일손  /물   등급");
            string[] kinds = { BuildingKinds.Farm, BuildingKinds.Orchard, BuildingKinds.Pasture, BuildingKinds.HerbFarm, BuildingKinds.Mill,
                BuildingKinds.Brewery, BuildingKinds.Winery, BuildingKinds.Quarry, BuildingKinds.Mine, BuildingKinds.Well, BuildingKinds.House };
            foreach (string kind in kinds)
            {
                List<BuildingLevel> levels = config.BuildingLevels[kind];
                for (int i = 0; i < levels.Count; i++)
                {
                    BuildingLevel lv = levels[i];
                    double output = rules.MaxOutput(kind, lv);
                    Goods? g = kind == BuildingKinds.Farm ? Goods.Grain : SettlementRules.GoodsOf(kind);
                    Goods? input = SettlementRules.InputOf(kind);
                    double inputPerHour = input == null ? 0 : output * (lv.InputPerOutput ?? 0);
                    double netFood = kind == BuildingKinds.Farm ? output : rules.NetFoodValue(kind, output, lv);
                    double sale = g == null ? 0 : output * QaSellPrice(config, g.Value) - (input == null ? 0 : inputPerHour * QaSellPrice(config, input.Value));
                    double workers = lv.Workers ?? 0, water = lv.WaterDemand ?? 0;
                    string made = kind == BuildingKinds.Mine ? $"광물 {output:0.#}" : kind == BuildingKinds.Quarry ? $"석재 {output:0.#}"
                        : kind == BuildingKinds.Well ? $"물 {lv.WaterSupply:0}" : kind == BuildingKinds.House ? $"정원 {lv.Residents:0}"
                        : g == null ? "" : $"{GoodsCatalog.Name(g.Value)} {output:0.##}";
                    string used = input == null ? "" : $"{GoodsCatalog.Name(input.Value)} {inputPerHour:0.##}";
                    string grade = g == null ? "" : GoodsCatalog.Settings(config, g.Value).StandardPoints.ToString("0");
                    Line($"  {FacilityCatalog.NameOf(kind),-8} L{i + 1} {lv.Stone,4:0} {lv.Hours,4:0} {lv.Upkeep,6:0.###} {workers,4:0} {lv.BuildWorkers,4:0} {water,4:0}  {made,-16} {used,-12} " +
                         $"{(g == null ? "" : netFood.ToString("+0.##;-0.##;0")),6} {(g == null ? "" : sale.ToString("+0.##;-0.##;0")),8} " +
                         $"{(g == null || workers <= 0 ? "" : (sale / workers).ToString("0.##")),6} {(g == null || water <= 0 ? "" : (sale / water).ToString("0.###")),6} {grade,5}");
                }
            }
            Line();
            Line($"  재화 시장(시장 1개, 회복 {config.Purchase.Trade.RecoveryPerHour:P0}/h): 기준가 · 한도 · 오래 팔 때 시간당 최대 수입(그때 시간당 판매량)");
            foreach (Goods g in GoodsCatalog.All)
            {
                (double rate, double income) = QaBestSellRate(config, g);
                GoodsSettings gs = GoodsCatalog.Settings(config, g);
                Line($"    {GoodsCatalog.Name(g),-4} 기준 {QaSellPrice(config, g),4:0.##} · 한도 {QaSellLimit(config, g),4:0} · 최대 {income,5:0.00}/h ({rate:0.0}개/h)" +
                     $" · 식량 효율 {gs.FoodValue:0.#} 가중치 {(GoodsCatalog.IsDrink(g) ? gs.DrinkWeight : gs.EatWeight):0} 등급 {gs.StandardPoints:0} 보관 {gs.KeepRank:0}");
            }
            Line($"  주민 1명 시간당: 식량 {config.Labor.FoodPerResidentPerHour} · 물 {config.Labor.WaterPerResidentPerHour} · 기호품 {config.Labor.DrinkPerResidentPerHour}" +
                 $" / 경매 주민 가치 {config.AuctionSeed.ValuePerResident} × 입찰자 주민 가중치");
        }

        // ───────────────────────── marginal ─────────────────────────

        private static List<QaArm> QaArms()
        {
            string H = BuildingKinds.House;
            var arms = new List<QaArm>
            {
                new QaArm { Name = "base" },
                new QaArm { Name = "house", Builds = new[] { H } },
            };
            var singles = new (string Name, string[] Builds)[]
            {
                ("farm", new[] { H, BuildingKinds.Farm }),
                ("orchard", new[] { H, BuildingKinds.Orchard }),
                ("pasture", new[] { H, BuildingKinds.Pasture }),
                ("mill", new[] { H, BuildingKinds.Mill }),
                ("brewery", new[] { H, BuildingKinds.Brewery }),
                ("winery", new[] { H, BuildingKinds.Orchard, BuildingKinds.Winery }),
                ("herb", new[] { H, BuildingKinds.HerbFarm }),
                ("diet", new[] { H, H, BuildingKinds.Orchard, BuildingKinds.Mill, BuildingKinds.Pasture, BuildingKinds.Brewery, BuildingKinds.Winery }),
                ("diet-drinks", new[] { H, H, BuildingKinds.Orchard, BuildingKinds.Mill, BuildingKinds.Pasture }),
                ("wellup", new[] { QaWellUpgrade }),
                ("wellup+diet-drinks", new[] { QaWellUpgrade, H, H, BuildingKinds.Orchard, BuildingKinds.Mill, BuildingKinds.Pasture }),
                ("diet+herb", new[] { H, H, H, BuildingKinds.Orchard, BuildingKinds.Mill, BuildingKinds.Pasture, BuildingKinds.Brewery, BuildingKinds.Winery, BuildingKinds.HerbFarm }),
            };
            foreach ((string name, string[] builds) in singles)
            {
                string compare = name == "winery" ? "orchard" : name.StartsWith("diet") || name.StartsWith("wellup") ? "base" : "house";
                arms.Add(new QaArm { Name = name + " 먹기", Builds = builds, Compare = compare == "base" || compare == "house" ? compare : compare + " 먹기" });
                arms.Add(new QaArm { Name = name + " 팔기", Builds = builds, SellAll = true, Compare = compare == "base" || compare == "house" ? compare : compare + " 팔기" });
            }
            // 생활 수준 효과(생산·이주·경매)를 끈 같은 식단: 재화 자체의 값과 생활 수준의 값을 나눈다.
            arms.Add(new QaArm
            {
                Name = "diet+herb 먹기(생활 효과 끔)", Builds = arms.First(a => a.Name == "diet+herb 먹기").Builds, Compare = "diet+herb 먹기",
                Tweak = c =>
                {
                    c.Living.ProductionAtMax = 0;
                    c.Living.LeaveFactorAtMax = 1;
                    foreach (BidderProfile b in c.Auction.Bidders) b.LivingStandardWeight = 0;
                },
            });
            return arms;
        }

        private static void QaMarginal(int perD, bool water2, string lab)
        {
            var procedural = LoadProceduralConfig();
            double[] ds = { 0.5, 1.5, 2.5, 3.5 };
            var maps = new List<(double D, MapDefinition Map, int Seed)>();
            foreach (double d in ds)
                for (int i = 0; i < perD; i++)
                {
                    int seed = 70000 + (int)(d * 1000) + i * 13;
                    GeneratedMap g = ProceduralMapGenerator.Generate(DifficultyParams.For(d, procedural.Difficulty), procedural.Generator, seed);
                    if (g?.Map != null) maps.Add((d, g.Map, seed));
                }
            List<QaArm> arms = QaArms();
            var configs = arms.Select(a =>
            {
                EconomyConfig c = LoadConfig();
                a.Tweak?.Invoke(c);
                if (lab != null) ApplyLaborCandidate(c, lab);
                if (water2)
                {
                    Levels(c, BuildingKinds.Well, (lv, i) => lv.WaterSupply *= 2);
                    Levels(c, BuildingKinds.CanalEdge, (lv, i) => lv.CapacityPerHour *= 2);
                }
                return c;
            }).ToList();
            var results = new QaRun[arms.Count, maps.Count];
            Parallel.For(0, arms.Count * maps.Count, k =>
            {
                int ai = k / maps.Count, mi = k % maps.Count;
                results[ai, mi] = QaPlay(configs[ai], maps[mi].Map, arms[ai], maps[mi].Seed);
            });

            Header($"시설 하나 더 짓기 — 생성 맵 D{string.Join("/", ds)} × {perD}장(재해 없음, 시작 자금 {QaStartingCash}, 계획이 끝나거나 {QaAddBy}h에 추가, 첫 경매에 최고 현금 입찰로 매각)"
                + (water2 ? ", 우물·수로 ×2(물 넉넉)" : ", 지금 물") + (lab != null ? $", 후보 {lab}" : ""));
            Line("  Δ = 비교 갈래와의 최종 자금 차이(같은 맵끼리). 이득률 = Δ>0 맵 비율. 생활 = 매각 때 생활 수준. 판매 = 플레이 중 시장 판매 + 매각 때 일괄 매도.");
            Line("  배치·물 = 더 지은 생산 시설의 운영 중 평균 배치율·물 공급률. 광물 = 경매 직전 24h 광물(비교 갈래 대비). 자리 없음 = 지을 칸을 못 찾은 맵 수.");
            int baseIdx = arms.FindIndex(a => a.Name == "base");
            for (int ai = 0; ai < arms.Count; ai++)
            {
                QaArm arm = arms[ai];
                int ci = arms.FindIndex(a => a.Name == arm.Compare);
                var pairs = Enumerable.Range(0, maps.Count).Where(mi => results[ai, mi].Ok && results[ci, mi].Ok).ToList();
                if (pairs.Count == 0) { Line($"  {arm.Name}: 결과 없음"); continue; }
                var deltas = pairs.Select(mi => results[ai, mi].Final - results[ci, mi].Final).OrderBy(x => x).ToList();
                var byD = ds.Select(d => pairs.Where(mi => maps[mi].D == d).Select(mi => results[ai, mi].Final - results[ci, mi].Final).DefaultIfEmpty(double.NaN).Average());
                QaRun[] runs = pairs.Select(mi => results[ai, mi]).ToArray();
                double ore = pairs.Average(mi => results[ai, mi].Ore24 - results[ci, mi].Ore24);
                int missing = Enumerable.Range(0, maps.Count).Count(mi => results[ai, mi].Missing > 0);
                double staff = runs.Where(r => !double.IsNaN(r.AddedStaff)).Select(r => r.AddedStaff).DefaultIfEmpty(double.NaN).Average();
                double water = runs.Where(r => !double.IsNaN(r.AddedWater)).Select(r => r.AddedWater).DefaultIfEmpty(double.NaN).Average();
                int colonist = runs.Count(r => r.Winner == "이주 사업가");
                string head = ai == ci ? $"  {arm.Name,-28} 최종 {runs.Average(r => r.Final),6:0} (매각 {runs.Average(r => r.Bid):0})"
                    : $"  {arm.Name,-28} Δ{deltas.Average(),6:+0;-0} 중앙 {deltas[deltas.Count / 2],5:+0;-0} 이득률 {deltas.Count(x => x > 0) * 100 / deltas.Count,3}% vs {arm.Compare,-10}";
                Line(head + $" | D별 {string.Join(" ", byD.Select(x => double.IsNaN(x) ? "  -" : x.ToString("+0;-0")))}" +
                     $" | 생활 {runs.Average(r => r.Living),4:+0;-0;0}(목표 {runs.Average(r => r.Target):+0;-0;0}) 판매 {runs.Average(r => r.Sold + r.Liquidated),4:0}" +
                     $" 배치 {(double.IsNaN(staff) ? "-" : staff.ToString("P0"))} 물 {(double.IsNaN(water) ? "-" : water.ToString("P0"))}" +
                     $" 광물 {ore:+0;-0} 주민 {runs.Average(r => r.Residents):0} 식량배율 {runs.Average(r => r.FoodMult):0.00} 이주사업가 낙찰 {colonist}" +
                     (missing > 0 ? $" 자리 없음 {missing}" : "") + $" (맵 {pairs.Count})");
            }
            // 기준 갈래의 노동력·생활 수준 흐름(새 시스템 진단).
            QaRun[] b = Enumerable.Range(0, maps.Count).Select(mi => results[baseIdx, mi]).Where(r => r.Ok).ToArray();
            Line();
            Line($"  기준 갈래 진단(맵 {b.Length}): 생활 수준 최저 평균 {b.Average(r => r.LivingMin):0} · 음수였던 시간 평균 {b.Average(r => r.LivingNegHours):0.0}h" +
                 $" (음수를 겪은 맵 {b.Count(r => r.LivingNegHours > 0)}) · 놀던 주민 평균 {b.Average(r => r.Idle):0.0}명 · 광산 배치율 평균 {b.Average(r => r.MineStaff):P0}" +
                 $" · 매각 때 주민 {b.Average(r => r.Residents):0}명 · 매각 때 생활 수준 음수 {b.Count(r => r.Living < 0)}맵");
            Line($"  생활 수준 목표가 음수였던 시간(맵 평균): 식량 부족 {b.Average(r => r.ShortFoodHours):0.0}h · 주택 물 부족 {b.Average(r => r.ShortWaterHours):0.0}h · 처음 24h 중 음수 {b.Average(r => r.NegFirst24):0.0}h");
            Line($"  물 여유(경매 때 우물이 더 낼 수 있던 물): 평균 {b.Average(r => r.SpareWater):0}/h · 20 이상인 맵 {b.Count(r => r.SpareWater >= 20)} · 우물 없는 수원 칸이 있는 맵 {b.Count(r => r.UnusedSources > 0)} (평균 {b.Average(r => r.UnusedSources):0.0}칸)");
            int failed = Enumerable.Range(0, arms.Count).Sum(ai => Enumerable.Range(0, maps.Count).Count(mi => !results[ai, mi].Ok));
            if (failed > 0) Line($"  실패한 실행(계획 불가·파산·시간 초과): {failed}");
        }

        /// <summary>한 맵·한 갈래: 다중 봇 계획 → (계획이 끝나거나 QaAddBy 시각에) 시설 추가 → 첫 경매 → 일괄 매도 + 최고 현금 입찰.</summary>
        private static QaRun QaPlay(EconomyConfig config, MapDefinition map, QaArm arm, int seed)
        {
            var r = new QaRun();
            List<MapBuildEntry> plan = ReferenceBot.Plan(map, BotStrategy.Multi);
            if (plan == null) return r;
            SettlementRules rules = SettlementRules.FromConfig(config);
            rules.StartingCash = QaStartingCash;
            var s = new Settlement(map.CreateMap(), rules);
            var a = new AuctionManager(s, seed);
            var withPlan = new MapDefinition { Id = map.Id, Radius = map.Radius, Terrain = map.Terrain, SampleLayout = plan };
            if (withPlan.RequestSampleLayout(s).Count > 0) return r;

            bool added = false;
            var addedTiles = new List<HexCoord>(); // 예약만 된 시설은 아직 시설 번호가 없어 칸으로 찾는다
            double staffSum = 0, waterSum = 0, idleSum = 0, mineStaffSum = 0;
            int staffN = 0, waterN = 0, hours = 0, mineN = 0;
            for (int h = 0; h < 600 && !a.IsOpen && !a.IsFinished; h++)
            {
                ReferenceBot.UnblockStone(s);
                foreach (Facility m in s.OperatingOf(BuildingKinds.Mine))
                    if (!s.HirePriority.Contains(m.Id)) s.PrioritizeHiring(m.Id); // 사람처럼 광산을 먼저 지킨다(결정 LB7)
                if (!added && (h >= QaAddBy || !s.Jobs.Any()))
                {
                    added = true;
                    foreach (string kind in arm.Builds)
                    {
                        if (kind == QaWellUpgrade)
                        {
                            foreach (Facility w in s.OperatingOf(BuildingKinds.Well).ToList()) if (!s.RequestUpgrade(w.Id).Success) r.Missing++;
                            continue;
                        }
                        HexCoord? tile = QaBuild(s, kind);
                        if (tile == null) r.Missing++;
                        else if (kind != BuildingKinds.House) addedTiles.Add(tile.Value);
                    }
                }
                QaTrade(s, arm.SellAll);
                s.AdvanceHour();
                hours++;
                idleSum += s.LastLabor.IdleResidents;
                r.LivingMin = Math.Min(r.LivingMin, s.LivingStandard);
                if (s.LivingStandard < -1e-9) { r.LivingNegHours++; if (h < 24) r.NegFirst24++; }
                if (s.LastLiving.Supply < 1 - 1e-9)
                {
                    HourRecord last = s.History[s.History.Count - 1];
                    if (last.FoodDeficit > 1e-9) r.ShortFoodHours++; else r.ShortWaterHours++;
                }
                foreach (Facility m in s.OperatingOf(BuildingKinds.Mine)) { mineStaffSum += s.StaffRatio(m); mineN++; }
                foreach (HexCoord tile in addedTiles)
                {
                    Facility f = s.FacilityAt(tile);
                    if (f == null || !f.IsOperating || s.WorkersNeeded(f) <= 0) continue;
                    staffSum += s.StaffRatio(f);
                    staffN++;
                    if ((s.LevelOf(f).WaterDemand ?? 0) > 0) { waterSum += s.WaterRateOf(f); waterN++; }
                }
            }
            if (!a.IsOpen) return r;

            AuctionEvent e = a.CurrentAuction;
            AuctionBid top = e.Bids.OrderByDescending(x => x.Cash).First();
            ValuationResult v = SettlementValuation.Evaluate(s, e.Hour);
            r.Sold = GoodsCatalog.All.Sum(g => s.EarnedFrom(g));
            r.Liquidated = s.LiquidateStock();
            r.Ok = true;
            r.Bid = top.Cash;
            r.Winner = top.Bidder.Name;
            r.Final = s.Cash + top.Cash;
            r.Living = s.LivingStandard;
            r.Target = s.LivingTarget;
            if (s.LastWater != null) r.SpareWater = Math.Max(0, s.LastWater.TotalSupply - s.LastWater.TotalReleased);
            r.UnusedSources = s.Map.Tiles.Count(t => t.Terrain == TerrainType.WaterSource && s.FacilityAt(t.Coord) == null);
            r.Ore24 = v.OreRawLast24h;
            r.FoodMult = v.FoodMultiplier;
            r.Residents = s.Residents;
            r.Idle = idleSum / Math.Max(1, hours);
            r.MineStaff = mineN > 0 ? mineStaffSum / mineN : 1;
            if (staffN > 0) r.AddedStaff = staffSum / staffN;
            if (waterN > 0) r.AddedWater = waterSum / waterN;
            return r;
        }

        /// <summary>
        /// 시설 하나를 예약하고 그 칸을 돌려준다(못 지으면 null). 물을 쓰는 시설·주택은 수로가 닿은 칸, 나머지는 아무 칸.
        /// 자리는 우물에서 가까운 칸부터(숙련 봇과 같은 규칙).
        /// </summary>
        private static HexCoord? QaBuild(Settlement s, string kind)
        {
            bool wet = kind == BuildingKinds.House || (s.Rules.Config.GetLevel(kind, 1).WaterDemand ?? 0) > 0;
            List<HexCoord> wells = s.Facilities.Where(f => f.Kind == BuildingKinds.Well).Select(w => w.Tile).ToList();
            List<HexCoord> sites = s.Map.Tiles.Where(t => s.CanBuild(kind, t.Coord).Success)
                .OrderBy(t => wells.Count == 0 ? 0 : wells.Min(w => w.DistanceTo(t.Coord))).ThenBy(t => t.Coord.Q).ThenBy(t => t.Coord.R)
                .Select(t => t.Coord).ToList();
            if (!wet) return sites.Count > 0 && s.RequestBuild(kind, sites[0]).Success ? sites[0] : (HexCoord?)null;

            // 물을 쓰면 수로가 닿은 칸. 없으면 사람처럼 수로를 1~2변 늘려 닿게 한다(가장 가까운 칸부터).
            var canalEdges = new HashSet<EdgeId>(s.Facilities.Where(f => f.Kind == BuildingKinds.CanalEdge).Select(f => f.Edge.Value)
                .Concat(s.Jobs.Where(j => j.Edge.HasValue).Select(j => j.Edge.Value)));
            var wetVertices = new HashSet<VertexId>(canalEdges.SelectMany(e => new[] { e.VertexA, e.VertexB }));
            for (int extend = 0; extend <= 2; extend++)
                foreach (HexCoord site in sites)
                {
                    List<EdgeId> path = QaCanalPath(s, HexTopology.VerticesOf(site).ToList(), wetVertices, canalEdges, extend);
                    if (path == null) continue;
                    foreach (EdgeId e in path) s.RequestCanal(e);
                    return s.RequestBuild(kind, site).Success ? site : (HexCoord?)null;
                }
            return null;
        }

        /// <summary>칸의 꼭짓점에서 물 닿은 꼭짓점까지 정확히 length변짜리 새 수로 경로(지을 수 있는 변만). 없으면 null.</summary>
        private static List<EdgeId> QaCanalPath(Settlement s, List<VertexId> from, HashSet<VertexId> wet, HashSet<EdgeId> existing, int length)
        {
            if (from.Any(wet.Contains)) return length == 0 ? new List<EdgeId>() : null;
            if (length == 0) return null;
            foreach (VertexId v in from)
                foreach (EdgeId e in s.Topology.EdgesOf(v))
                {
                    if (existing.Contains(e) || !s.CanBuildCanal(e).Success) continue;
                    VertexId next = e.VertexA.Equals(v) ? e.VertexB : e.VertexA;
                    List<EdgeId> rest = QaCanalPath(s, new List<VertexId> { next }, wet, existing, length - 1);
                    if (rest != null) { rest.Insert(0, e); return rest; }
                }
            return null;
        }

        /// <summary>
        /// 재화 처리. 팔기면 곡물 밖의 재화를 판매가가 기준의 절반 이상인 동안 매시간 판다.
        /// 두 방식 모두 창고가 80% 넘게 차면 60%까지 판다(버려지는 몫, 숙련 봇과 같은 규칙: 재화마다 10개 남김, 기준의 30% 밑이면 멈춤).
        /// </summary>
        private static void QaTrade(Settlement s, bool sellAll)
        {
            if (!s.HasMarket || s.Rules.Trade == null) return;
            EconomyConfig c = s.Rules.Config;
            if (sellAll)
                foreach (Goods g in GoodsCatalog.All.Where(x => x != Goods.Grain))
                    while (s.Stock(g) >= 1 && s.NextSellPrice(g) >= QaSellPrice(c, g) * 0.5)
                        if (s.SellGoods(g, 1) == 0) break;
            if (s.PantryUsed < s.FoodCapacity * 0.8) return;
            foreach (Goods g in GoodsCatalog.All)
            {
                double floor = QaSellPrice(c, g) * 0.3;
                while (s.PantryUsed > s.FoodCapacity * 0.6 && s.Stock(g) >= 11 && s.NextSellPrice(g) >= Math.Max(1e-9, floor))
                    if (s.SellGoods(g, 1) == 0) break;
            }
        }
    }
}
