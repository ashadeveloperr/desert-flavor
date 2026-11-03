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
    /// 숙련 봇(2026-10-23 로드맵 2-④, Docs/decision_log_auto.md O5~O8). 기준 봇(<see cref="ReferenceBot"/>, 다중 계획)에 더해:
    /// - 개량: 6시간마다 석재·자금이 넉넉하면 광산을 한 단계 올리고, 식량 계수가 떨어지면 농장을 올린다.
    /// - 보류: 첫 정기 경매에서 '보류 + 광산 개량'의 예상 가치(시세 감소 포함)가 지금 파는 것보다 크면 한 번 보류한다.
    /// - 병원: 전염병 예고가 뜨면 병원을 짓는다(병상만큼 환자가 빨리 회복, 위치는 상관없어 광산 옆 빈 사막).
    /// 물탱크는 상황 판단이 많아 넣지 않았다(O8). 기존 측정과 비교할 수 있도록 기준 봇은 그대로 둔다.
    /// </summary>
    public static class SkilledBot
    {
        private const double CashReserve = 300;

        /// <summary>측정 출력용 누적 횟수(보류·광산 개량·병원·추가 주택).</summary>
        public static int Holds, MineUpgrades, Hospitals, Houses;

        /// <summary>매각 직전 경매(측정용, 재미 보강 측정 2026-10-24). null이면 아무것도 안 함.</summary>
        public static Action<AuctionEvent> BeforeSell;

        /// <summary>매각 시점 측정(`-- selltime`): 0 이상이면 HoldPays 대신 정기 경매를 이 횟수만큼 보류한 뒤 판다. -1 = 보통 판단.</summary>
        public static int ForceHolds = -1;

        public static bool PlayCampaignSettlement(Campaign c, int maxHours = 900)
        {
            List<MapBuildEntry> plan = ReferenceBot.Plan(c.CurrentMap, BotStrategy.Multi);
            if (plan == null) return false;
            var withPlan = new MapDefinition { Id = c.CurrentMap.Id, Radius = c.CurrentMap.Radius, Terrain = c.CurrentMap.Terrain, SampleLayout = plan };
            if (withPlan.RequestSampleLayout(c.Current).Count > 0) return false;

            Settlement s = c.Current;
            AuctionManager a = c.Auctions;
            bool hospital = false;
            for (int h = 0; h < maxHours; h++)
            {
                if (a.IsFinished) return false;
                if (a.IsOpen)
                {
                    if (a.CurrentAuction.Kind == AuctionKind.Regular && (ForceHolds >= 0 ? a.Holds < ForceHolds : a.Holds == 0 && HoldPays(s, a)))
                    {
                        a.Hold();
                        Holds++;
                        UpgradeMines(s, force: true);
                        continue;
                    }
                    var bids = a.CurrentAuction.Bids.ToList();
                    BeforeSell?.Invoke(a.CurrentAuction);
                    LivingAtSale += s.LivingStandard;
                    SalesSeen++;
                    a.Sell(bids.IndexOf(bids.OrderByDescending(b => b.Cash).First()));
                    return c.State == CampaignState.Shopping;
                }
                ReferenceBot.UnblockStone(s);
                if (SellSurplus) SellOverflow(s);
                if (!hospital && EpidemicWarned(s)) hospital = BuildHospital(s);
                if (h % 6 == 0) Improve(s);
                s.AdvanceHour();
            }
            return false;
        }

        /// <summary>보류 + 광산 한 단계 개량의 예상 가치 × (1 − 보류 시세 감소) − 72시간 유지비 − 석재값 &gt; 지금 가치.</summary>
        private static bool HoldPays(Settlement s, AuctionManager a)
        {
            List<Facility> mines = Upgradable(s, BuildingKinds.Mine).ToList();
            if (mines.Count == 0) return false;
            double v = SettlementValuation.Evaluate(s, a.CurrentAuction.Hour).Value;
            double ore = s.OperatingOf(BuildingKinds.Mine).Sum(m => s.LevelOf(m).OreProduction ?? 0);
            double gain = mines.Sum(m => (s.Rules.Config.GetLevel(BuildingKinds.Mine, m.Level + 1).OreProduction ?? 0) - (s.LevelOf(m).OreProduction ?? 0));
            if (ore <= 0) return false;
            double after = v * (1 + gain / ore) * (1 - s.Rules.Config.Auction.HoldMarketPenalty);
            double upkeep = s.History.Count > 0 ? s.History[s.History.Count - 1].Upkeep * 72 : 0;
            double stone = mines.Sum(m => s.Rules.Config.GetLevel(BuildingKinds.Mine, m.Level + 1).Stone ?? 0) * 4; // 석재 1 ≈ 구매가 4
            return after - upkeep - stone > v;
        }

        /// <summary>남는 자원 판매 여부(2026-10-27 이슈 #18, 측정 인자 `sell=0`으로 끔)와 누적 판매 수입·구매 지출(측정 출력용).</summary>
        public static bool SellSurplus = true;
        public static double SalesIncome, PurchaseSpend;

        /// <summary>
        /// 창고가 80% 넘게 차면(곧 버려질 몫) 60%까지 판다. 판매가가 기준의 30% 밑이면 멈춘다(헐값 투매 안 함).
        /// 석재는 대기 작업이 없을 때만 판다.
        /// </summary>
        private static void SellOverflow(Settlement s)
        {
            if (!s.HasMarket || s.Rules.Trade == null) return;
            // 식품 창고가 차면 곡물 밖의 재화부터 판다(재화마다 10개는 남김, 판매가가 기준의 30% 밑이면 멈춤, 결정 FD6).
            if (s.PantryUsed >= s.FoodCapacity * 0.8)
            {
                foreach (Goods g in GoodsCatalog.All.Where(x => x != Goods.Grain))
                {
                    double floor = GoodsCatalog.Settings(s.Rules.Config, g).SellPrice * 0.3;
                    while (s.PantryUsed > s.FoodCapacity * 0.6 && s.Stock(g) >= 11 && s.NextSellPrice(g) >= Math.Max(1e-9, floor))
                    {
                        double before = s.Cash;
                        if (s.SellGoods(g, 1) == 0) break;
                        SalesIncome += s.Cash - before;
                    }
                }
            }
            void Sell(bool food)
            {
                double stock = food ? s.PantryUsed : s.Stone, cap = food ? s.FoodCapacity : s.StoneCapacity;
                if (stock < cap * 0.8) return;
                var p = food ? s.Rules.Config.Purchase.Food : s.Rules.Config.Purchase.Stone;
                double floor = p.BaseCost * p.SellRatio * 0.3;
                int want = (int)(stock - cap * 0.6);
                for (int i = 0; i < want && s.NextSellPrice(food) >= floor; i++)
                {
                    double before = s.Cash;
                    if ((food ? s.SellFood(1) : s.SellStone(1)) == 0) break;
                    SalesIncome += s.Cash - before;
                }
            }
            Sell(true);
            if (!s.Jobs.Any(j => j.State == JobState.Queued)) Sell(false);
        }

        private static IEnumerable<Facility> Upgradable(Settlement s, string kind) =>
            s.OperatingOf(kind).Where(f => s.CanUpgrade(f.Id).Success).OrderBy(f => f.Level).ToList();

        /// <summary>
        /// 6시간마다: 석재·자금이 넉넉하면 (사람이 모자라면) 주택 → 광산 → (식량이 모자라면) 농장 순으로 하나.
        /// 식량 부족 신호는 노동력이 켜져 있으면 직전 시간 식량 적자·재고 부족, 꺼져 있으면 식량 계수 F(원본).
        /// </summary>
        private static void Improve(Settlement s)
        {
            if (s.Jobs.Any(j => j.State == JobState.Queued) || s.Cash < CashReserve + 200) return;
            if (s.Rules.LaborOn && AddHouse(s)) return;
            if (UpgradeMines(s, force: false)) return;
            HourRecord last = s.History.Count > 0 ? s.History[s.History.Count - 1] : null;
            bool hungry = s.Rules.LaborOn ? last != null && (last.FoodDeficit > 0 || last.FoodProduced < last.FoodDemand && s.Food < 30) : s.FoodCoefficient < 0.95;
            if (hungry)
            {
                Facility farm = Upgradable(s, BuildingKinds.Farm).FirstOrDefault();
                if (farm != null && Enough(s, farm)) { s.RequestUpgrade(farm.Id); return; }
            }
            if (Diet && !hungry) AddDietFacility(s);
        }

        /// <summary>
        /// 식품 다양화 측정(결정 FD·LS, `smart diet`): 굶지 않을 때 차례로 하나씩 — 과수원 → 제분소 → 목축지 → 양조장 → 포도주 공방 → 경작지 하나 더.
        /// 물이 드는 시설은 수로가 닿은 빈 사막에, 가공 시설은 아무 빈 사막에. 이미 지었으면 건너뜀.
        /// </summary>
        public static bool Diet;
        /// <summary>측정 출력용: 매각 순간 생활 수준 합과 매각 수.</summary>
        public static double LivingAtSale;
        public static int SalesSeen;
        public static int DietBuilds;

        private static readonly string[] DietOrder =
            { BuildingKinds.Orchard, BuildingKinds.Mill, BuildingKinds.Pasture, BuildingKinds.Brewery, BuildingKinds.Winery, BuildingKinds.Farm };

        private static void AddDietFacility(Settlement s)
        {
            // 사람처럼 광산을 먼저 지킨다: 광산을 우선 고용(결정 LB7), 광산 물이 모자라면 물을 쓰는 새 시설은 짓지 않는다.
            foreach (Facility mine in s.OperatingOf(BuildingKinds.Mine))
                if (!s.HirePriority.Contains(mine.Id)) s.PrioritizeHiring(mine.Id);
            bool minesDry = s.OperatingOf(BuildingKinds.Mine).Any(m => s.WaterRateOf(m) < 0.99);
            foreach (string kind in DietOrder)
            {
                int want = kind == BuildingKinds.Farm ? 2 : 1;
                if (s.Facilities.Count(f => f.Kind == kind) >= want) continue;
                if (s.Stone < (s.Rules.Config.GetLevel(kind, 1).Stone ?? 0) + 10) return;
                bool wet = (s.Rules.Config.GetLevel(kind, 1).WaterDemand ?? 0) > 0;
                if (wet && minesDry) continue;
                HexCoord? site = WetDesert(s, kind, wet);
                if (site != null && s.RequestBuild(kind, site.Value).Success) DietBuilds++;
                return;
            }
        }

        private static HexCoord? WetDesert(Settlement s, string kind, bool needWater)
        {
            var wet = new HashSet<VertexId>(s.Facilities.Where(f => f.Kind == BuildingKinds.CanalEdge && f.IsOperating)
                .SelectMany(f => new[] { f.Edge.Value.VertexA, f.Edge.Value.VertexB }));
            List<HexCoord> wells = s.OperatingOf(BuildingKinds.Well).Select(w => w.Tile).ToList();
            return s.Map.Tiles.Where(t => s.CanBuild(kind, t.Coord).Success && (!needWater || HexTopology.VerticesOf(t.Coord).Any(wet.Contains)))
                .OrderBy(t => wells.Count == 0 ? 0 : wells.Min(w => w.DistanceTo(t.Coord))).ThenBy(t => t.Coord.Q).ThenBy(t => t.Coord.R)
                .Select(t => (HexCoord?)t.Coord).FirstOrDefault();
        }

        /// <summary>
        /// 운영 시설 + 공사가 필요로 하는 인원이 주민보다 많으면 주택 하나(결정 LB1~).
        /// 자리는 수로가 닿은(물을 받을) 빈 사막 중 우물에서 가까운 칸. 석재가 모자라면 짓지 않는다.
        /// </summary>
        private static bool AddHouse(Settlement s)
        {
            LaborAssignment labor = s.LastLabor;
            if (labor.IdleResidents > 1 || labor.Needed <= labor.Assigned + 0.5) return false;
            if (s.Stone < (s.Rules.Config.GetLevel(BuildingKinds.House, 1).Stone ?? 0) + 10) return false;
            var wet = new HashSet<VertexId>(s.Facilities.Where(f => f.Kind == BuildingKinds.CanalEdge && f.IsOperating)
                .SelectMany(f => new[] { f.Edge.Value.VertexA, f.Edge.Value.VertexB }));
            List<HexCoord> wells = s.OperatingOf(BuildingKinds.Well).Select(w => w.Tile).ToList();
            HexCoord? site = s.Map.Tiles.Where(t => t.Terrain == TerrainType.Desert && s.CanBuild(BuildingKinds.House, t.Coord).Success
                                                    && HexTopology.VerticesOf(t.Coord).Any(wet.Contains))
                .OrderBy(t => wells.Count == 0 ? 0 : wells.Min(w => w.DistanceTo(t.Coord))).ThenBy(t => t.Coord.Q).ThenBy(t => t.Coord.R)
                .Select(t => (HexCoord?)t.Coord).FirstOrDefault();
            if (site == null || !s.RequestBuild(BuildingKinds.House, site.Value).Success) return false;
            Houses++;
            return true;
        }

        private static bool Enough(Settlement s, Facility f) =>
            s.Stone >= (s.Rules.Config.GetLevel(f.Kind, f.Level + 1).Stone ?? 0) + 10;

        /// <summary>광산 개량 예약. force면 석재가 모자라도 예약한다(UnblockStone이 사서 채움).</summary>
        private static bool UpgradeMines(Settlement s, bool force)
        {
            bool any = false;
            foreach (Facility m in Upgradable(s, BuildingKinds.Mine))
            {
                if (!force && !Enough(s, m)) break;
                if (s.RequestUpgrade(m.Id).Success) { any = true; MineUpgrades++; }
                if (!force) break; // 평소에는 한 번에 하나
            }
            return any;
        }

        private static bool EpidemicWarned(Settlement s) =>
            s.Disasters != null && s.Disasters.Events.Any(e => e.TypeId == DisasterManager.EpidemicId && e.State == DisasterState.Warned);

        /// <summary>광산 옆 빈 사막 칸에 병원(병원 효과는 위치와 상관없다, 결정 EV3).</summary>
        private static bool BuildHospital(Settlement s)
        {
            if (s.Cash < CashReserve) return false;
            List<HexCoord> mines = s.Facilities.Where(f => f.Kind == BuildingKinds.Mine).Select(f => f.Tile).ToList();
            HexCoord? best = null;
            int bestCover = 0;
            foreach (HexCoord m in mines)
                foreach (HexCoord c in ProceduralMapGenerator.Neighbors(m))
                {
                    if (!s.CanBuild(BuildingKinds.Hospital, c).Success) continue;
                    int cover = mines.Count(x => x.DistanceTo(c) <= 1);
                    if (cover > bestCover) { bestCover = cover; best = c; }
                }
            bool ok = best.HasValue && s.RequestBuild(BuildingKinds.Hospital, best.Value).Success;
            if (ok) Hospitals++;
            return ok;
        }
    }
}
