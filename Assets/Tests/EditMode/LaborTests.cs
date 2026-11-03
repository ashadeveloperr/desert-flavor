using System;
using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;
using NUnit.Framework;

namespace DesertSettlement.Core.Tests
{
    /// <summary>
    /// 주택·노동력(결정 LB1~): 주택은 빈 채로 생겨 천천히 참(HI1), 주민이 떠나고 돌아옴, 배치율 생산, 배치 순서와 우선 고용,
    /// 공사 인원(기본 인부 먼저, 모자라면 느려짐), 주민이 먹는 식량, 원본 v0.8 회귀 모드에서는 꺼짐.
    /// </summary>
    public class LaborTests
    {
        private static readonly HexCoord WellTile = HexCoord.Zero;
        private static readonly HexCoord HouseTile = new HexCoord(1, 0), FarmTile = new HexCoord(0, 1), FarTile = new HexCoord(-2, 2);
        private static readonly HexCoord SecondWell = new HexCoord(-2, 0);

        /// <summary>
        /// 반지름 3 맵, 수원 두 곳(가운데·서쪽). 설정을 바꿀 수 있게 매번 새로 읽는다. instantHouse면 주택 1채를 공사 없이 바로 세울 수 있다
        /// (빈 채로 생기므로 다 찬 주택이 필요하면 <see cref="TestData.MoveIn"/>).
        /// </summary>
        private static Settlement NewSettlement(Action<EconomyConfig> configure = null, Action<SettlementRules> tweak = null, bool instantHouse = true)
        {
            EconomyConfig config = TestData.Economy();
            configure?.Invoke(config);
            var map = new HexMap(3);
            map.GetTile(WellTile).Terrain = TerrainType.WaterSource;
            map.GetTile(SecondWell).Terrain = TerrainType.WaterSource;
            SettlementRules rules = SettlementRules.FromConfig(config);
            rules.StartingCash = 5000;
            if (instantHouse) rules.FreeBuilds[BuildingKinds.House] = 1;
            tweak?.Invoke(rules);
            return new Settlement(map, rules);
        }

        private static LaborSettings Labor(Settlement s) => s.Rules.Labor;
        private static double Capacity(Settlement s) => s.Rules.Config.GetLevel(BuildingKinds.House, 1).Residents.Value;

        /// <summary>우물(무료) + 우물–주택 경계 수로(공사) → 주택이 우물 옆 꼭짓점에서 바로 물을 받는다.</summary>
        private static Facility WaterTheHouse(Settlement s)
        {
            Assert.IsTrue(s.RequestCanal(EdgeId.Of(WellTile, HexDirection.East)).Success);
            for (int i = 0; i < 6 && s.Jobs.Count > 0; i++) s.AdvanceHour();
            Assert.AreEqual(0, s.Jobs.Count, "수로 공사가 끝나야 한다(기본 인부가 맡음)");
            return s.FacilityAt(WellTile);
        }

        [Test]
        public void NoFreeHouse_AndHousesCannotClose()
        {
            // 결정 HI4: 정착지 무료 주택은 없다(이주 사업가 혜택만 빈 무료 주택).
            Settlement s = NewSettlement(instantHouse: false);
            Assert.IsTrue(s.Rules.LaborOn);
            Assert.AreEqual(0, Labor(s).FreeHouses);
            Assert.AreEqual(0, s.FreeBuildsRemaining(BuildingKinds.House));
            Assert.IsTrue(s.RequestBuild(BuildingKinds.House, HouseTile).Success);
            Facility house = s.FacilityAt(HouseTile);
            Assert.AreEqual(FacilityState.Constructing, house.State, "석재를 내고 공사");
            Assert.AreEqual(0, house.Residents, 1e-12);
            for (int i = 0; i < 30 && house.State != FacilityState.Operating; i++) s.AdvanceHour();
            Assert.IsFalse(s.SetClosed(house.Id, true).Success, "주택은 닫지 않는다");
        }

        [Test]
        public void BuiltHouse_StartsEmpty_AndFillsAtTheArrivalSpeed()
        {
            // 결정 HI1·HI2: 주택은 빈 채로 완공되고 시간당 정원 × returnPerHour × 생활 수준 배율씩 찬다.
            Settlement s = NewSettlement(c => c.Labor.StartGraceHours = 1000, instantHouse: false); // 물 부족을 세지 않아 들어오는 속도만 본다
            Assert.IsTrue(s.RequestBuild(BuildingKinds.House, HouseTile).Success);
            Facility built = s.FacilityAt(HouseTile);
            for (int i = 0; i < 30 && built.State != FacilityState.Operating; i++) s.AdvanceHour();
            Assert.AreEqual(FacilityState.Operating, built.State);
            double cap = Capacity(s);
            Assert.AreEqual(1, s.ReturnFactor, 1e-9, "곡물만 먹어 생활 수준 0 → 배율 1");
            Assert.AreEqual(Labor(s).ReturnPerHour * cap, built.ResidentFill, 1e-9, "빈 채로 완공되고 완공한 시간에 한 시간 몫만 들어옴");
            Assert.AreEqual(Math.Floor(built.ResidentFill), built.Residents, "주민은 1명분이 다 차야 센다(결정 PI1)");
            s.AdvanceHour();
            Assert.AreEqual(2 * Labor(s).ReturnPerHour * cap, built.ResidentFill, 1e-9);
            Assert.AreEqual(Math.Floor(built.ResidentFill), built.Residents);
        }

        [Test]
        public void WholePeople_ChangeOnlyWhenAWholePersonArrivesOrLeaves()
        {
            // 결정 PI1: 늘 때는 내림, 줄 때는 올림.
            Assert.AreEqual(4, WholePeople.Next(4, 4.7));
            Assert.AreEqual(5, WholePeople.Next(4, 5.0));
            Assert.AreEqual(6, WholePeople.Next(6, 5.4), "떠나는 중이어도 1명분이 다 빠지기 전엔 그대로");
            Assert.AreEqual(5, WholePeople.Next(6, 5.0));
            Assert.AreEqual(4, WholePeople.Next(4, 3.6), "들어오다 다시 떠나도 남은 사람은 그대로");
            Assert.AreEqual(3, WholePeople.Next(4, 3.0));
        }

        [Test]
        public void Labor_AssignsWholePeopleOnly()
        {
            // 결정 PI1: 일손은 1명 단위. 진행도가 남아도 0.x명은 배치되지 않는다.
            Settlement s = NewSettlement(c => c.Labor.StartGraceHours = 1000, instantHouse: false);
            Assert.IsTrue(s.RequestBuild(BuildingKinds.House, HouseTile).Success);
            for (int i = 0; i < 40; i++)
            {
                s.AdvanceHour();
                Assert.AreEqual(Math.Round(s.Residents), s.Residents, 1e-12, $"{s.CurrentHour}h 주민 수가 정수");
                foreach (double n in s.LastLabor.Facilities.Values.Concat(s.LastLabor.Jobs.Values))
                    Assert.AreEqual(Math.Round(n), n, 1e-12, $"{s.CurrentHour}h 배치 인원이 정수");
            }
        }

        [Test]
        public void HouseUpgrade_AddedCapacityFillsGradually()
        {
            // 결정 HI1: 개량으로 늘어난 정원도 바로 차지 않고 유입 속도대로 찬다.
            Settlement s = NewSettlement(c => c.Labor.StartGraceHours = 1000);
            Assert.IsTrue(s.RequestBuild(BuildingKinds.House, HouseTile).Success);
            Facility house = s.FacilityAt(HouseTile);
            TestData.MoveIn(s);
            double before = house.Residents;
            Assert.IsTrue(s.RequestUpgrade(house.Id).Success);
            for (int i = 0; i < 30 && house.State != FacilityState.Operating; i++) s.AdvanceHour();
            Assert.AreEqual(2, house.Level);
            double cap2 = s.HouseCapacity(house);
            Assert.Greater(cap2, before);
            Assert.AreEqual(before + Labor(s).ReturnPerHour * s.ReturnFactor * cap2, house.ResidentFill, 1e-9, "개량이 끝난 시간에 한 시간 몫만 들어옴");
            Assert.AreEqual(Math.Floor(house.ResidentFill + 1e-9), house.Residents);
        }

        [Test]
        public void HouseWithoutWater_ResidentsLeaveSlowly_AndReturnWhenWatered()
        {
            // 떠남·복귀 기본 속도만 본다: 생활 수준(이주 배율, FoodGoodsTests)과 시작 유예(StartGrace_ 테스트)는 끈다.
            Settlement s = NewSettlement(c => { c.Living = null; c.Labor.StartGraceHours = 1; }, r => r.FreeBuilds[BuildingKinds.Well] = 1);
            s.RequestBuild(BuildingKinds.House, HouseTile);
            s.RequestBuild(BuildingKinds.Well, WellTile);
            TestData.MoveIn(s); // 유예 1시간 안에 다 참
            Facility house = s.FacilityAt(HouseTile);
            double cap = Capacity(s);

            s.AdvanceHour(); // 수로가 없어 물 0 → 목표 0
            Assert.AreEqual(cap - Labor(s).LeavePerHour * cap, house.ResidentFill, 1e-9, "한 시간에 정원 × leavePerHour씩 떠난다");
            Assert.AreEqual(Math.Ceiling(house.ResidentFill - 1e-9), house.Residents, "1명분이 다 빠져야 한 명 준다(결정 PI1)");
            Assert.IsTrue(s.IsLeaving(house));
            s.AdvanceHours(4);
            double left = house.ResidentFill;
            Assert.AreEqual(cap - 5 * Labor(s).LeavePerHour * cap, left, 1e-9);

            WaterTheHouse(s);
            double before = house.ResidentFill;
            s.AdvanceHour();
            Assert.AreEqual(1.0, s.HouseWaterRate(house), 1e-9, "우물 옆 꼭짓점에서 물을 받는다");
            Assert.AreEqual(Math.Min(cap, before + Labor(s).ReturnPerHour * cap), house.ResidentFill, 1e-9, "정원 × returnPerHour씩 돌아온다");
        }

        [Test]
        public void StartGrace_HouseWithoutWater_KeepsResidentsAndLiving_UntilGraceEnds()
        {
            // 결정 BQ3: 정착지 시작 후 startGraceHours 동안은 주택 물 부족을 떠남·생활 수준에 세지 않는다.
            const double grace = 4;
            Settlement s = NewSettlement(c => c.Labor.StartGraceHours = grace);
            s.RequestBuild(BuildingKinds.House, HouseTile);
            Facility house = s.FacilityAt(HouseTile);
            double cap = Capacity(s);

            TestData.MoveIn(s);
            s.AdvanceHours((int)grace - 2);
            Assert.AreEqual(0, s.HouseWaterRate(house), 1e-9, "수로가 없어 물은 오지 않는다");
            Assert.AreEqual(1, s.StartGraceHoursLeft, 1e-9);
            Assert.IsFalse(s.IsLeaving(house), "유예 중에는 떠나는 중으로 보이지 않는다");
            s.AdvanceHour();
            Assert.AreEqual(cap, house.Residents, 1e-9, "유예 동안 아무도 떠나지 않는다");
            Assert.AreEqual(1, s.LastLiving.Supply, 1e-9, "물 부족을 생활 수준 부족으로 세지 않는다");
            Assert.GreaterOrEqual(s.LivingStandard, 0);

            Assert.AreEqual(0, s.StartGraceHoursLeft, 1e-9);
            Assert.IsTrue(s.IsLeaving(house), "유예가 끝나면 떠나는 중");
            s.AdvanceHour();
            Assert.Less(house.ResidentFill, cap, "유예가 끝나면 떠나기 시작한다");
            Assert.Less(s.LivingTarget, 0, "물 부족이 생활 수준 목표를 음수로 만든다");
        }

        [Test]
        public void Residents_EatFood_FacilitiesDoNot()
        {
            Settlement s = NewSettlement(tweak: r => r.FreeBuilds[BuildingKinds.Well] = 1);
            s.RequestBuild(BuildingKinds.House, HouseTile);
            s.RequestBuild(BuildingKinds.Well, WellTile);
            TestData.MoveIn(s);
            double residents = s.Residents;
            HourRecord h = s.AdvanceHour();
            double expected = (residents + s.Rules.BaseCrew) * Labor(s).FoodPerResidentPerHour;
            Assert.AreEqual(expected, h.FoodDemand, 1e-9, "주민 + 기본 인부가 먹고, 우물(foodUse 0.25)은 먹지 않는다");
            Assert.AreEqual(1.0, s.FoodCoefficient, 1e-12, "노동력이 켜져 있으면 F는 1에 머문다");
        }

        [Test]
        public void Production_ScalesWithStaffRatio()
        {
            // 농장 L1 필요 인원을 정원보다 크게: 주민 6명(1단계 우물은 일손 0) → 농장 6/10 = 60%.
            Settlement s = NewSettlement(c => c.BuildingLevels[BuildingKinds.Farm][0].Workers = 10,
                r => { r.FreeBuilds[BuildingKinds.Well] = 1; r.FreeBuilds[BuildingKinds.Farm] = 1; });
            s.RequestBuild(BuildingKinds.House, HouseTile);
            s.RequestBuild(BuildingKinds.Well, WellTile);
            s.RequestBuild(BuildingKinds.Farm, FarmTile);
            TestData.MoveIn(s);
            Assert.IsTrue(s.RequestCanal(EdgeId.Of(WellTile, HexDirection.SouthEast)).Success); // 우물–농장 경계
            for (int i = 0; i < 6 && s.Jobs.Count > 0; i++) s.AdvanceHour();
            Facility farm = s.FacilityAt(FarmTile);
            HourRecord h = s.AdvanceHour();
            double ratio = s.StaffRatio(farm);
            Assert.Greater(ratio, 0.3);
            Assert.LessOrEqual(ratio, 0.6);
            double max = s.Rules.MaxOutput(BuildingKinds.Farm, s.LevelOf(farm));
            Assert.AreEqual(max * s.WaterRateOf(farm) * ratio, h.FoodProduced, 1e-9, "생산 = 최대 × 물 × 배치율");
            FacilityOutput o = s.OutputOf(farm);
            Assert.AreEqual(ratio, o.Workforce, 1e-12);
            Assert.AreEqual(10, o.WorkersNeeded, 1e-12);
        }

        [Test]
        public void Hiring_FollowsCompletionOrder_PriorityMovesToFront_AndClears()
        {
            // 우물 필요 인원 4, 주민 6: 먼저 지은 우물 4명, 다음 우물 2명.
            Settlement s = NewSettlement(c => c.BuildingLevels[BuildingKinds.Well][0].Workers = 4,
                r => r.FreeBuilds[BuildingKinds.Well] = 2);
            s.RequestBuild(BuildingKinds.House, HouseTile);
            s.RequestBuild(BuildingKinds.Well, WellTile);
            s.RequestBuild(BuildingKinds.Well, SecondWell);
            Facility first = s.FacilityAt(WellTile), second = s.FacilityAt(SecondWell);
            TestData.MoveIn(s);
            Assert.AreEqual(4, s.LastLabor.OfFacility(first.Id), 1e-9);
            Assert.AreEqual(2, s.LastLabor.OfFacility(second.Id), 1e-9);

            Assert.IsTrue(s.PrioritizeHiring(second.Id).Success);
            Assert.AreEqual(4, s.LastLabor.OfFacility(second.Id), 1e-9, "우선 고용한 시설이 먼저 찬다");
            Assert.AreEqual(2, s.LastLabor.OfFacility(first.Id), 1e-9);
            Assert.IsFalse(s.CanPrioritizeHiring(second.Id).Success, "이미 1순위");
            Assert.IsTrue(s.PrioritizeHiring(first.Id).Success);
            Assert.AreEqual(new[] { first.Id, second.Id }, s.HirePriority.ToArray(), "마지막에 누른 시설이 1순위, 전에 누른 것은 밀린다");

            Assert.IsTrue(s.ClearHiringPriority(first.Id).Success);
            Assert.IsTrue(s.ClearHiringPriority(second.Id).Success);
            Assert.AreEqual(4, s.LastLabor.OfFacility(first.Id), 1e-9, "해제하면 완공 순으로 돌아간다");
            Assert.IsFalse(s.ClearHiringPriority(second.Id).Success);
            Facility house = s.FacilityAt(HouseTile);
            Assert.IsFalse(s.CanPrioritizeHiring(house.Id).Success, "사람이 필요 없는 시설");
        }

        [Test]
        public void DryFacility_IsSkippedByAutoHiring_ButPriorityStillStaffsIt()
        {
            Settlement s = NewSettlement(tweak: r => { r.FreeBuilds[BuildingKinds.Well] = 1; r.FreeBuilds[BuildingKinds.Farm] = 1; });
            s.RequestBuild(BuildingKinds.House, HouseTile);
            s.RequestBuild(BuildingKinds.Well, WellTile);
            s.RequestBuild(BuildingKinds.Farm, FarTile); // 수로가 없어 물 0
            TestData.MoveIn(s);
            // 배치 순서는 직전 시간 물 배분으로 정한다(LB6): 첫 정산 전에는 기록이 없어 100%로 보고, 두 번째 시간부터 빠진다.
            s.AdvanceHours(2);
            Facility farm = s.FacilityAt(FarTile);
            Assert.AreEqual(0, s.WaterRateOf(farm), 1e-12);
            Assert.AreEqual(0, s.LastLabor.OfFacility(farm.Id), 1e-12, "물 0% 시설에는 자동 배치하지 않는다");
            Assert.IsFalse(s.HiringOrder().Contains(farm));
            Assert.IsTrue(s.PrioritizeHiring(farm.Id).Success);
            Assert.Greater(s.LastLabor.OfFacility(farm.Id), 0, "우선 고용은 물 조건과 상관없이 배치");
        }

        [Test]
        public void Construction_UsesCrewFirst_AndSlowsWhenShortHanded()
        {
            // 공사 인원 4명, 주민 없음(주택 안 지음) → 기본 인부 2명뿐 = 50% → 공사 시간 두 배.
            Settlement s = NewSettlement(c => c.BuildingLevels[BuildingKinds.Warehouse][0].BuildWorkers = 4);
            Assert.AreEqual(2, s.Rules.BaseCrew, 1e-12, "건설 슬롯 2 × 1명");
            Assert.IsTrue(s.RequestBuild(BuildingKinds.Warehouse, HouseTile).Success);
            ConstructionJob job = s.Jobs.Single();
            Assert.AreEqual(2, s.LastLabor.OfJob(job.Id), 1e-12);
            Assert.AreEqual(0.5, s.StaffRatio(job), 1e-12);
            int planned = job.Hours;
            long started = job.StartedHour;
            int hours = 0;
            while (s.FacilityAt(HouseTile).State != FacilityState.Operating && hours < 50) { s.AdvanceHour(); hours++; }
            // 정각 명령이라 다 찬 인원이면 planned시간 뒤 정각(그 앞 시간의 정산)에 완공. 절반이면 일한 양이 planned가 될 때까지 약 두 배
            // (지연은 시간마다 (1 − 속도)만큼 완공 시각을 늦추고, 마지막 시간은 늦추지 않는다 — 결정 JQ4).
            Assert.AreEqual(2 * planned - 1, s.FacilityAt(HouseTile).CompletedHour - started, 1, "절반 인원이면 약 두 배 걸린다");
            Assert.Greater(s.FacilityAt(HouseTile).CompletedHour - started, planned, "다 찬 인원보다 늦다");
        }

        [Test]
        public void Construction_TakesWorkersFromFacilitiesInReverseOrder()
        {
            // 우물 둘(각 3명) + 주민 6 → 다 참. 창고 공사(4명, 기본 인부 2명 + 주민 2명)가 나중 우물에서 2명을 데려간다.
            Settlement s = NewSettlement(c =>
            {
                c.BuildingLevels[BuildingKinds.Well][0].Workers = 3;
                c.BuildingLevels[BuildingKinds.Warehouse][0].BuildWorkers = 4;
            }, r => r.FreeBuilds[BuildingKinds.Well] = 2);
            s.RequestBuild(BuildingKinds.House, HouseTile);
            s.RequestBuild(BuildingKinds.Well, WellTile);
            s.RequestBuild(BuildingKinds.Well, SecondWell);
            Facility first = s.FacilityAt(WellTile), second = s.FacilityAt(SecondWell);
            TestData.MoveIn(s);
            Assert.IsTrue(s.RequestBuild(BuildingKinds.Warehouse, FarTile).Success);
            Assert.AreEqual(4, s.LastLabor.OfJob(s.Jobs.Single().Id), 1e-9, "공사가 먼저 받는다");
            Assert.AreEqual(3, s.LastLabor.OfFacility(first.Id), 1e-9, "먼저 배치된 시설은 지킨다");
            Assert.AreEqual(1, s.LastLabor.OfFacility(second.Id), 1e-9, "배치 순서의 역순으로 비운다");
        }

        [Test]
        public void Valuation_AddsPopulationValue_AndHouseInfraByOccupancy()
        {
            // 결정 CL1·CL2: 주민 가치 = 사는 인원 × 1인 가치 × 입찰자 가중치, 주택은 거주율만큼 인프라 배율.
            Settlement s = NewSettlement(c => c.Labor.StartGraceHours = 1);
            s.RequestBuild(BuildingKinds.House, HouseTile);
            TestData.MoveIn(s); // 유예 안에 다 참
            Facility house = s.FacilityAt(HouseTile);
            for (int i = 0; i < 24 && house.Residents >= Capacity(s); i++) s.AdvanceHour(); // 유예가 끝나 물이 없어 한 명이 다 떠날 때까지 → 거주율 < 1
            double occupancy = house.Residents / Capacity(s);
            Assert.Less(occupancy, 1);
            AuctionSeed seed = s.Rules.Config.AuctionSeed;
            BidderProfile colonist = s.Rules.Config.Auction.Bidders.Single(b => b.Id == "colonist");
            ValuationResult v = SettlementValuation.EvaluateFor(s, s.CurrentHour, colonist);
            Assert.AreEqual(s.Residents * seed.ValuePerResident * colonist.PopulationWeight, v.PopulationValue, 1e-9);
            double expectedInfra = 1 + occupancy * seed.Infra.PerOperationalHouse * colonist.InfraWeight;
            Assert.AreEqual(expectedInfra, v.InfraMultiplier, 1e-9, "주택 하나 × 거주율");
            ValuationResult neutral = SettlementValuation.Evaluate(s, s.CurrentHour);
            Assert.Greater(v.PopulationValue, neutral.PopulationValue, "이주 사업가가 주민을 더 크게 본다");
        }

        [Test]
        public void ColonistPerk_IsAnEmptyFreeHouse()
        {
            // 결정 CL3·HI4: 이주 사업가 혜택(다음 정착지 무료 주택 1채)은 공사 없이 서지만 빈 채로 생긴다.
            Settlement s = NewSettlement(tweak: r => r.FreeBuilds[BuildingKinds.House] = 1, instantHouse: false);
            Assert.AreEqual(1, s.FreeBuildsRemaining(BuildingKinds.House));
            Assert.IsTrue(s.RequestBuild(BuildingKinds.House, HouseTile).Success);
            Facility house = s.FacilityAt(HouseTile);
            Assert.AreEqual(FacilityState.Operating, house.State, "공사 없이 바로");
            Assert.AreEqual(0, house.Residents, 1e-12, "빈 채로 생긴다");
        }

        [Test]
        public void V08Regression_HasNoLabor()
        {
            EconomyConfig config = TestData.Economy();
            SettlementRules rules = SettlementRules.ForV08Regression(config);
            Assert.IsFalse(rules.LaborOn);
            Assert.AreEqual(0, rules.BaseCrew, 1e-12);
            var map = new HexMap(3);
            var s = new Settlement(map, rules);
            Assert.AreEqual(0, s.FreeBuildsRemaining(BuildingKinds.House));
            Assert.IsFalse(s.CanBuild(BuildingKinds.House, HouseTile).Success, "노동력이 꺼져 있으면 주택 없음");
        }
    }
}
