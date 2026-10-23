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
    /// - 병원: 전염병 예고가 뜨면 광산을 가장 많이 덮는 사막 칸에 병원을 짓는다.
    /// 물탱크는 상황 판단이 많아 넣지 않았다(O8). 기존 측정과 비교할 수 있도록 기준 봇은 그대로 둔다.
    /// </summary>
    public static class SkilledBot
    {
        private const double CashReserve = 300;

        /// <summary>측정 출력용 누적 횟수(보류·광산 개량·병원).</summary>
        public static int Holds, MineUpgrades, Hospitals;

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
                    if (a.CurrentAuction.Kind == AuctionKind.Regular && a.Holds == 0 && HoldPays(s, a))
                    {
                        a.Hold();
                        Holds++;
                        UpgradeMines(s, force: true);
                        continue;
                    }
                    var bids = a.CurrentAuction.Bids.ToList();
                    a.Sell(bids.IndexOf(bids.OrderByDescending(b => b.Cash).First()));
                    return c.State == CampaignState.Shopping;
                }
                ReferenceBot.UnblockStone(s);
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

        private static IEnumerable<Facility> Upgradable(Settlement s, string kind) =>
            s.OperatingOf(kind).Where(f => s.CanUpgrade(f.Id).Success).OrderBy(f => f.Level).ToList();

        /// <summary>6시간마다: 석재·자금이 넉넉하면 광산 → (식량 계수가 낮으면) 농장 순으로 한 단계.</summary>
        private static void Improve(Settlement s)
        {
            if (s.Jobs.Any(j => j.State == JobState.Queued) || s.Cash < CashReserve + 200) return;
            if (UpgradeMines(s, force: false)) return;
            if (s.FoodCoefficient < 0.95)
            {
                Facility farm = Upgradable(s, BuildingKinds.Farm).FirstOrDefault();
                if (farm != null && Enough(s, farm)) s.RequestUpgrade(farm.Id);
            }
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

        /// <summary>광산을 가장 많이 덮는(반경 1) 빈 사막 칸에 병원.</summary>
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
