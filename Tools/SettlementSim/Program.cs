using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DesertSettlement.Core.Canal;
using DesertSettlement.Core.Hex;
using DesertSettlement.Core.Progression;

namespace SettlementSim
{
    /// <summary>
    /// 물 배분 규칙 후보(W1~W6)와 경제 틱 규칙(C1·C2)을 비교하는 시나리오 실행기.
    /// 결과는 콘솔과 Tools/balance_output/settlement_sim_report.txt 에 남는다.
    /// </summary>
    public static partial class Program
    {
        private static readonly StringBuilder Report = new StringBuilder();
        private static int _failures;

        public static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            // dotnet run --project Tools/SettlementSim -- tune : 난이도 곡선 후보만 비교(DifficultyTuning.cs).
            if (args.Length > 0 && args[0] == "tune") return DifficultyTuning(args.Skip(1).ToArray());
            // dotnet run --project Tools/SettlementSim -- mods : 지역 모디파이어·장비 수치 측정(ModifierTuning.cs).
            if (args.Length > 0 && args[0] == "mods") return ModifierTuning(args.Skip(1).ToArray());
            // dotnet run --project Tools/SettlementSim -- exploit : 작업 목록 3번 재현·대책 비교(ExploitTuning.cs).
            if (args.Length > 0 && args[0] == "exploit") return ExploitTuning(args.Skip(1).ToArray());
            // dotnet run --project Tools/SettlementSim -- epidemic : 전염병·병원·방역 장비 측정(EpidemicTuning.cs).
            if (args.Length > 0 && args[0] == "epidemic") return EpidemicTuning(args.Skip(1).ToArray());
            if (args.Length > 0 && args[0] == "upgrades") return UpgradeTuning(args.Skip(1).ToArray());
            if (args.Length > 0 && args[0] == "bidders") return BidderTuning(args.Skip(1).ToArray());
            if (args.Length > 0 && args[0] == "savecheck") return SaveCheck(args.Skip(1).ToArray());

            EconomyRegression();
            ReleaseAndIntake();
            TiesAndPriorityCliff();
            SingleVersusMultiIntake();
            CyclesAndMultipleWells();
            CapacityMeasurement();
            RandomRobustness();
            CoreSolverCrossCheck();
            CoreEconomyCrossCheck();
            LateInvestmentWithDevelopmentAssets();
            TwoSettlementCampaign();
            ProceduralMapsReport();
            ProceduralCampaign();
            TickOrderAndQueue();

            Line();
            Line(_failures == 0 ? "모든 검증 통과" : $"검증 실패 {_failures}건");

            string outDir = Path.Combine(FindRepoRoot(), "Tools", "balance_output");
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "settlement_sim_report.txt"), Report.ToString(), new UTF8Encoding(false));
            return _failures == 0 ? 0 : 1;
        }

        // ───────────────────────── 공통 도구 ─────────────────────────

        private static void Line(string s = "")
        {
            Console.WriteLine(s);
            Report.AppendLine(s);
        }

        private static void Header(string s)
        {
            Line();
            Line("════ " + s + " ════");
        }

        private static void Check(bool ok, string what)
        {
            if (!ok) _failures++;
            Line($"  [{(ok ? "OK" : "FAIL")}] {what}");
        }

        private static string FindRepoRoot()
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null && !Directory.Exists(Path.Combine(dir, "Assets"))) dir = Path.GetDirectoryName(dir);
            return dir ?? Directory.GetCurrentDirectory();
        }

        private static readonly HexTopology Topology = new HexTopology(new HexMap(3));

        private static CanalGraph Graph(params EdgeId[] edges)
        {
            var g = new CanalGraph(Topology);
            foreach (EdgeId e in edges)
            {
                if (!g.TryInstall(e)) throw new InvalidOperationException("설치 실패/중복: " + e);
            }
            return g;
        }

        private static WellSpec Well(string name, int q, int r, double supply) => new WellSpec { Name = name, Tile = new HexCoord(q, r), Supply = supply };
        private static ConsumerSpec Use(string name, int q, int r, double demand) => new ConsumerSpec { Name = name, Tile = new HexCoord(q, r), Demand = demand };
        private static EdgeId E(int q, int r, HexDirection d) => EdgeId.Of(new HexCoord(q, r), d);

        private static string Pct(double v) => (v * 100).ToString("0.00") + "%";

        private static WaterResult SolveAndPrint(string title, CanalGraph g, IList<WellSpec> wells, IList<ConsumerSpec> users, WaterOptions o)
        {
            WaterResult r = WaterSolver.Solve(g, wells, users, o);
            string rows = string.Join("  ", users.Select(u =>
                $"{u.Name}={Pct(r.Rate[u.Name])}(d{r.Distance[u.Name]})"));
            Line($"  {title,-34} {rows}  | 방출 {r.TotalReleased:0.0}/{r.TotalSupply:0} 손실 {r.TotalLoss:0.00}");
            return r;
        }

        // 직렬 레이아웃 A: 우물 북동쪽 꼭짓점에서 바깥으로 뻗는 수로 3개 (원본 v0.8 시뮬레이터와 같은 구조).
        private static readonly EdgeId[] LayoutA = { E(1, -1, HexDirection.SouthEast), E(1, 0, HexDirection.NorthEast), E(2, -1, HexDirection.SouthEast) };
        private static readonly (string name, HexCoord tile)[] LayoutAUsers = { ("farm", new HexCoord(1, -1)), ("quarry", new HexCoord(2, -1)), ("mine", new HexCoord(2, 0)) };

        // 직렬 레이아웃 B: 첫 수로를 우물 타일의 테두리 변(동쪽)에 놓고 이어 간다.
        private static readonly EdgeId[] LayoutB = { E(0, 0, HexDirection.East), E(1, 0, HexDirection.SouthWest), E(0, 1, HexDirection.East) };
        private static readonly (string name, HexCoord tile)[] LayoutBUsers = { ("farm", new HexCoord(1, 0)), ("quarry", new HexCoord(0, 1)), ("mine", new HexCoord(1, 1)) };

        private static List<ConsumerSpec> SerialUsers((string name, HexCoord tile)[] tiles) => new List<ConsumerSpec>
        {
            new ConsumerSpec { Name = "farm", Tile = tiles[0].tile, Demand = 30 },
            new ConsumerSpec { Name = "quarry", Tile = tiles[1].tile, Demand = 25 },
            new ConsumerSpec { Name = "mine", Tile = tiles[2].tile, Demand = 50 },
        };

        /// <summary>경제 시뮬레이터용 물 모델: 지어진 구조물만 실제 헥스 수로망으로 계산.</summary>
        private static Func<EconomySim, Dictionary<string, double>> HexWater(EdgeId[] canals, (string name, HexCoord tile)[] users, WaterOptions o)
        {
            return sim =>
            {
                var g = new CanalGraph(Topology);
                var caps = new Dictionary<EdgeId, double>();
                for (int i = 0; i < 3; i++)
                {
                    string name = "canal" + (i + 1);
                    if (!sim.Active(name)) continue;
                    g.TryInstall(canals[i]);
                    caps[canals[i]] = sim.Data(name).Capacity;
                }
                var wells = new List<WellSpec>();
                if (sim.Active("well")) wells.Add(new WellSpec { Name = "well", Tile = HexCoord.Zero, Supply = sim.Data("well").WaterSupply });
                var consumers = users.Where(u => sim.Active(u.name))
                    .Select(u => new ConsumerSpec { Name = u.name, Tile = u.tile, Demand = sim.Data(u.name).WaterNeed }).ToList();
                var options = new WaterOptions
                {
                    Release = o.Release, Intake = o.Intake, Routing = o.Routing, Sources = o.Sources, Capacity = o.Capacity,
                    EdgeCapacity = e => caps.TryGetValue(e, out double c) ? c : 0,
                };
                WaterResult r = WaterSolver.Solve(g, wells, consumers, options);
                return new Dictionary<string, double>
                {
                    ["farm"] = r.Rate.GetValueOrDefault("farm"),
                    ["quarry"] = r.Rate.GetValueOrDefault("quarry"),
                    ["mine"] = r.Rate.GetValueOrDefault("mine"),
                };
            };
        }

        private static string Summary(EconomySim s, int h)
        {
            Valuation v = s.Value(h);
            double cash = s.Log[h - 1].Cash; // h시 경매 직전 = h-1시 정산 후
            string done = s.CompletedHour.TryGetValue("auction", out int a) ? a + "h" : "미완공";
            string first = s.FirstAuction.HasValue ? s.FirstAuction + "h" : "없음";
            return $"경매장 완공 {done}, 첫 경매 {first}, 경매 직전 현금 {cash:0.00}, " +
                   $"광산 물 {Pct(s.Log[h - 1].WaterMine)}, 가치 V {v.Value:0.00}, 매각 후 현금 {cash + v.Bid:0.00}";
        }

        // ───────────────────────── 시나리오 ─────────────────────────

        private static void EconomyRegression()
        {
            Header("0. 경제 시뮬레이터 이식 검증 (원본 v0.8 회귀값)");
            EconomySim baseSim = new EconomySim("base").Run(133);
            Line("  원본 직렬 공식: " + Summary(baseSim, 133));
            Check(baseSim.FirstAuction == 133 && baseSim.CompletedHour["auction"] == 37, "첫 경매 133h, 경매장 37h");
            Check(Math.Abs(baseSim.Cash - 540.05) < 0.01, "첫 경매 직전 현금 540.05");
            Check(Math.Abs(baseSim.SerialWater()["mine"] - 0.7427964) < 1e-6, "광산 물 가동률 74.28%");
            Check(Math.Abs(baseSim.Value(133).Value - 1732.80) < 0.01, "첫 경매 V 1732.80");

            var minehouse = new EconomySim("minehouse", new[] { ("mine", "upgrade", 133), ("warehouse", "build", 133) }).Run(229);
            Check(Math.Abs(minehouse.Cash + minehouse.Value(229).Bid - 3248.32) < 0.01, "보류 + 광산 L2 + 창고 → 매각 후 3248.32");

            var stonebuy = new EconomySim("stonebuy") { PurchaseStone = { [20] = 5 } }.Run(128);
            Check(stonebuy.CompletedHour["auction"] == 32 && stonebuy.FirstAuction == 128, "h20 석재 5 구매 → 경매장 32h, 첫 경매 128h");

            var hex = new EconomySim("hexA") { WaterModel = HexWater(LayoutA, LayoutAUsers, new WaterOptions()) }.Run(133);
            Line("  헥스 배분기(레이아웃 A, 꼭짓점 방출·중간점 취수): " + Summary(hex, 133));
            Check(Math.Abs(hex.Cash - baseSim.Cash) < 1e-6 && Math.Abs(hex.Value(133).Value - baseSim.Value(133).Value) < 1e-6,
                "범용 헥스 배분기가 원본 직렬 공식과 같은 결과");
        }

        private static void ReleaseAndIntake()
        {
            Header("W2·W3. 우물 방출 지점 × 시설 취수 지점 (우물 L1 100, 농장30·채석장25·광산50)");
            var wells = new[] { Well("well", 0, 0, 100) };
            foreach (var (label, edges, users) in new[] { ("레이아웃 A(꼭짓점에서 바깥으로)", LayoutA, LayoutAUsers), ("레이아웃 B(우물 테두리부터)", LayoutB, LayoutBUsers) })
            {
                Line($"  ▸ {label}");
                foreach (ReleaseMode rel in new[] { ReleaseMode.TileCorners, ReleaseMode.EdgeMidpoints })
                foreach (IntakeMode intake in new[] { IntakeMode.EdgeMidpoint, IntakeMode.TileCorner })
                {
                    SolveAndPrint($"방출={rel}, 취수={intake}", Graph(edges), wells, SerialUsers(users),
                        new WaterOptions { Release = rel, Intake = intake });
                }
            }

            // 경제 결과에 주는 영향.
            Line("  ▸ 첫 경매 매각 결과(원본 건설 순서 그대로)");
            foreach (var (label, edges, users) in new[] { ("A", LayoutA, LayoutAUsers), ("B", LayoutB, LayoutBUsers) })
            foreach (ReleaseMode rel in new[] { ReleaseMode.TileCorners, ReleaseMode.EdgeMidpoints })
            {
                var sim = new EconomySim("x") { WaterModel = HexWater(edges, users, new WaterOptions { Release = rel }) }.Run(133);
                Line($"    {label} / 방출={rel,-13}: " + Summary(sim, 133));
            }
        }

        private static void TiesAndPriorityCliff()
        {
            Header("W1. 동순위 판정과 우선순위 절벽 (우물 L1 100, 광산 50 × 2)");
            var wells = new[] { Well("well", 0, 0, 100) };
            // 우물 북동 꼭짓점과 남서 꼭짓점에서 각각 바깥으로 1구간 → 광산 두 개가 같은 거리.
            CanalGraph equal = Graph(E(1, -1, HexDirection.SouthEast), E(-1, 1, HexDirection.NorthWest));
            SolveAndPrint("같은 거리(반쪽 변 1)", equal, wells,
                new[] { Use("mineA", 1, -1, 50), Use("mineB", -1, 1, 50) }, new WaterOptions());
            // B쪽을 한 구간 더 늘리면 → 반쪽 변 3. 엄격한 상류 우선.
            CanalGraph longer = Graph(E(1, -1, HexDirection.SouthEast), E(-1, 1, HexDirection.NorthWest), E(-1, 0, HexDirection.SouthWest));
            WaterResult lr = SolveAndPrint("B만 1구간 더 멂", longer, wells,
                new[] { Use("mineA", 1, -1, 50), Use("mineB", -2, 1, 50) }, new WaterOptions());
            Line("  → 균일한 헥스에서는 모든 변 길이가 같아 '변 개수'와 '실제 길이'가 항상 같은 순서를 만든다(반쪽 변 단위 정수).");
            Line($"  → 단, 한 칸 차이로 공급 순위가 완전히 갈린다: 가까운 쪽 {Pct(lr.Rate["mineA"])}, 먼 쪽 {Pct(lr.Rate["mineB"])}.");
        }

        private static void SingleVersusMultiIntake()
        {
            Header("W4. 수로 두 개에 접한 시설: 단일 취수 vs 다중 취수 (우물 L2 140, 수로 L1 용량 100)");
            var wells = new[] { Well("well", 0, 0, 140) };
            // 가지 A: 우물 북동 꼭짓점 → 광산 두 개 → 농장. 가지 B: 우물 남동 꼭짓점 → 농장.
            EdgeId[] branchA = { E(1, -1, HexDirection.SouthEast), E(1, 0, HexDirection.NorthEast), E(2, -1, HexDirection.SouthEast) };
            EdgeId[] branchB = { E(1, 0, HexDirection.SouthWest), E(1, 0, HexDirection.SouthEast), E(1, 1, HexDirection.NorthEast) };
            var users = new[] { Use("mine1", 1, -1, 50), Use("mine2", 2, -1, 50), Use("farm", 2, 0, 30) };
            foreach (RoutingMode mode in new[] { RoutingMode.SingleIntake, RoutingMode.MultiIntake })
            {
                WaterResult r = SolveAndPrint($"{mode}", Graph(branchA.Concat(branchB).ToArray()), wells, users, new WaterOptions { Routing = mode });
                Line($"      농장 취수: {r.Intake["farm"]}, 막힘: {r.BlockedBy.GetValueOrDefault("farm", "-")}, 미사용 우물 물 {r.TotalSupply - r.TotalReleased:0.0}");
            }
            // 가지 B가 더 길 때(동률 아님): 단일 취수는 가까운 A만 쓴다.
            // (1,1) 타일을 남쪽으로 돌아가는 긴 가지 → 농장까지 반쪽 변 11.
            EdgeId[] branchBLong =
            {
                E(1, 0, HexDirection.SouthWest), E(0, 1, HexDirection.East), E(1, 1, HexDirection.SouthWest),
                E(1, 1, HexDirection.SouthEast), E(1, 1, HexDirection.East), E(1, 1, HexDirection.NorthEast),
            };
            foreach (RoutingMode mode in new[] { RoutingMode.SingleIntake, RoutingMode.MultiIntake })
            {
                WaterResult r = SolveAndPrint($"B가 더 긴 경우 {mode}", Graph(branchA.Concat(branchBLong).ToArray()), wells, users, new WaterOptions { Routing = mode });
                Line($"      농장 취수: {r.Intake["farm"]}, 막힘: {r.BlockedBy.GetValueOrDefault("farm", "-")}, 미사용 우물 물 {r.TotalSupply - r.TotalReleased:0.0}");
            }
        }

        private static void CyclesAndMultipleWells()
        {
            Header("W5. 순환(고리) 수로와 여러 우물");
            var wells = new[] { Well("well", 0, 0, 100) };
            // 농장 타일 (2,-1)을 수로 6개로 둘러싸고, 우물에서 한 구간으로 연결.
            HexCoord farmTile = new HexCoord(2, -1);
            var ring = HexTopology.EdgesOf(farmTile).ToList();
            ring.Add(E(1, -1, HexDirection.SouthEast));
            CanalGraph ringGraph = Graph(ring.Distinct().ToArray());
            WaterResult rr = SolveAndPrint("농장을 둘러싼 고리", ringGraph, wells, new[] { Use("farm", 2, -1, 30), Use("mine", 3, -1, 50) }, new WaterOptions());
            Line($"      ContainsCycle={rr.HasCycle} → 트리 전용(W5 원안)이면 이 배치는 건설 거부. 최단 경로 트리 방식이면 정상 계산.");
            int idle = ringGraph.Segments.Count(s => !rr.EdgeLoad.ContainsKey(s.Edge) || rr.EdgeLoad[s.Edge] < 1e-9);
            Line($"      고리 수로 {ringGraph.SegmentCount}개 중 물이 흐르지 않는 변 {idle}개 (최단 경로만 사용 → 용량을 더해 주지 않음)");

            // 우물 타일 자체 테두리 6변에 수로 → 우물 주변 고리.
            CanalGraph wellRing = Graph(HexTopology.EdgesOf(HexCoord.Zero).ToArray());
            WaterResult wr = SolveAndPrint("우물 테두리 고리", wellRing, wells, new[] { Use("farm", 1, 0, 30), Use("quarry", -1, 0, 25) }, new WaterOptions());
            Line($"      ContainsCycle={wr.HasCycle} — 우물 둘레에 수로를 두르는 자연스러운 배치도 순환으로 잡힌다.");

            Line("  ▸ 우물 두 개가 한 수로망에 이어진 경우 (우물1 L1 100 주변 수요 130, 우물2 L1 100은 서쪽에서 연결)");
            var twoWells = new[] { Well("well1", 0, 0, 100), Well("well2", -2, 0, 100) };
            CanalGraph linked = Graph(
                E(0, 0, HexDirection.NorthWest), E(0, 0, HexDirection.NorthEast), E(1, -1, HexDirection.SouthEast), // 우물1 북쪽 테두리 + 동쪽
                E(-1, 0, HexDirection.NorthEast), E(-1, 0, HexDirection.NorthWest));                               // 우물2 → 우물1 연결
            var users = new[] { Use("mine1", 0, -1, 50), Use("mine2", 1, -1, 50), Use("farm", 1, 0, 30) };
            foreach (SourceMode s in new[] { SourceMode.NearestWellOnly, SourceMode.AllWells })
            {
                WaterResult r = SolveAndPrint($"{s}", linked, twoWells, users, new WaterOptions { Sources = s, Routing = RoutingMode.MultiIntake });
                Line($"      우물별 방출: {string.Join(", ", r.WellReleased.Select(kv => $"{kv.Key} {kv.Value:0.0}"))}");
            }
        }

        private static void CapacityMeasurement()
        {
            Header("W6. 수로 용량 측정 위치 (우물 L2 140, 수로 L1 100, 광산 50 ×3)");
            var wells = new[] { Well("well", 0, 0, 140) };
            CanalGraph oneOutlet = Graph(LayoutA);
            var users = new[] { Use("mine1", 1, -1, 50), Use("mine2", 2, -1, 50), Use("mine3", 2, 0, 50) };
            foreach (CapacityMode cm in new[] { CapacityMode.Entry, CapacityMode.Exit })
            {
                WaterResult r = SolveAndPrint($"출구 1개, 용량={cm}", oneOutlet, wells, users, new WaterOptions { Capacity = cm });
                Line($"      첫 변 부하 {r.EdgeLoad[LayoutA[0]]:0.00}, 우물 미사용 {r.TotalSupply - r.TotalReleased:0.0}");
            }
            CanalGraph twoOutlets = Graph(LayoutA.Concat(new[] { E(1, 0, HexDirection.SouthWest), E(1, 0, HexDirection.SouthEast) }).ToArray());
            SolveAndPrint("출구 2개, 용량=Entry", twoOutlets, wells, users.Append(Use("farm", 1, 1, 30)).ToArray(), new WaterOptions());
            CanalGraph l2 = Graph(LayoutA);
            SolveAndPrint("출구 1개를 L2(150)로", l2, wells, users,
                new WaterOptions { EdgeCapacity = e => e == LayoutA[0] ? 150 : 100 });
        }

        private static void RandomRobustness()
        {
            Header("검증. 무작위 트리 수로망 2,000개 — 물 보존·용량·우선순위·결정성");
            var rng = new Random(20261022);
            int conservation = 0, capacity = 0, demand = 0, priority = 0, determinism = 0, runs = 0;
            int multiBetter = 0, multiWorse = 0;
            for (int k = 0; k < 2000; k++)
            {
                HexCoord wellTile = RandomTile(rng);
                var installed = RandomTree(rng, wellTile, rng.Next(4, 18));
                if (installed.Count == 0) continue;
                var touching = installed.SelectMany(e => Topology.TilesOf(e)).Distinct().Where(t => t != wellTile).ToList();
                if (touching.Count == 0) continue;
                var users = touching.OrderBy(_ => rng.Next()).Take(rng.Next(1, Math.Min(7, touching.Count + 1)))
                    .Select((t, i) => new ConsumerSpec { Name = "c" + i, Tile = t, Demand = new[] { 25, 30, 50 }[rng.Next(3)] }).ToList();
                var wells = new[] { new WellSpec { Name = "w", Tile = wellTile, Supply = new[] { 100, 140 }[rng.Next(2)] } };
                var o = new WaterOptions();
                WaterResult r = WaterSolver.Solve(Graph(installed.ToArray()), wells, users, o);
                runs++;

                if (r.TotalReleased <= r.TotalSupply + 1e-6 && r.TotalDelivered <= r.TotalReleased + 1e-6) conservation++;
                if (r.EdgeLoad.All(kv => kv.Value <= 100 + 1e-6)) capacity++;
                if (users.All(u => r.Delivered[u.Name] <= u.Demand + 1e-6)) demand++;
                // 우선순위: 미충족 시설은 반드시 어떤 제약(변/우물)에 막혀 있어야 하고,
                // 우물에 막혔다면 더 먼 시설은 물을 받지 못해야 한다.
                bool ok = true;
                foreach (ConsumerSpec u in users.Where(u => r.Rate[u.Name] < 1 - 1e-9 && r.Distance[u.Name] >= 0))
                {
                    if (!r.BlockedBy.TryGetValue(u.Name, out string why)) { ok = false; break; }
                    if (why.StartsWith("well") && users.Any(v => r.Distance[v.Name] > r.Distance[u.Name] && r.Delivered[v.Name] > 1e-9)) { ok = false; break; }
                }
                if (ok) priority++;

                // 결정성: 설치·시설 순서를 섞어도 같은 결과.
                var shuffled = Graph(installed.OrderBy(_ => rng.Next()).ToArray());
                WaterResult r2 = WaterSolver.Solve(shuffled, wells, users.OrderBy(_ => rng.Next()).ToList(), o);
                if (users.All(u => Math.Abs(r.Delivered[u.Name] - r2.Delivered[u.Name]) < 1e-9)) determinism++;

                WaterResult multi = WaterSolver.Solve(Graph(installed.ToArray()), wells, users, new WaterOptions { Routing = RoutingMode.MultiIntake });
                if (multi.TotalDelivered > r.TotalDelivered + 1e-6) multiBetter++;
                if (multi.TotalDelivered < r.TotalDelivered - 1e-6) multiWorse++;
            }
            Check(conservation == runs, $"물 보존(방출 ≤ 생산, 전달 ≤ 방출) {conservation}/{runs}");
            Check(capacity == runs, $"모든 변 부하 ≤ 용량 {capacity}/{runs}");
            Check(demand == runs, $"전달 ≤ 수요 {demand}/{runs}");
            Check(priority == runs, $"상류 우선 불변식 {priority}/{runs}");
            Check(determinism == runs, $"설치·시설 순서와 무관한 결정적 결과 {determinism}/{runs}");
            Line($"  다중 취수가 총 전달량을 늘린 경우 {multiBetter}/{runs}, 줄인 경우 {multiWorse}/{runs}");
        }

        /// <summary>Core의 WaterNetworkSolver(확정 규칙)가 프로토타입의 같은 옵션 조합과 같은 결과를 내는지.</summary>
        private static void CoreSolverCrossCheck()
        {
            Header("Core WaterNetworkSolver ↔ 프로토타입(확정 규칙 조합) 교차 검증");
            var confirmed = new WaterOptions
            {
                Release = ReleaseMode.TileCorners, Intake = IntakeMode.TileCorner, Routing = RoutingMode.MultiIntake,
                Sources = SourceMode.AllWells, Capacity = CapacityMode.Entry,
            };
            var rng = new Random(7);
            int runs = 0, same = 0;
            double worst = 0;
            for (int k = 0; k < 3000; k++)
            {
                // 우물 1~2개, 순환이 생길 수 있도록 트리 2개를 겹친다.
                int wellCount = rng.Next(1, 3);
                var wellTiles = Enumerable.Range(0, wellCount).Select(_ => RandomTile(rng)).Distinct().ToList();
                var edges = wellTiles.SelectMany(t => RandomTree(rng, t, rng.Next(3, 14))).Distinct().ToList();
                if (rng.Next(2) == 0) edges = edges.Concat(RandomTree(rng, wellTiles[0], rng.Next(3, 10))).Distinct().ToList();
                var touching = edges.SelectMany(e => Topology.TilesOf(e)).Distinct().Where(t => !wellTiles.Contains(t)).ToList();
                if (touching.Count == 0) continue;
                var users = touching.OrderBy(_ => rng.Next()).Take(rng.Next(1, Math.Min(8, touching.Count + 1)))
                    .Select((t, i) => new ConsumerSpec { Name = "c" + i, Tile = t, Demand = new[] { 25, 30, 35, 50 }[rng.Next(4)] }).ToList();
                var wells = wellTiles.Select((t, i) => new WellSpec { Name = "w" + i, Tile = t, Supply = new[] { 100, 140 }[rng.Next(2)] }).ToList();
                CanalGraph g = Graph(edges.ToArray());
                var levels = edges.Where(_ => rng.Next(4) == 0).ToHashSet();
                foreach (EdgeId e in levels) g.TrySetLevel(e, 2);
                confirmed.EdgeCapacity = e => levels.Contains(e) ? 150 : 100;

                WaterResult proto = WaterSolver.Solve(g, wells, users, confirmed);
                var core = DesertSettlement.Core.Water.WaterNetworkSolver.Solve(g,
                    wells.Select(w => new DesertSettlement.Core.Water.WaterSource(w.Name, w.Tile, w.Supply)).ToList(),
                    users.Select(u => new DesertSettlement.Core.Water.WaterConsumer(u.Name, u.Tile, u.Demand)).ToList(),
                    new DesertSettlement.Core.Water.WaterNetworkSettings { CapacityOf = s => s.Level >= 2 ? 150 : 100 });
                runs++;
                double diff = users.Max(u => Math.Abs(proto.Delivered[u.Name] - core.Consumers[u.Name].Delivered));
                worst = Math.Max(worst, diff);
                if (diff < 1e-7 && Math.Abs(proto.TotalReleased - core.TotalReleased) < 1e-7) same++;
            }
            Check(same == runs, $"무작위 수로망(우물 1~2개, 순환 포함, L1/L2 혼합) {same}/{runs} 일치, 최대 차이 {worst:E1}");
        }

        /// <summary>Core Settlement(경제 틱)가 v0.8 이식본과 시간별로 같은 결과를 내는지 여러 전략으로 비교.</summary>
        private static void CoreEconomyCrossCheck()
        {
            Header("Core Settlement ↔ v0.8 이식본 교차 검증 (시간별 현금·재고·F·광물, 경매 입찰가)");
            string json = File.ReadAllText(Path.Combine(FindRepoRoot(), "Assets", "Data", "Config", "economy_balance_v0.8_config.json"));
            var config = System.Text.Json.JsonSerializer.Deserialize<DesertSettlement.Core.Config.EconomyConfig>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var scenarios = new (string label, int until, (string, string, int)[] actions, Dictionary<int, int> stone, Dictionary<int, int> food)[]
            {
                ("첫 경매 즉시 매각", 133, Array.Empty<(string, string, int)>(), null, null),
                ("보류(투자 없음)", 229, Array.Empty<(string, string, int)>(), null, null),
                ("보류 + 광산 L2", 229, new[] { ("mine", "upgrade", 133) }, null, null),
                ("보류 + 광산 L2 + 창고", 229, new[] { ("mine", "upgrade", 133), ("warehouse", "build", 133) }, null, null),
                ("보류 + 농장 L2", 229, new[] { ("farm", "upgrade", 133) }, null, null),
                ("보류 + 채석장 L2", 229, new[] { ("quarry", "upgrade", 133) }, null, null),
                ("보류 + 우물·수로1 L2", 229, new[] { ("well", "upgrade", 133), ("canal1", "upgrade", 133) }, null, null),
                ("마지막 24h 채석장 폐쇄", 133, new[] { ("quarry", "close", 109) }, null, null),
                ("h20 석재 5 구매", 128, Array.Empty<(string, string, int)>(), new Dictionary<int, int> { [20] = 5 }, null),
                ("h95 식량 30 구매", 133, Array.Empty<(string, string, int)>(), null, new Dictionary<int, int> { [95] = 30 }),
            };

            // 확정 물 규칙(꼭짓점 방출·꼭짓점 취수·다중 경로·모든 우물)으로 계산하는 이식본과 비교한다.
            var confirmedWater = new WaterOptions
            {
                Release = ReleaseMode.TileCorners, Intake = IntakeMode.TileCorner, Routing = RoutingMode.MultiIntake, Sources = SourceMode.AllWells,
            };
            Line("  (물 규칙: 꼭짓점 방출 + 꼭짓점 취수 — 원본 v0.8의 변 중간점 취수와 수치가 다르다)");
            foreach (var sc in scenarios)
            {
                var port = new EconomySim(sc.label, sc.actions) { WaterModel = HexWater(LayoutA, LayoutAUsers, confirmedWater), CountDiscardedInValuation = true, OreValueFactor = config.AuctionSeed.OreProduction24hValueFactor };
                if (sc.stone != null) foreach (var kv in sc.stone) port.PurchaseStone[kv.Key] = kv.Value;
                if (sc.food != null) foreach (var kv in sc.food) port.PurchaseFood[kv.Key] = kv.Value;
                port.Run(sc.until);

                var core = RunCoreScript(config, sc.actions, sc.stone, sc.food, sc.until);
                double worst = 0;
                for (int h = 0; h < sc.until; h++)
                {
                    var a = port.Log[h];
                    var b = core.History[h];
                    worst = Math.Max(worst, new[]
                    {
                        Math.Abs(a.Cash - b.Cash), Math.Abs(a.Food - b.Food), Math.Abs(a.Stone - b.Stone),
                        Math.Abs(a.F - b.FoodCoefficient), Math.Abs(a.Ore - b.OreProduced),
                    }.Max());
                }
                double portBid = port.Log[sc.until - 1].Cash + port.Value(sc.until).Bid;
                double coreBid = core.History[sc.until - 1].Cash + DesertSettlement.Core.Economy.SettlementValuation.Evaluate(core, sc.until).Bid(1.05);
                Check(worst < 1e-6 && Math.Abs(portBid - coreBid) < 1e-6,
                    $"{sc.label,-22} 매각 후 현금 이식본 {portBid:0.00} / Core {coreBid:0.00}, 시간별 최대 차이 {worst:E1}");
                var v = DesertSettlement.Core.Economy.SettlementValuation.Evaluate(core, sc.until);
                Line($"      경매장 {core.AuctionHallCompletedHour}h, 경매 직전 현금 {core.History[sc.until - 1].Cash:0.00}, " +
                     $"광산 물 {Pct(port.Log[sc.until - 1].WaterMine)}, V {v.Value:0.00}, 식량 {core.Food:0.00}, 석재 {core.Stone:0.00}, 폐기 석재 {core.StoneDiscarded:0.00}, F {core.FoodCoefficient:0.0000}");
            }
        }

        private static DesertSettlement.Core.Config.EconomyConfig LoadConfig()
        {
            string json = File.ReadAllText(Path.Combine(FindRepoRoot(), "Assets", "Data", "Config", "economy_balance_v0.8_config.json"));
            return System.Text.Json.JsonSerializer.Deserialize<DesertSettlement.Core.Config.EconomyConfig>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        /// <summary>Assets/Data/Config/procedural_map_config.json(게임과 같은 절차 맵 수치).</summary>
        private static ProceduralMapConfig LoadProceduralConfig()
        {
            string json = File.ReadAllText(Path.Combine(FindRepoRoot(), "Assets", "Data", "Config", "procedural_map_config.json"));
            var config = System.Text.Json.JsonSerializer.Deserialize<ProceduralMapConfig>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } });
            var problems = config.Validate();
            if (problems.Count > 0) throw new InvalidOperationException("절차 맵 설정 오류: " + string.Join("; ", problems));
            return config;
        }

        private static List<DesertSettlement.Core.Progression.MapDefinition> LoadMaps()
        {
            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return Directory.GetFiles(Path.Combine(FindRepoRoot(), "Assets", "Data", "Maps"), "*.json").OrderBy(p => p, StringComparer.Ordinal)
                .Select(p => System.Text.Json.JsonSerializer.Deserialize<DesertSettlement.Core.Progression.MapDefinition>(File.ReadAllText(p), options))
                .ToList();
        }

        /// <summary>A3 재검토: 개발 중 자산 가치를 넣으면 경매 직전 개량이 여전히 손해인지.</summary>
        private static void LateInvestmentWithDevelopmentAssets()
        {
            Header("A3. 늦은 투자: 광산 L2 개량 시작 시각 → 두 번째 경매(229h) 가치 (폐기분 포함 배율)");
            var config = LoadConfig();
            var bidders = config.Auction.Bidders;
            Line("  개량 시작 | V(개발 중 자산 없음) | V(개발 중 자산 50%) | 산업가 | 농장주");
            foreach (int start in new[] { -1, 133, 170, 190, 200, 205, 210, 215, 220 })
            {
                double[] values = new double[4];
                for (int mode = 0; mode < 2; mode++)
                {
                    var actions = start < 0 ? Array.Empty<(string, string, int)>() : new[] { ("mine", "upgrade", start) };
                    var s = RunCoreScript(config, actions, null, null, 229, mode == 0 ? 0.0 : (double?)null);
                    values[mode] = DesertSettlement.Core.Economy.SettlementValuation.Evaluate(s, 229).Value;
                    if (mode == 1)
                    {
                        values[2] = DesertSettlement.Core.Economy.SettlementValuation.EvaluateFor(s, 229, bidders[0]).Value;
                        values[3] = DesertSettlement.Core.Economy.SettlementValuation.EvaluateFor(s, 229, bidders[1]).Value;
                    }
                }
                Line($"  {(start < 0 ? "안 함" : start + "h"),8} | {values[0],10:0.0} | {values[1],10:0.0} | {values[2],8:0.0} | {values[3],8:0.0}");
            }
        }

        /// <summary>두 고정 맵을 샘플 배치로 연속 매각하는 캠페인(MVP 루프 검증).</summary>
        private static void TwoSettlementCampaign()
        {
            Header("4단계. 두 정착지 연속 매각 (고정 맵 2종, 샘플 배치, 매번 현금 최대 입찰 수락)");
            var config = LoadConfig();
            var maps = LoadMaps();
            var campaign = new DesertSettlement.Core.Progression.Campaign(config, maps);
            for (int n = 0; n < 2; n++)
            {
                var s = campaign.StartNextSettlement();
                var failures = campaign.CurrentMap.RequestSampleLayout(s);
                Check(failures.Count == 0, $"{campaign.CurrentMap.Name}: 샘플 배치 예약 {(failures.Count == 0 ? "성공" : string.Join("; ", failures))}");
                Line($"    시작 자금 {s.Cash:0.0}, 식량 {s.Food:0.0}, 우물 배율 {s.Rules.WellSupplyMultiplier:0.00}");
                var auctions = campaign.Auctions;
                int guard = 0;
                while (!auctions.IsOpen && !auctions.IsFinished && guard++ < 1000) s.AdvanceHour();
                if (!auctions.IsOpen)
                {
                    Check(false, $"{campaign.CurrentMap.Name}: 경매가 열리지 않음 ({auctions.Phase}, {auctions.BankruptcyReason})");
                    return;
                }
                var a = auctions.CurrentAuction;
                Line($"    경매장 {s.AuctionHallCompletedHour}h 완공 → {a.Kind} 경매 {a.Hour}h, 시세 {a.Market:0.000}, 직전 현금 {s.Cash:0.0}");
                foreach (var bid in a.Bids)
                {
                    string perk = string.Join(", ", bid.NextSettlementFreeBuilds.Where(kv => kv.Value > 0).Select(kv => $"완공 {kv.Key} {kv.Value}개"));
                    Line($"      {bid.Bidder.Name}: 총액 {bid.TotalOffer:0.0} (현금 {bid.Cash:0.0}{(perk.Length > 0 ? " + 다음 정착지 " + perk : "")}) " +
                         $"V {bid.Valuation.Value:0.0} [광물24h {bid.Valuation.OreLast24h:0.0}, 식량 ×{bid.Valuation.FoodMultiplier:0.00}, 석재 ×{bid.Valuation.StoneMultiplier:0.00}, 인프라 ×{bid.Valuation.InfraMultiplier:0.00}]");
                }
                int best = a.Bids.Select((b, i) => (b, i)).OrderByDescending(x => x.b.Cash).First().i;
                auctions.Sell(best);
                Line($"    → {a.Bids[best].Bidder.Name}에게 매각, 캠페인 자금 {campaign.Cash:0.0}");
                if (n == 0)
                {
                    var buy = campaign.BuyEquipment("wellPump");
                    Line($"    장비 '우물 성능 향상' 구매: {(buy.Success ? "성공" : buy.Message)}, 남은 자금 {campaign.Cash:0.0}");
                }
            }
            Check(campaign.Sales.Count == 2, $"두 정착지 연속 매각 완료 (최종 자금 {campaign.Cash:0.0})");
        }

        /// <summary>절차 맵: 난이도별로 맵을 여러 장 만들어 규칙 준수와 기준 봇 결과를 표로 낸다.</summary>
        private static void ProceduralMapsReport()
        {
            Header("절차 맵 생성 (BSP) — 난이도별 40장, 기준 봇(시작 자금 2000 고정)으로 첫 경매까지");
            var config = LoadConfig();
            var proc = LoadProceduralConfig();
            var rules = proc.Generator;
            string outDir = Path.Combine(FindRepoRoot(), "Tools", "balance_output", "generated_maps");
            Directory.CreateDirectory(outDir);
            foreach (string old in Directory.GetFiles(outDir, "*.json")) File.Delete(old);

            Line("  D    | 생성 성공 | 평균 시도 | 산맥/고원 | 독립 장애물 | 광맥 장애물옆 | 암반 장애물속 | 우물–광맥 거리 | 수로 경로 | 봇 경매 성공 | 파산 | 경매 시각 | 기준 V | 매각 후 순이익 ‖ 다중 봇: 우물/광산 | 경매 성공 | 파산 | 경매 시각 | 기준 V | 매각 후 순이익");
            int violations = 0;
            foreach (double d in new[] { 0.0, 0.25, 0.5, 0.75, 1.0, 1.5, 2.0, 3.0, 4.0, 5.0 })
            {
                DifficultyParams p = DifficultyParams.For(d, proc.Difficulty);
                var maps = new List<GeneratedMap>();
                for (int i = 0; i < 40; i++)
                {
                    GeneratedMap g = ProceduralMapGenerator.Generate(p, rules, 10000 + (int)(d * 1000) + i);
                    if (g != null) maps.Add(g);
                }
                violations += maps.Sum(CountRuleViolations);

                var bots = maps.Select(g => ReferenceBot.Run(config, g.Map, r => r.StartingCash = 2000, seed: g.Seed)).ToList();
                var multi = maps.Select(g => ReferenceBot.Run(config, g.Map, r => r.StartingCash = 2000, seed: g.Seed, strategy: BotStrategy.Multi)).ToList();
                var multiOk = multi.Where(b => b.AuctionHour >= 0).ToList();
                var ok = bots.Where(b => b.AuctionHour >= 0).ToList();
                int mountains = maps.Sum(g => g.Formations.Count(f => f.kind == FormationKind.Mountain));
                int plateaus = maps.Sum(g => g.Formations.Count(f => f.kind == FormationKind.Plateau));
                double orNear = maps.Sum(g => g.OresNearObstacle) / (double)Math.Max(1, maps.Sum(g => g.OreTotal));
                double rockIn = maps.Sum(g => g.BedrocksInObstacle) / (double)Math.Max(1, maps.Sum(g => g.BedrockTotal));
                string Avg(IEnumerable<double> xs) => xs.Any() ? xs.Average().ToString("0") : "-";
                string Median(IEnumerable<double> xs)
                {
                    var list = xs.OrderBy(x => x).ToList();
                    return list.Count == 0 ? "-" : list[list.Count / 2].ToString("0");
                }
                Line($"  {d:0.00} | {maps.Count,2}/40 | {maps.Average(g => g.Attempts),5:0.0} | {mountains,3}/{plateaus,-3} | {maps.Count(g => g.IsolatedObstacle.HasValue),2}장 | {orNear,6:P0} | {rockIn,6:P0} | " +
                     $"{maps.Average(g => g.NearestWellOreDistance),4:0.0} | {maps.Average(g => g.ShortestWellOreCanalPath),4:0.0}변 | {ok.Count,2}/{bots.Count} | {bots.Count(b => b.Bankrupt),2} | " +
                     $"{Avg(ok.Select(b => (double)b.AuctionHour))}h | {Avg(ok.Select(b => b.NeutralValue))} | {Median(ok.Select(b => b.FinalCash - 2000))} ‖ " +
                     $"{multi.Average(b => b.WellCount):0.0}/{multi.Average(b => b.MineCount):0.0} | {multiOk.Count,2}/{multi.Count} | {multi.Count(b => b.Bankrupt),2} | " +
                     $"{Avg(multiOk.Select(b => (double)b.AuctionHour))}h | {Avg(multiOk.Select(b => b.NeutralValue))} | {Median(multiOk.Select(b => b.FinalCash - 2000))}");
                Line($"         {p}");

                // 예시: 난이도마다 2장을 저장(맵 스케치에서 'JSON 불러오기'로 볼 수 있음, sampleLayout = 다중 봇 배치).
                foreach (GeneratedMap g in maps.Take(2))
                {
                    var def = g.Map;
                    var plan = ReferenceBot.Plan(def, BotStrategy.Multi);
                    var copy = new DesertSettlement.Core.Progression.MapDefinition
                    {
                        Id = def.Id, Name = def.Name, Description = def.Description + " / 다중 봇 배치 포함", Radius = def.Radius,
                        Terrain = def.Terrain, SampleLayout = plan ?? new List<DesertSettlement.Core.Progression.MapBuildEntry>(),
                    };
                    var json = System.Text.Json.JsonSerializer.Serialize(copy, new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    });
                    File.WriteAllText(Path.Combine(outDir, def.Id + ".json"), json, new UTF8Encoding(false));
                }
            }
            // Unity EditMode 테스트(ProceduralMapTests.Generate_MatchesSimulatorGoldenMap)와 같은 맵이 나오는지 확인하는 서명.
            Line($"  기준 맵 서명(D 0.50, 시드 42): {GoldenSignature(proc)}");
            Check(violations == 0, $"생성된 모든 맵이 규칙 준수(지형끼리 붙지 않음, 트인 땅 연결, 우물 간격, 수원·광맥·암반 존재) — 위반 {violations}");
            Line($"  예시 맵 JSON: Tools/balance_output/generated_maps/ (맵 스케치 씬 → 'JSON 불러오기')");
        }

        internal static string GoldenSignature(ProceduralMapConfig proc)
        {
            GeneratedMap g = ProceduralMapGenerator.Generate(DifficultyParams.For(0.5, proc.Difficulty), proc.Generator, 42);
            return g == null ? "생성 실패" : $"r{g.Map.Radius} " + string.Join(" ", g.Map.Terrain.Select(t => $"{t.Q},{t.R}{t.Type.Substring(0, 2)}"));
        }

        /// <summary>생성된 맵이 규칙을 어긴 횟수(0이어야 한다).</summary>
        private static int CountRuleViolations(GeneratedMap g)
        {
            int v = 0;
            var hex = g.Map.CreateMap();
            var owner = new Dictionary<HexCoord, int>();
            for (int i = 0; i < g.Formations.Count; i++) foreach (var c in g.Formations[i].tiles) owner[c] = i;
            foreach (var kv in owner)
            {
                if (ProceduralMapGenerator.Neighbors(kv.Key).Any(n => owner.TryGetValue(n, out int o) && o != kv.Value)) v++; // 지형끼리 붙음
            }
            var open = hex.Tiles.Where(t => t.Terrain != TerrainType.Obstacle).Select(t => t.Coord).ToList();
            var seen = new HashSet<HexCoord> { open[0] };
            var q = new Queue<HexCoord>(seen);
            while (q.Count > 0)
                foreach (var n in ProceduralMapGenerator.Neighbors(q.Dequeue()))
                    if (hex.Contains(n) && hex.GetTile(n).Terrain != TerrainType.Obstacle && seen.Add(n)) q.Enqueue(n);
            if (seen.Count != open.Count) v++; // 트인 땅 끊김
            var wells = hex.Tiles.Where(t => t.Terrain == TerrainType.WaterSource).Select(t => t.Coord).ToList();
            for (int i = 0; i < wells.Count; i++)
                for (int j = i + 1; j < wells.Count; j++)
                    if (wells[i].DistanceTo(wells[j]) <= 2) v++; // 우물 간격
            if (wells.Count == 0 || !hex.Tiles.Any(t => t.Terrain == TerrainType.OreVein) || !hex.Tiles.Any(t => t.Terrain == TerrainType.Bedrock)) v++;
            if (wells.Count != g.Params.WellCount || hex.Tiles.Count(t => t.Terrain == TerrainType.OreVein) != g.Params.OreCount) v++;
            return v;
        }

        /// <summary>첫 정착지 고정 맵 1, 두 번째부터 보유 자금 비례 난이도의 생성 맵으로 캠페인을 이어 본다.</summary>
        private static void ProceduralCampaign()
        {
            Header("절차 맵 캠페인 — 정착지 1 = 고정 맵 1, 이후 자금 비례 난이도 생성 맵 (기준 봇, 첫 경매에서 현금 최대 입찰 매각)");
            var config = LoadConfig();
            var map1 = LoadMaps()[0];
            var proc = LoadProceduralConfig();
            var rules = proc.Generator;
            foreach (BotStrategy strategy in new[] { BotStrategy.Single, BotStrategy.Multi })
            foreach (int campaignSeed in new[] { 1, 2, 3 })
            {
                double cash = config.Prototype.StartingCash;
                double wellMultiplier = 1.0;
                var line = new StringBuilder($"  {(strategy == BotStrategy.Single ? "단일" : "다중")} 봇 캠페인 {campaignSeed}: ");
                for (int n = 1; n <= 6; n++)
                {
                    var def = map1;
                    string label = "고정1";
                    DifficultyParams p = null;
                    if (n > 1)
                    {
                        p = DifficultyParams.For(DifficultyParams.DifficultyFromProgress(cash, n - 2, proc.Difficulty), proc.Difficulty);
                        var g = ProceduralMapGenerator.Generate(p, rules, campaignSeed * 100 + n);
                        if (g == null) { line.Append($"| #{n} 생성 실패 "); break; }
                        def = g.Map;
                        label = $"D{p.D:0.00}";
                    }
                    double startCash = cash, wm = wellMultiplier;
                    var r = ReferenceBot.Run(config, def, x => { x.StartingCash = startCash; x.WellSupplyMultiplier = wm; }, seed: campaignSeed * 31 + n, strategy: strategy);
                    if (r.AuctionHour < 0)
                    {
                        line.Append($"| #{n} {label} {r.Failure} ");
                        break;
                    }
                    cash = r.FinalCash;
                    if (n == 1 && cash >= 400 + 1000) { cash -= 400; wellMultiplier = 1.1; } // 첫 매각 후 우물 장비 구매(+10%)
                    line.Append($"| #{n} {label} {r.AuctionHour}h → {cash:0} ");
                }
                Line(line.ToString());
            }
        }

        private static DesertSettlement.Core.Economy.Settlement RunCoreScript(DesertSettlement.Core.Config.EconomyConfig config,
            (string name, string action, int time)[] extra, Dictionary<int, int> stoneBuys, Dictionary<int, int> foodBuys, int until,
            double? developmentRatio = 0.0, bool currentRules = false)
        {
            var map = new HexMap(3);
            map.GetTile(HexCoord.Zero).Terrain = TerrainType.WaterSource;
            map.GetTile(new HexCoord(2, -1)).Terrain = TerrainType.Bedrock;
            map.GetTile(new HexCoord(2, 0)).Terrain = TerrainType.OreVein;
            var rules = DesertSettlement.Core.Economy.SettlementRules.FromConfig(config);
            if (developmentRatio.HasValue) rules.DevelopmentAssetRatio = developmentRatio.Value; // 이식본엔 개발 중 자산이 없으므로 비교 시 0
            if (!currentRules)
            {
                rules.MaturityStartRatio = 1; // 이식본(v0.8)엔 가동 숙성·채석장 능력 규칙이 없으므로 비교 때는 끈다
                rules.StoneMultiplierUsesQuarryCapacity = false;
            }
            var s = new DesertSettlement.Core.Economy.Settlement(map, rules);

            var tiles = new Dictionary<string, HexCoord>
            {
                ["well"] = HexCoord.Zero, ["farm"] = new HexCoord(1, -1), ["quarry"] = new HexCoord(2, -1), ["mine"] = new HexCoord(2, 0),
                ["auction"] = new HexCoord(-1, 0), ["warehouse"] = new HexCoord(0, 1),
            };
            var kinds = new Dictionary<string, string>
            {
                ["well"] = "well", ["farm"] = "farm", ["quarry"] = "quarry", ["mine"] = "mine", ["auction"] = "auctionHall", ["warehouse"] = "warehouse",
            };
            var canals = new Dictionary<string, EdgeId> { ["canal1"] = LayoutA[0], ["canal2"] = LayoutA[1], ["canal3"] = LayoutA[2] };
            var plan = new List<(string name, string action, int time)>
            {
                ("well", "build", 0), ("farm", "build", 0), ("canal1", "build", 6), ("canal2", "build", 8),
                ("quarry", "build", 10), ("mine", "build", 10), ("canal3", "build", 18), ("auction", "build", 20),
            };
            plan.AddRange(extra);

            DesertSettlement.Core.Economy.Facility Find(string name) => canals.TryGetValue(name, out EdgeId e)
                ? s.FacilityOn(e, "canalEdge")
                : s.FacilityAt(tiles[name]);

            for (int h = 0; h < until; h++)
            {
                foreach (var p in plan.Where(p => p.time == h))
                {
                    DesertSettlement.Core.Economy.CommandResult r = p.action switch
                    {
                        "build" when canals.ContainsKey(p.name) => s.RequestCanal(canals[p.name]),
                        "build" => s.RequestBuild(kinds[p.name], tiles[p.name]),
                        "upgrade" => s.RequestUpgrade(Find(p.name).Id),
                        "close" => s.SetClosed(Find(p.name).Id, true),
                        "open" => s.SetClosed(Find(p.name).Id, false),
                        _ => throw new InvalidOperationException(p.action),
                    };
                    if (!r.Success) throw new InvalidOperationException($"h{h} {p.name} {p.action}: {r.Message}");
                }
                // 이식본은 시간대 안에서 '착공 전 석재 구매 → 착공 → 식량 구매' 순서다.
                if (stoneBuys != null && stoneBuys.TryGetValue(h, out int st)) s.BuyStone(st);
                if (foodBuys != null && foodBuys.TryGetValue(h, out int fd)) s.BuyFood(fd);
                s.AdvanceHour();
            }
            return s;
        }

        private static HexCoord RandomTile(Random rng)
        {
            var tiles = Topology.Map.Tiles.Select(t => t.Coord).ToList();
            return tiles[rng.Next(tiles.Count)];
        }

        // 우물 꼭짓점에서 시작해 순환 없이 자라는 무작위 수로 트리.
        private static List<EdgeId> RandomTree(Random rng, HexCoord well, int size)
        {
            var visited = new HashSet<VertexId>(HexTopology.VerticesOf(well).Where(Topology.Contains));
            var frontier = visited.ToList();
            var edges = new List<EdgeId>();
            for (int i = 0; i < size * 4 && edges.Count < size; i++)
            {
                VertexId from = frontier[rng.Next(frontier.Count)];
                var options = Topology.EdgesOf(from).Where(e => !edges.Contains(e)).ToList();
                if (options.Count == 0) continue;
                EdgeId pick = options[rng.Next(options.Count)];
                VertexId other = pick.VertexA == from ? pick.VertexB : pick.VertexA;
                if (visited.Contains(other)) continue; // 순환 방지(우물 꼭짓점끼리도)
                edges.Add(pick);
                visited.Add(other);
                frontier.Add(other);
            }
            return edges;
        }

        private static void TickOrderAndQueue()
        {
            Header("C1. 경제 틱 순서: v0.8(시작 시 완공) vs 기획서 4.2(끝에 완공)");
            EconomySim v08 = new EconomySim("v08").Run(229);
            EconomySim cp = new EconomySim("cp") { Order = TickOrder.CheckpointCompleteAtEnd }.Run(229);
            bool same = v08.Log.Zip(cp.Log).All(p => Math.Abs(p.First.Cash - p.Second.Cash) < 1e-9 && Math.Abs(p.First.Ore - p.Second.Ore) < 1e-9);
            Line("  v0.8 : " + Summary(v08, 133));
            Line("  4.2  : " + Summary(cp, 133));
            Check(same, "두 순서의 229시간 전체 기록이 동일 (완공 시점 해석이 같음)");

            Header("C2. 석재 부족 작업 처리: FIFO 대기(v0.8 시뮬레이터) vs 건너뛰기(기획서 잠정)");
            var variants = new (string label, (string, string, int)[] actions)[]
            {
                ("기본 건설 순서", Array.Empty<(string, string, int)>()),
                ("h21에 창고 예약 추가", new[] { ("warehouse", "build", 21) }),
                ("h21에 창고 + h22 농장 L2 예약", new[] { ("warehouse", "build", 21), ("farm", "upgrade", 22) }),
            };
            foreach (var (label, actions) in variants)
            {
                foreach (QueuePolicy qp in new[] { QueuePolicy.FifoBlock, QueuePolicy.SkipBlocked })
                {
                    var sim = new EconomySim(label, actions) { Queue = qp }.Run(200);
                    int auctionDone = sim.CompletedHour.GetValueOrDefault("auction", -1);
                    int first = sim.FirstAuction ?? -1;
                    var simAtSale = new EconomySim(label, actions) { Queue = qp }.Run(Math.Max(first, 24));
                    Line($"  {label,-24} {qp,-11}: 경매장 착공 {sim.StartedHour.GetValueOrDefault("auction:build:1", -1)}h 완공 {auctionDone}h, 첫 경매 {first}h, " +
                         $"매각 후 현금 {(first > 0 ? (simAtSale.Cash + simAtSale.Value(first).Bid).ToString("0.00") : "-")}");
                }
            }
        }
    }
}
