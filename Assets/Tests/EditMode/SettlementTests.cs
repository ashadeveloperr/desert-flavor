using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;
using NUnit.Framework;
using UnityEngine;

namespace DesertSettlement.Core.Tests
{
    public class SettlementTests
    {
        private static EconomyConfig _config;

        // 원본 v0.8 직렬 배치와 같은 타일·변.
        private static readonly HexCoord WellTile = new HexCoord(0, 0);
        private static readonly HexCoord FarmTile = new HexCoord(1, -1);
        private static readonly HexCoord QuarryTile = new HexCoord(2, -1);
        private static readonly HexCoord MineTile = new HexCoord(2, 0);
        private static readonly HexCoord AuctionTile = new HexCoord(-1, 0);
        private static readonly HexCoord WarehouseTile = new HexCoord(0, 1);
        private static readonly EdgeId Canal1 = EdgeId.Of(new HexCoord(1, -1), HexDirection.SouthEast);
        private static readonly EdgeId Canal2 = EdgeId.Of(new HexCoord(1, 0), HexDirection.NorthEast);
        private static readonly EdgeId Canal3 = EdgeId.Of(new HexCoord(2, -1), HexDirection.SouthEast);

        [OneTimeSetUp]
        public void LoadConfig()
        {
            string path = Path.Combine(Application.dataPath, "Data", "Config", "economy_balance_v0.8_config.json");
            _config = EconomyConfigLoader.Parse(File.ReadAllText(path));
        }

        private static Settlement NewSettlement(Action<SettlementRules> tweak = null)
        {
            var map = new HexMap(3);
            map.GetTile(WellTile).Terrain = TerrainType.WaterSource;
            map.GetTile(QuarryTile).Terrain = TerrainType.Bedrock;
            map.GetTile(MineTile).Terrain = TerrainType.OreVein;
            SettlementRules rules = SettlementRules.FromConfig(_config);
            tweak?.Invoke(rules);
            return new Settlement(map, rules);
        }

        /// <summary>원본 시뮬레이터의 고정 건설 스크립트를 시각별 명령으로 재현하는 도우미.</summary>
        private sealed class Script
        {
            public readonly Settlement S;
            private readonly Dictionary<int, List<Action>> _plan = new Dictionary<int, List<Action>>();
            private readonly Dictionary<int, string> _jobNames = new Dictionary<int, string>();
            public readonly Dictionary<string, int> Ids = new Dictionary<string, int>();

            public Script(Action<SettlementRules> tweak = null)
            {
                // v0.8 이식본과 같은 수치를 비교하는 스크립트라, 원본에 없는 가동 숙성·채석장 능력 규칙(2026-10-23)은 끈다.
                S = NewSettlement(r =>
                {
                    r.MaturityStartRatio = 1;
                    r.StoneMultiplierUsesQuarryCapacity = false;
                    tweak?.Invoke(r);
                });
                S.JobStarted += job =>
                {
                    if (_jobNames.TryGetValue(job.Id, out string name) && job.Type == JobType.Build) Ids[name] = job.FacilityId;
                };
                At(0, "well", s => s.RequestBuild(BuildingKinds.Well, WellTile));
                At(0, "farm", s => s.RequestBuild(BuildingKinds.Farm, FarmTile));
                At(6, "canal1", s => s.RequestCanal(Canal1));
                At(8, "canal2", s => s.RequestCanal(Canal2));
                At(10, "quarry", s => s.RequestBuild(BuildingKinds.Quarry, QuarryTile));
                At(10, "mine", s => s.RequestBuild(BuildingKinds.Mine, MineTile));
                At(18, "canal3", s => s.RequestCanal(Canal3));
                At(20, "auction", s => s.RequestBuild(BuildingKinds.AuctionHall, AuctionTile));
            }

            public Script At(int hour, string name, Func<Settlement, CommandResult> command)
            {
                if (!_plan.TryGetValue(hour, out List<Action> list)) _plan[hour] = list = new List<Action>();
                list.Add(() =>
                {
                    CommandResult r = command(S);
                    Assert.IsTrue(r.Success, $"h{hour} {name}: {r.Message}");
                    if (r.JobId > 0) _jobNames[r.JobId] = name;
                });
                return this;
            }

            public Script Do(int hour, Action<Settlement> action)
            {
                if (!_plan.TryGetValue(hour, out List<Action> list)) _plan[hour] = list = new List<Action>();
                list.Add(() => action(S));
                return this;
            }

            public Script Run(int until)
            {
                while (S.CurrentHour < until)
                {
                    if (_plan.TryGetValue((int)S.CurrentHour, out List<Action> actions)) actions.ForEach(a => a());
                    S.AdvanceHour();
                }
                return this;
            }

            public Facility F(string name) => S.GetFacility(Ids[name]);
            public double CashBefore(long hour) => S.History[(int)hour - 1].Cash;
        }

        // ───────────── v0.8 회귀 ─────────────

        // 회귀값: 원본 v0.8 건설 스크립트 + 확정 규칙(꼭짓점 방출·꼭짓점 취수, 배율 실적에 폐기분 포함, 광물 가치 계수 2.5).
        // 원본 v0.8(변 중간점 취수·폐기분 제외: 광산 74.28%, V 1732.80, 매각 후 2359.49)과는 그만큼 다르다.
        // 값의 출처: dotnet run --project Tools/SettlementSim (Core ↔ 이식본 교차 검증 출력).

        [Test]
        public void BaseScript_FirstAuctionMatchesRegression()
        {
            Script sc = new Script().Run(133);
            Settlement s = sc.S;

            Assert.AreEqual(37, s.AuctionHallCompletedHour, "경매장 완공 37h");
            // 133h = 원본 v0.8 스크립트의 첫 경매 시각(경매장 37h + 96h). 게임의 경매 간격은 72h로 바뀌었지만(2026-10-23),
            // 이 회귀는 v0.8 이식본과 같은 133h 시점의 경제 상태를 비교한다.
            Assert.AreEqual(72, _config.ConfirmedRules.AuctionRepeatsGameHours, "경매 간격 72h(첫 경매 = 완공 + 72h = 109h)");
            Assert.AreEqual(540.05, sc.CashBefore(133), 0.01, "첫 경매 직전 현금");
            Assert.AreEqual(0.7885, s.WaterRateOf(sc.F("mine")), 1e-4, "광산 물 가동률");
            Assert.AreEqual(91.5, s.Food, 0.01);
            Assert.AreEqual(100.0, s.Stone, 0.01);
            Assert.AreEqual(8.0, s.StoneDiscarded, 0.01, "초과 석재 폐기 8");

            ValuationResult v = SettlementValuation.Evaluate(s, 133);
            Assert.AreEqual(1.0, v.StoneAveragePerHour, 1e-9, "석재가 한도(100)에 차서 폐기돼도 생산 실적으로 센다(확정)");
            Assert.AreEqual(1149.63, v.Value, 0.01, "첫 경매 기준 가치 V (광물 가치 계수 2.5)");
            Assert.AreEqual(1747.16, sc.CashBefore(133) + v.Bid(_config.AuctionSeed.FixedTestBidMultiplier), 0.01, "즉시 매각 후 현금(중립 1.05)");
        }

        [Test]
        public void DeferWithMineUpgradeAndWarehouse_MatchesRegression()
        {
            Script sc = new Script();
            sc.Do(133, s => Assert.IsTrue(s.RequestUpgrade(sc.Ids["mine"]).Success));
            sc.At(133, "warehouse", s => s.RequestBuild(BuildingKinds.Warehouse, WarehouseTile));
            sc.Run(229);

            ValuationResult v = SettlementValuation.Evaluate(sc.S, 229);
            Assert.AreEqual(1959.54, sc.CashBefore(229) + v.Bid(_config.AuctionSeed.FixedTestBidMultiplier), 0.01);
            Assert.AreEqual(2, sc.F("mine").Level);
            Assert.GreaterOrEqual(sc.S.FoodCoefficient, 0.99);
        }

        [Test]
        public void StonePurchaseAtHour20_RushesAuctionHall()
        {
            Script sc = new Script();
            sc.Do(20, s => Assert.AreEqual(5, s.BuyStone(5)));
            sc.Run(128);
            Assert.AreEqual(32, sc.S.AuctionHallCompletedHour);
            Assert.AreEqual(32 + 72, sc.S.AuctionHallCompletedHour + _config.ConfirmedRules.AuctionRepeatsGameHours, "첫 경매가 4시간 빨라짐");
        }

        // ───────────── 건설 규칙 ─────────────

        [Test]
        public void Build_RespectsTerrainOccupancyAndDisabledKinds()
        {
            Settlement s = NewSettlement();
            s.Map.GetTile(new HexCoord(-2, 0)).Terrain = TerrainType.Obstacle;

            Assert.IsFalse(s.RequestBuild(BuildingKinds.Well, FarmTile).Success, "우물은 수원에만");
            Assert.IsFalse(s.RequestBuild(BuildingKinds.Mine, QuarryTile).Success, "광산은 광맥에만");
            Assert.IsFalse(s.RequestBuild(BuildingKinds.Farm, new HexCoord(-2, 0)).Success, "장애물 불가");
            Assert.IsFalse(s.RequestBuild(BuildingKinds.WaterTank, FarmTile).Success, "물탱크는 MVP 이후");
            Assert.IsFalse(s.RequestBuild(BuildingKinds.Farm, new HexCoord(9, 9)).Success, "맵 밖");

            Assert.IsTrue(s.RequestBuild(BuildingKinds.Farm, FarmTile).Success);
            Assert.IsFalse(s.RequestBuild(BuildingKinds.Warehouse, FarmTile).Success, "예약된 타일에 중복 불가");
        }

        [Test]
        public void Canals_AllowBoundaryEdges_GateNeedsCanal()
        {
            Settlement s = NewSettlement();
            EdgeId boundary = EdgeId.Of(new HexCoord(3, 0), HexDirection.East);
            Assert.IsTrue(s.RequestCanal(boundary).Success, "경계 변 수로 가능");
            Assert.IsFalse(s.RequestCanal(EdgeId.Of(new HexCoord(4, 0), HexDirection.West)).Success, "같은 변(이웃 쪽 표현) 중복 불가");
            Assert.IsFalse(s.RequestGate(Canal1).Success, "수로 없는 변에 수문 불가");
            Assert.IsTrue(s.RequestGate(boundary).Success, "예약된 수로 위 수문 가능");
        }

        [Test]
        public void Canal_IsBlockedOnlyBetweenTwoObstacles()
        {
            Settlement s = NewSettlement();
            var a = new HexCoord(-2, 1);
            HexCoord b = a.Neighbor(HexDirection.East);
            s.Map.GetTile(a).Terrain = TerrainType.Obstacle;
            s.Map.GetTile(b).Terrain = TerrainType.Obstacle;

            CommandResult between = s.RequestCanal(EdgeId.Of(a, HexDirection.East));
            Assert.IsFalse(between.Success, "양쪽 모두 장애물");
            StringAssert.Contains("장애물", between.Message);
            Assert.IsTrue(s.RequestCanal(EdgeId.Of(a, HexDirection.West)).Success, "한쪽만 장애물이면 가능");

            var edgeTile = new HexCoord(-3, 0);
            s.Map.GetTile(edgeTile).Terrain = TerrainType.Obstacle;
            Assert.IsTrue(s.RequestCanal(EdgeId.Of(edgeTile, HexDirection.West)).Success, "맵 경계 변은 바깥이 장애물이 아니므로 가능");
        }

        [Test]
        public void Upgrade_IsCappedAtMvpLevel2()
        {
            Script sc = new Script().Run(30);
            Facility farm = sc.F("farm");
            Assert.IsTrue(sc.S.RequestUpgrade(farm.Id).Success);
            sc.S.AdvanceHours(20);
            Assert.AreEqual(2, farm.Level);
            Assert.IsFalse(sc.S.RequestUpgrade(farm.Id).Success, "L3는 MVP에서 잠금");
        }

        [Test]
        public void Queue_IsFifo_BlockedFrontJobHoldsTheRest_UntilReordered()
        {
            Settlement s = NewSettlement(r => r.StarterStone = 23);
            int big = s.RequestBuild(BuildingKinds.Mine, MineTile).JobId;          // 석재 24 → 부족
            int farm = s.RequestBuild(BuildingKinds.Farm, FarmTile).JobId;         // 석재 12

            s.AdvanceHour();
            Assert.AreEqual(JobState.Queued, s.GetJob(big).State);
            Assert.AreEqual(JobState.Queued, s.GetJob(farm).State, "FIFO: 앞 작업이 막히면 뒤 작업도 대기");

            Assert.IsTrue(s.MoveQueuedJob(farm, 0).Success);
            s.AdvanceHour();
            Assert.AreEqual(JobState.InProgress, s.GetJob(farm).State, "순서를 바꾸면 착공");
            Assert.AreEqual(11.0, s.Stone, 1e-9);
        }

        [Test]
        public void TwoSlotsOnly()
        {
            Settlement s = NewSettlement();
            int a = s.RequestBuild(BuildingKinds.Well, WellTile).JobId;
            int b = s.RequestBuild(BuildingKinds.Farm, FarmTile).JobId;
            int c = s.RequestCanal(Canal1).JobId;
            s.AdvanceHour();
            Assert.AreEqual(JobState.InProgress, s.GetJob(a).State);
            Assert.AreEqual(JobState.InProgress, s.GetJob(b).State);
            Assert.AreEqual(JobState.Queued, s.GetJob(c).State);
        }

        [Test]
        public void Cancel_QueuedIsFree_InProgressRefundsHalfStone()
        {
            Settlement s = NewSettlement();
            int farm = s.RequestBuild(BuildingKinds.Farm, FarmTile).JobId;
            Assert.IsTrue(s.CancelJob(farm).Success);
            Assert.AreEqual(75.0, s.Stone);

            int mine = s.RequestBuild(BuildingKinds.Mine, MineTile).JobId;
            s.AdvanceHour();
            Assert.AreEqual(51.0, s.Stone, 1e-9);
            Assert.IsTrue(s.CancelJob(mine).Success);
            Assert.AreEqual(63.0, s.Stone, 1e-9, "24의 50% = 12 환급");
            Assert.IsNull(s.FacilityAt(MineTile), "건설 취소 → 시설 제거");
        }

        [Test]
        public void Demolish_TakesHalfBuildTime_RefundsHalfOfInvestedStone()
        {
            Script sc = new Script().Run(30);
            Settlement s = sc.S;
            Facility farm = sc.F("farm");
            double stone = s.Stone;
            Assert.IsTrue(s.RequestDemolish(farm.Id).Success);

            s.AdvanceHour();                       // 착공 (농장 L1 6시간 × 0.5 = 3시간)
            Assert.AreEqual(FacilityState.Demolishing, farm.State);
            s.AdvanceHours(3);                     // 완료
            Assert.IsNull(s.GetFacility(farm.Id));
            Assert.Greater(s.Stone, stone - 1e-9, "환급 석재 6(=12×0.5)이 들어온다(저장 한도 내)");
        }

        [Test]
        public void Warehouse_CannotClose_AndDemolishChecksStorage()
        {
            Script sc = new Script(r => r.StartingCash = 100000); // 석재를 한도 가까이 사 모을 수 있게
            sc.At(40, "warehouse", s => s.RequestBuild(BuildingKinds.Warehouse, WarehouseTile));
            sc.Run(60);
            Settlement st = sc.S;
            Facility wh = sc.F("warehouse");
            Assert.AreEqual(200.0, st.StoneCapacity);
            Assert.IsFalse(st.SetClosed(wh.Id, true).Success, "MVP: 창고 수동 폐쇄 불가");

            st.BuyStone(200);
            Assert.Greater(st.Stone, 100.0);
            Assert.IsFalse(st.RequestDemolish(wh.Id).Success, "철거 후 한도(100)를 재고가 넘으면 거부");
        }

        [Test]
        public void ClosedFacility_UsesNoWaterOrFood_AndPaysPartialUpkeep()
        {
            Script sc = new Script().Run(40);
            Settlement s = sc.S;
            Facility quarry = sc.F("quarry");
            double before = s.LastWater.TotalDelivered;
            Assert.IsTrue(s.SetClosed(quarry.Id, true).Success);
            HourRecord r = s.AdvanceHour();
            Assert.IsFalse(s.LastWater.Consumers.ContainsKey(quarry.Id.ToString()), "폐쇄 시설은 물 수요 없음");
            Assert.Less(s.LastWater.TotalDelivered, before);
            Assert.AreEqual(0.0, r.StoneProduced);

            double fullUpkeep = s.Facilities.Where(f => f.IsOperating).Sum(f => s.LevelOf(f).Upkeep ?? 0);
            Assert.AreEqual(fullUpkeep + 0.3 * 0.7, r.Upkeep, 1e-9, "채석장 유지비 0.7의 30%");
        }

        [Test]
        public void ClosedGate_CutsDownstreamWater()
        {
            Script sc = new Script();
            sc.At(30, "gate", s => s.RequestGate(Canal2));
            sc.Run(40);
            Settlement s = sc.S;
            Facility gate = s.FacilityOn(Canal2, BuildingKinds.CanalGate);
            Assert.IsNotNull(gate);
            Assert.Greater(s.WaterRateOf(sc.F("mine")), 0.7);

            s.SetGateOpen(gate.Id, false);
            s.AdvanceHour();
            Assert.AreEqual(1.0, s.WaterRateOf(sc.F("farm")), 1e-9, "수문 상류는 그대로");
            Assert.AreEqual(1.0, s.WaterRateOf(sc.F("quarry")), 1e-9, "채석장은 수로1 끝 꼭짓점에서도 받는다");
            Assert.AreEqual(0.0, s.WaterRateOf(sc.F("mine")), "수문 하류(수로2 너머)만 닿는 광산은 단수");
        }

        [Test]
        public void OutputOf_ReportsProductionAndFoodUse()
        {
            Script sc = new Script().Run(133);
            Settlement s = sc.S;

            FacilityOutput farm = s.OutputOf(sc.F("farm"));
            Assert.AreEqual(ResourceKind.Food, farm.Resource);
            Assert.AreEqual(4.0, farm.ProducedPerHour, 1e-9, "물 100%·F 100% → 최대 4/h");
            Assert.AreEqual(0.75, farm.FoodUsePerHour, 1e-9);
            Assert.IsFalse(farm.FarmBelowOwnUpkeep);

            FacilityOutput mine = s.OutputOf(sc.F("mine"));
            Assert.AreEqual(ResourceKind.Ore, mine.Resource);
            Assert.AreEqual(12.0 * s.WaterRateOf(sc.F("mine")), mine.ProducedPerHour, 1e-9);
            Assert.AreEqual(s.History.Last().OreProduced, mine.ProducedPerHour, 1e-9, "직전 정산과 같은 조건이면 같은 값");

            Assert.IsFalse(s.OutputOf(sc.F("well")).IsProducer);
        }

        [Test]
        public void FarmWithoutWater_IsBelowItsOwnUpkeep()
        {
            Settlement s = NewSettlement();
            Assert.IsTrue(s.RequestBuild(BuildingKinds.Farm, FarmTile).Success);
            s.AdvanceHours(8);
            Facility farm = s.FacilityAt(FarmTile);
            FacilityOutput o = s.OutputOf(farm);
            Assert.AreEqual(0.0, o.ProducedPerHour);
            Assert.IsTrue(o.FarmBelowOwnUpkeep, "물 없는 농장은 생산 0 < 소비 0.75");
        }

        [Test]
        public void FoodShortage_LowersCoefficient_ThenPurchaseRecovers()
        {
            Settlement s = NewSettlement(r => r.StarterFood = 0);
            Assert.IsTrue(s.RequestBuild(BuildingKinds.Farm, FarmTile).Success);   // 물 없는 농장 → 식량 생산 0
            s.AdvanceHours(31);
            Assert.Less(s.FoodCoefficient, 1.0, "식량 부족 → F 감소");
            Assert.IsNotNull(s.FirstShortageHour);

            double f = s.FoodCoefficient;
            s.BuyFood(50);
            s.AdvanceHour();
            Assert.AreEqual(f, s.FoodCoefficient, 1e-12, "물 받는 농장이 없으면 회복도 하지 않는다(v0.8 규칙)");
        }
    }
}
