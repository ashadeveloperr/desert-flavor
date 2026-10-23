using System;
using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Auction;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;
using DesertSettlement.Core.Progression;

namespace SettlementSim
{
    /// <summary>
    /// 맵 난이도를 재기 위한 단순한 기준 플레이(사람보다 서툰 고정 전략).
    /// 수로로 가장 가까운 우물–광맥 쌍을 고르고: 우물 → 우물 옆 농장 → 가장 가까운 암반까지 수로·채석장
    /// → 광맥까지 수로·광산 → 경매장 순서로 한 번에 예약한 뒤, 첫 경매(또는 파산)까지 돌린다.
    /// 경매가 열리면 현금이 가장 큰 입찰을 받는다.
    /// <see cref="BotStrategy.Multi"/>는 여기에 더해 남은 광맥마다 '새 우물 + 그 옆 농장 + 수로 + 광산' 묶음을 경매장 뒤에 예약한다.
    /// </summary>
    public enum BotStrategy
    {
        /// <summary>우물·광맥 하나씩만 쓴다(서툰 기준).</summary>
        Single,
        /// <summary>모든 광맥에 광산을 짓고, 남는 우물이 있으면 광산마다 우물·농장을 하나씩 더 쓴다(숙련 기준).</summary>
        Multi,
    }

    public static class ReferenceBot
    {
        public sealed class Result
        {
            public bool Planned;
            public string Failure;
            public long AuctionHour = -1;
            public bool Bankrupt;
            public double NeutralValue;
            public double BestCash;
            public string BestBidder;
            public double SettlementCashAtAuction;
            public double FinalCash => SettlementCashAtAuction + BestCash;
            public int CanalCount;
            public int MineCount;
            public int WellCount;
            public AuctionManager Auctions;
            public Settlement Settlement;
        }

        /// <summary>맵에 대한 건설 계획(MapBuildEntry 목록). 계획을 세울 수 없으면 null.</summary>
        public static List<MapBuildEntry> Plan(MapDefinition def, BotStrategy strategy = BotStrategy.Single)
        {
            HexMap map = def.CreateMap();
            var topology = new HexTopology(map);
            List<HexCoord> Tiles(TerrainType t) => map.Tiles.Where(x => x.Terrain == t).Select(x => x.Coord).ToList();
            List<HexCoord> wells = Tiles(TerrainType.WaterSource), ores = Tiles(TerrainType.OreVein), rocks = Tiles(TerrainType.Bedrock);
            if (wells.Count == 0 || ores.Count == 0 || rocks.Count == 0) return null;

            // 물은 '수로가 닿은 우물 꼭짓점'에서만 나오고, 시설도 '수로가 닿은 꼭짓점'에서만 받는다.
            // 그래서 먼저 우물–농장 경계 변에 수로를 놓아 물이 나오는 지점을 만들고, 나머지 경로는 그 수로망에서 뻗는다.
            (HexCoord well, HexCoord farm, HexCoord ore, List<EdgeId> path)? best = null;
            foreach (HexCoord w in wells)
            {
                foreach (HexDirection dir in Enum.GetValues(typeof(HexDirection)))
                {
                    HexCoord f = w.Neighbor(dir);
                    if (!map.Contains(f) || map.GetTile(f).Terrain != TerrainType.Desert) continue;
                    EdgeId border = EdgeId.Of(w, dir);
                    var start = new[] { border.VertexA, border.VertexB };
                    foreach (HexCoord o in ores)
                    {
                        List<EdgeId> path = CanalPaths.ShortestPath(topology, map, start, new[] { o });
                        if (path == null) continue;
                        if (best == null || path.Count < best.Value.path.Count) best = (w, f, o, path);
                    }
                }
            }
            if (best == null) return null;
            HexCoord well = best.Value.well, ore = best.Value.ore;
            HexCoord farmTile = best.Value.farm;
            EdgeId wellFarmEdge = EdgeId.Of(well, DirectionTo(well, farmTile));

            // 지금까지의 수로망(우물–농장 변 + 광맥 경로)에서 가장 가까운 암반.
            var network = new HashSet<VertexId> { wellFarmEdge.VertexA, wellFarmEdge.VertexB };
            foreach (EdgeId e in best.Value.path) { network.Add(e.VertexA); network.Add(e.VertexB); }
            List<EdgeId> rockPath = null;
            HexCoord rock = default;
            foreach (HexCoord r in rocks)
            {
                List<EdgeId> path = CanalPaths.ShortestPath(topology, map, network, new[] { r });
                if (path != null && (rockPath == null || path.Count < rockPath.Count)) { rockPath = path; rock = r; }
            }
            if (rockPath == null) return null;

            var used = new HashSet<HexCoord> { well, ore, rock, farmTile };
            foreach (EdgeId e in rockPath) { network.Add(e.VertexA); network.Add(e.VertexB); }

            // 숙련 기준: 남은 광맥을 수로가 짧은 순서로 하나씩 개발한다.
            // 쓰지 않은 우물이 있으면 그 우물–농장 경계 변에서 광맥까지 새 경로(우물 1개 = 물 100이 광산 50 + 농장 30을 감당),
            // 없으면 기존 수로망에서 광맥까지 잇는다(물이 모자랄 수 있다).
            var expansions = new List<(HexCoord? well, HexCoord? farm, EdgeId? border, HexCoord ore, List<EdgeId> path)>();
            if (strategy == BotStrategy.Multi)
            {
                var usedWells = new HashSet<HexCoord> { well };
                var leftOres = ores.Where(o => o != ore).ToList();
                while (leftOres.Count > 0)
                {
                    (HexCoord? w, HexCoord? f, EdgeId? border, HexCoord o, List<EdgeId> path)? pick = null;
                    foreach (HexCoord w in wells.Where(x => !usedWells.Contains(x)))
                    {
                        foreach (HexDirection dir in Enum.GetValues(typeof(HexDirection)))
                        {
                            HexCoord f = w.Neighbor(dir);
                            if (!map.Contains(f) || map.GetTile(f).Terrain != TerrainType.Desert || used.Contains(f)) continue;
                            EdgeId border = EdgeId.Of(w, dir);
                            foreach (HexCoord o in leftOres)
                            {
                                List<EdgeId> path = CanalPaths.ShortestPath(topology, map, new[] { border.VertexA, border.VertexB }, new[] { o });
                                if (path != null && (pick == null || path.Count < pick.Value.path.Count)) pick = (w, f, border, o, path);
                            }
                        }
                    }
                    if (pick == null)
                    {
                        foreach (HexCoord o in leftOres)
                        {
                            List<EdgeId> path = CanalPaths.ShortestPath(topology, map, network, new[] { o });
                            if (path != null && (pick == null || path.Count < pick.Value.path.Count)) pick = (null, null, null, o, path);
                        }
                    }
                    if (pick == null) break;
                    var x = pick.Value;
                    expansions.Add(x);
                    leftOres.Remove(x.o);
                    used.Add(x.o);
                    if (x.w.HasValue) { usedWells.Add(x.w.Value); used.Add(x.w.Value); used.Add(x.f.Value); }
                    if (x.border.HasValue) { network.Add(x.border.Value.VertexA); network.Add(x.border.Value.VertexB); }
                    foreach (EdgeId e in x.path) { network.Add(e.VertexA); network.Add(e.VertexB); }
                }
            }

            HexCoord? farm = farmTile;
            HexCoord? hall = map.Tiles.Where(t => t.Terrain == TerrainType.Desert && !used.Contains(t.Coord))
                .OrderBy(t => t.Coord.DistanceTo(well)).ThenBy(t => t.Coord.Q).ThenBy(t => t.Coord.R)
                .Select(t => (HexCoord?)t.Coord).FirstOrDefault();
            if (hall == null) return null;

            var plan = new List<MapBuildEntry>
            {
                Build(BuildingKinds.Well, well),
                Build(BuildingKinds.Farm, farm.Value),
            };
            var canals = new HashSet<EdgeId>();
            void AddCanals(IEnumerable<EdgeId> edges)
            {
                foreach (EdgeId e in edges)
                {
                    if (!canals.Add(e)) continue;
                    plan.Add(new MapBuildEntry { Kind = BuildingKinds.CanalEdge, Q = e.Owner.Q, R = e.Owner.R, Side = e.Side.ToString() });
                }
            }
            AddCanals(new[] { wellFarmEdge });
            AddCanals(rockPath);
            plan.Add(Build(BuildingKinds.Quarry, rock));
            AddCanals(best.Value.path);
            plan.Add(Build(BuildingKinds.Mine, ore));
            plan.Add(Build(BuildingKinds.AuctionHall, hall.Value));
            // 확장 묶음은 경매장 뒤: 첫 경매 시각(경매장 완공 + 경매 간격 72h)을 늦추지 않고, 그 사이에 짓는다.
            foreach (var x in expansions)
            {
                if (x.well.HasValue)
                {
                    plan.Add(Build(BuildingKinds.Well, x.well.Value));
                    plan.Add(Build(BuildingKinds.Farm, x.farm.Value));
                    AddCanals(new[] { x.border.Value });
                }
                AddCanals(x.path);
                plan.Add(Build(BuildingKinds.Mine, x.ore));
            }
            return plan;
        }

        private static HexDirection DirectionTo(HexCoord from, HexCoord to)
        {
            foreach (HexDirection d in Enum.GetValues(typeof(HexDirection))) if (from.Neighbor(d) == to) return d;
            throw new ArgumentException("이웃 타일이 아닙니다.");
        }

        private static MapBuildEntry Build(string kind, HexCoord c) => new MapBuildEntry { Kind = kind, Q = c.Q, R = c.R };

        /// <summary>
        /// Core 캠페인의 지금 정착지에서 계획을 예약하고 첫 경매까지 돌린 뒤 현금이 가장 큰 입찰에 판다.
        /// 팔았으면 true(캠페인은 상점 상태), 계획 불가·파산·시간 초과면 false.
        /// </summary>
        public static bool PlayCampaignSettlement(Campaign c, BotStrategy strategy = BotStrategy.Multi, int maxHours = 600)
        {
            List<MapBuildEntry> plan = Plan(c.CurrentMap, strategy);
            if (plan == null) return false;
            var withPlan = new MapDefinition { Id = c.CurrentMap.Id, Radius = c.CurrentMap.Radius, Terrain = c.CurrentMap.Terrain, SampleLayout = plan };
            if (withPlan.RequestSampleLayout(c.Current).Count > 0) return false;
            for (int h = 0; h < maxHours && !c.Auctions.IsOpen && !c.Auctions.IsFinished; h++) c.Current.AdvanceHour();
            if (!c.Auctions.IsOpen) return false;
            var bids = c.Auctions.CurrentAuction.Bids.ToList();
            c.Auctions.Sell(bids.IndexOf(bids.OrderByDescending(b => b.Cash).First()));
            return c.State == CampaignState.Shopping;
        }

        /// <summary>계획을 예약하고 첫 경매(또는 파산·시간 초과)까지 돌린다. disasters를 주면 캠페인처럼 재해 일정을 붙인다(seed로 결정).</summary>
        public static Result Run(EconomyConfig config, MapDefinition def, Action<SettlementRules> tweak = null, int seed = 1, int maxHours = 600,
            BotStrategy strategy = BotStrategy.Single, DisasterSettings disasters = null)
        {
            var result = new Result();
            List<MapBuildEntry> plan = Plan(def, strategy);
            if (plan == null)
            {
                result.Failure = "계획 불가";
                return result;
            }
            result.Planned = true;
            result.CanalCount = plan.Count(b => b.Kind == BuildingKinds.CanalEdge);
            result.MineCount = plan.Count(b => b.Kind == BuildingKinds.Mine);
            result.WellCount = plan.Count(b => b.Kind == BuildingKinds.Well);

            SettlementRules rules = SettlementRules.FromConfig(config);
            tweak?.Invoke(rules);
            var s = new Settlement(def.CreateMap(), rules);
            var a = new AuctionManager(s, seed);
            if (disasters != null) DisasterManager.Attach(s, disasters, seed);
            var withPlan = new MapDefinition { Id = def.Id, Radius = def.Radius, Terrain = def.Terrain, SampleLayout = plan };
            List<string> failures = withPlan.RequestSampleLayout(s);
            if (failures.Count > 0)
            {
                result.Failure = "예약 실패: " + string.Join("; ", failures);
                return result;
            }

            for (int h = 0; h < maxHours && !a.IsOpen && !a.IsFinished; h++) s.AdvanceHour();
            result.Auctions = a;
            result.Settlement = s;
            result.Bankrupt = a.Phase == AuctionPhase.Bankrupt;
            if (!a.IsOpen)
            {
                result.Failure = result.Bankrupt ? "파산: " + a.BankruptcyReason : "시간 초과";
                return result;
            }
            AuctionEvent e = a.CurrentAuction;
            AuctionBid top = e.Bids.OrderByDescending(b => b.Cash).First();
            result.AuctionHour = e.Hour;
            result.NeutralValue = SettlementValuation.Evaluate(s, e.Hour).Value;
            result.BestCash = top.Cash;
            result.BestBidder = top.Bidder.Name;
            result.SettlementCashAtAuction = s.Cash;
            return result;
        }
    }
}
