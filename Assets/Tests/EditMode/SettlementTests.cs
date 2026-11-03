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
            _config = TestData.Economy();
        }

        private static Settlement NewSettlement(Action<SettlementRules> tweak = null, bool v08Regression = false)
        {
            var map = new HexMap(3);
            map.GetTile(WellTile).Terrain = TerrainType.WaterSource;
            map.GetTile(QuarryTile).Terrain = TerrainType.Bedrock;
            map.GetTile(MineTile).Terrain = TerrainType.OreVein;
            SettlementRules rules = v08Regression ? SettlementRules.ForV08Regression(_config) : SettlementRules.FromConfig(_config);
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
                // v0.8 이식본과 같은 수치를 비교하는 스크립트라, v0.8 뒤에 더한 규칙을 끈 회귀 프리셋으로 돈다.
                S = NewSettlement(tweak, v08Regression: true);
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

        /// <summary>
        /// v0.8 회귀값은 광물 가치 계수 2.5로 고정된 원본 스크립트 기준이다. 게임 밸런스 값(2026-10-23 1-⑦: 2.3)과 상관없이 2.5로 평가한다.
        /// </summary>
        private static ValuationResult WithV08OreFactor(Func<ValuationResult> evaluate)
        {
            double saved = _config.AuctionSeed.OreProduction24hValueFactor;
            _config.AuctionSeed.OreProduction24hValueFactor = 2.5;
            try { return evaluate(); }
            finally { _config.AuctionSeed.OreProduction24hValueFactor = saved; }
        }

        [Test]
        public void BaseScript_FirstAuctionMatchesRegression()
        {
            // 회귀값과 그 뜻은 TestData.cs의 V08Golden(결정 TD2). 게임의 경매 간격(설정)과 별개인 v0.8 기준 시점이다.
            const int auction = V08Golden.FirstAuctionHour;
            Script sc = new Script().Run(auction);
            Settlement s = sc.S;

            Assert.AreEqual(V08Golden.AuctionHallCompletedHour, s.AuctionHallCompletedHour, "경매장 완공");
            Assert.AreEqual(V08Golden.CashBeforeFirstAuction, sc.CashBefore(auction), 0.01, "첫 경매 직전 현금");
            Assert.AreEqual(V08Golden.MineWaterRate, s.WaterRateOf(sc.F("mine")), 1e-4, "광산 물 가동률");
            Assert.AreEqual(V08Golden.FoodAtFirstAuction, s.Food, 0.01);
            Assert.AreEqual(V08Golden.StoneAtFirstAuction, s.Stone, 0.01);
            Assert.AreEqual(V08Golden.StoneDiscarded, s.StoneDiscarded, 0.01, "초과 석재 폐기");

            ValuationResult v = WithV08OreFactor(() => SettlementValuation.Evaluate(s, auction));
            Assert.AreEqual(1.0, v.StoneAveragePerHour, 1e-9, "석재가 한도에 차서 폐기돼도 생산 실적으로 센다(확정)");
            Assert.AreEqual(V08Golden.FirstAuctionValue, v.Value, 0.01, "첫 경매 기준 가치 V (광물 가치 계수 2.5)");
            Assert.AreEqual(V08Golden.CashAfterSellingAtFirstAuction, sc.CashBefore(auction) + v.Bid(_config.AuctionSeed.FixedTestBidMultiplier), 0.01, "즉시 매각 후 현금");
        }

        [Test]
        public void DeferWithMineUpgradeAndWarehouse_MatchesRegression()
        {
            Script sc = new Script();
            sc.Do(V08Golden.FirstAuctionHour, s => Assert.IsTrue(s.RequestUpgrade(sc.Ids["mine"]).Success));
            sc.At(V08Golden.FirstAuctionHour, "warehouse", s => s.RequestBuild(BuildingKinds.Warehouse, WarehouseTile));
            sc.Run(V08Golden.SecondAuctionHour);

            ValuationResult v = WithV08OreFactor(() => SettlementValuation.Evaluate(sc.S, V08Golden.SecondAuctionHour));
            Assert.AreEqual(V08Golden.CashAfterHoldWithMineUpgradeAndWarehouse,
                sc.CashBefore(V08Golden.SecondAuctionHour) + v.Bid(_config.AuctionSeed.FixedTestBidMultiplier), 0.01);
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
        }

        // ───────────── 건설 규칙 ─────────────

        [Test]
        public void Build_RespectsTerrainAndOccupancy()
        {
            Settlement s = NewSettlement();
            s.Map.GetTile(new HexCoord(-2, 0)).Terrain = TerrainType.Obstacle;

            Assert.IsFalse(s.RequestBuild(BuildingKinds.Well, FarmTile).Success, "우물은 수원에만");
            Assert.IsFalse(s.RequestBuild(BuildingKinds.Mine, QuarryTile).Success, "광산은 광맥에만");
            Assert.IsFalse(s.RequestBuild(BuildingKinds.Farm, new HexCoord(-2, 0)).Success, "장애물 불가");
            Assert.IsTrue(s.CanBuild(BuildingKinds.WaterTank, FarmTile).Success, "물탱크는 사막에 지을 수 있다(2026-10-23 로드맵 1-②)");
            Assert.IsFalse(s.RequestBuild(BuildingKinds.Farm, new HexCoord(9, 9)).Success, "맵 밖");

            Assert.IsTrue(s.RequestBuild(BuildingKinds.Farm, FarmTile).Success);
            Assert.IsFalse(s.RequestBuild(BuildingKinds.Warehouse, FarmTile).Success, "예약된 타일에 중복 불가");
        }

        [Test]
        public void Canals_AllowBoundaryEdges()
        {
            Settlement s = NewSettlement();
            EdgeId boundary = EdgeId.Of(new HexCoord(3, 0), HexDirection.East);
            Assert.IsTrue(s.RequestCanal(boundary).Success, "경계 변 수로 가능");
            Assert.IsFalse(s.RequestCanal(EdgeId.Of(new HexCoord(4, 0), HexDirection.West)).Success, "같은 변(이웃 쪽 표현) 중복 불가");
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
        public void Upgrade_GoesUpToLevel3()
        {
            // 2026-10-23 로드맵 1-③: 레벨 상한 2 → 3(Docs/decision_log_auto.md U1).
            Script sc = new Script(r => r.StartingCash = 100000).Run(30);
            Facility farm = sc.F("farm");
            sc.S.BuyStone(60);
            Assert.IsTrue(sc.S.RequestUpgrade(farm.Id).Success);
            sc.S.AdvanceHours(20);
            Assert.AreEqual(2, farm.Level);
            Assert.IsTrue(sc.S.RequestUpgrade(farm.Id).Success, "L3 개량 가능");
            sc.S.AdvanceHours(20);
            Assert.AreEqual(3, farm.Level);
            Assert.IsFalse(sc.S.RequestUpgrade(farm.Id).Success, "L3가 최고");
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
        public void Queue_DemolishSkipsBlockedFrontJob_BuildsStillWait()
        {
            // 2026-10-26 JQ1(이슈 #3): 철거는 석재가 들지 않으니 막힌 앞 작업을 건너뛰어 시작한다. 건설은 FIFO 그대로.
            Settlement s = NewSettlement(r =>
            {
                r.StarterStone = 23;
                r.FreeBuilds[BuildingKinds.Farm] = 1;
            });
            Assert.IsTrue(s.RequestBuild(BuildingKinds.Farm, FarmTile).Success); // 무료 완공 배치
            Facility farm = s.FacilityAt(FarmTile);
            int big = s.RequestBuild(BuildingKinds.Mine, MineTile).JobId;             // 석재 24 → 부족
            int well = s.RequestBuild(BuildingKinds.Well, WellTile).JobId;            // 석재가 있어도 앞이 막혀 대기
            int demolish = s.RequestDemolish(farm.Id).JobId;

            s.AdvanceHour();
            Assert.AreEqual(JobState.Queued, s.GetJob(big).State);
            Assert.AreEqual(JobState.Queued, s.GetJob(well).State, "건설은 FIFO: 앞이 막히면 대기");
            Assert.AreEqual(JobState.InProgress, s.GetJob(demolish).State, "철거는 막힌 작업을 건너뛰어 착공");
            Assert.AreEqual(FacilityState.Demolishing, farm.State);
            Assert.AreEqual(23.0, s.Stone, 1e-9, "철거는 석재를 쓰지 않음");

            s.AdvanceHours(3); // 농장 L1 6시간 × 0.5 = 3시간 뒤 철거 완료(무료 배치라 환급 석재는 0)
            Assert.IsNull(s.GetFacility(farm.Id));
            Assert.AreEqual(JobState.Queued, s.GetJob(big).State, "석재가 모자란 건설은 계속 대기");
            Assert.AreEqual(JobState.Queued, s.GetJob(well).State);
        }

        [Test]
        public void Command_StartsJobImmediately_AndEndsExactlyHoursLater()
        {
            // 결정 JQ2·JQ4: 명령 순간 석재를 내고 착공, 명령 시각 + 공사 시간에 완공(정각 명령이면 그 정각).
            Settlement s = NewSettlement();
            Assert.IsTrue(s.Rules.StartJobsOnCommand, "게임 설정은 명령 순간 착공");
            s.AdvanceHours(3);
            long h = s.CurrentHour;
            int farm = s.RequestBuild(BuildingKinds.Farm, FarmTile).JobId;
            ConstructionJob job = s.GetJob(farm);
            Assert.AreEqual(JobState.InProgress, job.State, "정각을 기다리지 않음");
            Assert.AreEqual(75.0 - job.StoneCost, s.Stone, 1e-9, "석재도 바로 냄");
            Assert.AreEqual(h, job.StartedHour);
            Assert.AreEqual(h, job.StartTime, 1e-9);
            Assert.AreEqual(h + job.Hours, job.EndTime, 1e-9, "명령 시각 + 공사 시간");
            Assert.AreEqual(h + job.Hours - 1, job.DeadlineHour, "정각 완공은 그 앞 시간의 정산에 든다");

            Facility f = s.FacilityAt(FarmTile);
            s.AdvanceHours(job.Hours - 1);
            Assert.AreEqual(FacilityState.Constructing, f.State);
            s.AdvanceHour();
            Assert.AreEqual(FacilityState.Operating, f.State, "명령 시각 + 공사 시간 정각에 완공");
        }

        [Test]
        public void Command_WaitsForTheHour_WhenStartJobsOnCommandIsOff()
        {
            Settlement s = NewSettlement(r => r.StartJobsOnCommand = false);
            int farm = s.RequestBuild(BuildingKinds.Farm, FarmTile).JobId;
            Assert.AreEqual(JobState.Queued, s.GetJob(farm).State, "원본 v0.8: 다음 정각에 착공");
            s.AdvanceHour();
            Assert.AreEqual(JobState.InProgress, s.GetJob(farm).State);
            Assert.AreEqual(s.GetJob(farm).StartedHour + s.GetJob(farm).Hours, s.GetJob(farm).DeadlineHour);
        }

        [Test]
        public void MidHourCommand_EndsAtTheSameMinute_NotOnTheHour()
        {
            // 결정 JQ4(이슈 #37): 12:30에 5시간 공사를 명령하면 17:30에 완공. 진행률은 착공 순간 0%.
            Settlement s = NewSettlement();
            s.AdvanceHours(3);
            long h = s.CurrentHour;
            s.AdvanceTo(h + 0.5);
            ConstructionJob job = s.GetJob(s.RequestBuild(BuildingKinds.Farm, FarmTile).JobId);
            Assert.AreEqual(JobState.InProgress, job.State);
            Assert.AreEqual(h + 0.5, job.StartTime, 1e-9);
            Assert.AreEqual(h + 0.5 + job.Hours, job.EndTime, 1e-9, "명령 시각 + 공사 시간");
            Assert.AreEqual(0, s.JobProgress(job, h + 0.5), 1e-9, "착공 순간 0%");
            Assert.AreEqual(0.5 / job.Hours, s.JobProgress(job, h + 1), 1e-9, "반 시간이면 반 시간만큼");

            Facility f = s.FacilityAt(FarmTile);
            s.AdvanceHours(job.Hours);
            Assert.AreEqual(FacilityState.Constructing, f.State, "정각에는 아직 30분 남음");
            s.AdvanceTo(h + job.Hours + 29 / 60.0);
            Assert.AreEqual(FacilityState.Constructing, f.State);
            s.AdvanceTo(h + job.Hours + 0.5);
            Assert.AreEqual(FacilityState.Operating, f.State, "정각을 기다리지 않고 그 분에 완공");
            Assert.AreEqual(h + job.Hours, f.CompletedHour, "완공 시각이 든 시간의 생산부터 낸다");
        }

        [Test]
        public void QueuedJob_StartsOnTheHourItsStoneArrives()
        {
            // 결정 JQ4(이슈 #37): 정산에서 생산된 석재로 그 정각에 착공한다(다음 정산까지 한 시간 더 기다리지 않음).
            Settlement s = NewSettlement(r =>
            {
                r.Labor = null; // 채석장 일손 없이 생산(석재 흐름만 본다)
                r.StarterStone = 4; // 수로 2칸 값만(수로는 무료 배치가 없음)
                r.FreeBuilds[BuildingKinds.Well] = 1;
                r.FreeBuilds[BuildingKinds.Quarry] = 1;
            });
            Assert.IsTrue(s.RequestBuild(BuildingKinds.Well, WellTile).Success);
            Assert.IsTrue(s.RequestBuild(BuildingKinds.Quarry, QuarryTile).Success);
            Assert.IsTrue(s.RequestCanal(Canal1).Success);
            Assert.IsTrue(s.RequestCanal(Canal2).Success); // 우물 → 채석장 물길
            Assert.AreEqual(0, s.Stone, 1e-9, "수로 착공에 시작 석재를 다 씀");
            ConstructionJob job = s.GetJob(s.RequestBuild(BuildingKinds.Warehouse, WarehouseTile).JobId);
            Assert.AreEqual(JobState.Queued, job.State, "석재가 모자라 대기");

            for (int i = 0; i < 100 && job.State == JobState.Queued; i++)
            {
                s.AdvanceHour();
                if (job.State == JobState.Queued) Assert.Less(s.Stone + 1e-8, job.StoneCost, $"{s.CurrentHour}시: 석재가 찼는데 대기");
            }
            Assert.AreEqual(JobState.InProgress, job.State, "채석장 석재가 모이면 착공");
            Assert.AreEqual(s.CurrentHour, job.StartTime, 1e-9, "석재가 찬 그 정각");
            Assert.AreEqual(s.CurrentHour - 1, job.StartedHour, "정각 착공은 막 정산한 시간으로 센다");
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
            Settlement s = NewSettlement(r => r.StartJobsOnCommand = false); // '대기 중' 취소를 보려고 다음 정각 착공으로
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
        public void Warehouse_CloseAndDemolish_CheckStorage()
        {
            Script sc = new Script(r => r.StartingCash = 100000); // 석재를 한도 가까이 사 모을 수 있게
            sc.At(40, "warehouse", s => s.RequestBuild(BuildingKinds.Warehouse, WarehouseTile));
            sc.Run(60);
            Settlement st = sc.S;
            Facility wh = sc.F("warehouse");
            Assert.AreEqual(100.0 + st.Rules.Config.GetLevel(BuildingKinds.Warehouse, 1).StoneStorageBonus.Value, st.StoneCapacity, "기본 100 + 창고 L1 석재 보너스(결정 UP2: 식품의 40%)");
            st.BuyStone(250);
            Assert.Greater(st.Stone, 100.0);
            Assert.IsFalse(st.SetClosed(wh.Id, true).Success, "폐쇄 후 한도(100)를 재고가 넘으면 거부(2026-10-23 창고 폐쇄 허용)");
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
            Assert.IsFalse(s.LastWater.Consumers.ContainsKey(WaterIds.Of(quarry)), "폐쇄 시설은 물 수요 없음");
            Assert.Less(s.LastWater.TotalDelivered, before);
            Assert.AreEqual(0.0, r.StoneProduced);

            double fullUpkeep = s.Facilities.Where(f => f.IsOperating).Sum(f => s.LevelOf(f).Upkeep ?? 0);
            Assert.AreEqual(fullUpkeep + 0.3 * 0.7, r.Upkeep, 1e-9, "채석장 유지비 0.7의 30%");
        }

        [Test]
        public void ClosedCanal_CutsDownstreamWater_AndReopens()
        {
            // 2026-10-23 수문 폐지: 물길 개폐는 수로 폐쇄로 한다.
            Script sc = new Script();
            sc.Run(40);
            Settlement s = sc.S;
            Facility canal = s.FacilityOn(Canal2, BuildingKinds.CanalEdge);
            Assert.IsNotNull(canal);
            Assert.Greater(s.WaterRateOf(sc.F("mine")), 0.7);

            Assert.IsTrue(s.SetClosed(canal.Id, true).Success);
            s.AdvanceHour();
            Assert.AreEqual(1.0, s.WaterRateOf(sc.F("farm")), 1e-9, "닫힌 수로 상류는 그대로");
            Assert.AreEqual(1.0, s.WaterRateOf(sc.F("quarry")), 1e-9, "채석장은 수로1 끝 꼭짓점에서도 받는다");
            Assert.AreEqual(0.0, s.WaterRateOf(sc.F("mine")), "닫힌 수로2 너머만 닿는 광산은 단수");

            Assert.IsTrue(s.SetClosed(canal.Id, false).Success);
            s.AdvanceHour();
            Assert.Greater(s.WaterRateOf(sc.F("mine")), 0.7, "다시 열면 물이 돌아온다");
        }

        [Test]
        public void OutputOf_ReportsProductionAndFoodUse()
        {
            Script sc = new Script().Run(V08Golden.FirstAuctionHour);
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
            Settlement s = NewSettlement(r => r.Labor = null); // 시설 고정 식량 소비는 노동력이 꺼진 원본 규칙(노동력이 켜지면 일꾼이 먹는다)
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
            // 식량 계수 F는 노동력이 꺼진 원본 규칙에만 있다(노동력이 켜지면 주민이 떠난다 — LaborTests).
            Settlement s = NewSettlement(r => { r.StarterFood = 0; r.Labor = null; });
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
