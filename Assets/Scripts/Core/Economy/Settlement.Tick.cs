using System;
using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Canal;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Hex;
using DesertSettlement.Core.Water;

namespace DesertSettlement.Core.Economy
{
    /// <summary>
    /// 경제 틱(<see cref="AdvanceHour"/>). 착공·완공·공사 지연은 Settlement.Jobs.cs, 정착지 전체 설명은 Settlement.cs.
    /// </summary>
    public sealed partial class Settlement
    {
        // ───────────────────────── 경제 틱 ─────────────────────────

        /// <summary>현재 게임 시간(<see cref="CurrentHour"/>)을 정산하고 다음 시간으로 넘어간다.</summary>
        public HourRecord AdvanceHour()
        {
            long h = CurrentHour;
            Disasters?.BeginHour(h); // 재해 예고·시작·끝
            CompleteJobsUntil(h + 1); // 이 시간 안(정각 포함)에 완공 시각이 든 공사. 게임에서는 대개 그 분에 이미 완공됨(결정 JQ4)
            LastLabor = AssignLabor(); // 이번 시간 공사 인원(공사가 시설보다 먼저 받는다, 결정 LB1~)
            SlowJobs(h);             // 모래폭풍·공사 인원 부족이면 남은 공사의 완공 시각을 늦춘다
            if (!Rules.StartJobsOnCommand) StartJobs(h + 1, h); // 원본 v0.8: 정각 착공은 생산 전에
            LastLabor = AssignLabor(); // 완공·착공을 반영한 이번 시간 배치(우물 방출·생산이 쓴다)
            WaterAllocation water = SolveWater();

            // 생산: 1차 재화(곡물·과일·향신료)·석재·광물은 바로, 가공 시설은 주민이 먹은 뒤(결정 FD4).
            double demand = 0, upkeep = 0, food = 0, stone = 0, ore = 0;   // food = 순 식량 가치(곡물 1 기준, 결정 FD7)
            double foodValued = 0, stoneValued = 0, oreValued = 0; // 가동 숙성 반영(경매 가치용)
            var produced = new double[GoodsCatalog.All.Length];
            var processors = new List<(Facility f, double capacity, double maturity)>();
            bool wateredFarm = false;
            foreach (Facility facility in _facilities.Values.OrderBy(x => x.Id))
            {
                if (facility.State == FacilityState.Constructing || facility.State == FacilityState.Demolishing) continue;
                BuildingLevel lv = LevelOf(facility);
                if (facility.State == FacilityState.Operating)
                {
                    if (!Rules.LaborOn) demand += lv.FoodUse ?? 0; // 원본 v0.8: 시설이 먹는다(노동력이 켜지면 아래에서 주민이 먹는다)
                    upkeep += lv.Upkeep ?? 0;
                    double rate = (lv.WaterDemand ?? 0) > 0 ? water.RateOf(WaterIds.Of(facility)) : 1.0;
                    double maturity = Rules.Maturity(h - facility.CompletedHour);
                    double amount = ProductionPerHour(facility, rate, WorkforceOf(facility));
                    if (SettlementRules.InputOf(facility.Kind) != null)
                    {
                        processors.Add((facility, amount, maturity));
                        continue;
                    }
                    switch (SettlementRules.ResourceOf(facility.Kind))
                    {
                        case ResourceKind.Food:
                        case ResourceKind.Goods:
                            Goods g = SettlementRules.GoodsOf(facility.Kind).Value;
                            AddGoods(g, amount);
                            produced[(int)g] += amount;
                            double value = Rules.NetFoodValue(facility.Kind, amount, lv);
                            food += value;
                            foodValued += value * maturity;
                            if (facility.Kind == BuildingKinds.Farm && rate > 0) wateredFarm = true;
                            break;
                        case ResourceKind.Stone:
                            stone += amount;
                            stoneValued += amount * maturity;
                            break;
                        case ResourceKind.Ore:
                            ore += amount;
                            oreValued += amount * maturity;
                            break;
                    }
                }
                else
                {
                    upkeep += Rules.ClosedUpkeepRatio * (lv.Upkeep ?? 0); // 폐쇄·개량 중
                }
            }

            // 먹기·마시기(주민이 먼저), 그다음 남은 원료로 가공.
            if (Rules.LaborOn) demand = (Residents + Rules.BaseCrew) * FoodPerResident; // 주민·기본 인부가 먹는다
            var eaten = new double[GoodsCatalog.All.Length];
            double fed = Eat(demand, eaten);
            double drinkDemand = Rules.LaborOn ? Residents * Rules.Labor.DrinkPerResidentPerHour : 0;
            double drunkTotal = Drink(drinkDemand, eaten);
            double deficit = Math.Max(0, demand - fed);
            double foodRatio = demand > 0 ? Math.Min(1, fed / demand) : 1; // 식량 충족률(주민 목표 인원)
            FoodSatisfaction = foodRatio;
            foreach ((Facility f, double capacity, double maturity) in processors)
                Process(f, capacity, produced, ref food, ref foodValued, maturity);
            double spoiled = Spoil(); // 식품 부패(창고 밀집이 줄임, 결정 DC1·DC2)

            upkeep *= Rules.UpkeepMultiplier;
            Cash -= upkeep;
            if (Cash < 0 && FirstInsolvencyHour == null) FirstInsolvencyHour = h;

            // 저장: 소비 먼저, 넘치면 보관 순위가 낮은 재화부터 시장에 자동 판매하고 남는 몫은 버린다(결정 AS1). FoodValid·StoneValid는 버려진 몫만 뺀다.
            double stoneCap = StoneCapacity;
            double foodValid = Math.Max(0, food - SellOrDiscardOverflow());

            double stoneOverflow = Math.Max(0, Stone + stone - stoneCap);
            double stoneDiscardedBefore = StoneDiscarded;
            SellOrDiscardStone(stoneOverflow);
            double stoneValid = Math.Max(0, stone - (StoneDiscarded - stoneDiscardedBefore));
            Stone = Math.Min(stoneCap, Stone + stone);

            TotalFoodShortage += deficit;
            if (deficit > 1e-9 && FirstShortageHour == null) FirstShortageHour = h;
            if (Rules.LaborOn)
            {
                UpdateLiving(eaten, foodRatio, water); // 생활 수준(향신료 포함, 결정 LS1~·SP1~)
                UpdateResidents(water, foodRatio);     // 물·식량이 모자란 주택에서 주민이 떠나고, 충분하면 돌아온다(생활 수준이 속도를 바꿈)
            }
            else if (deficit > 1e-9)
            {
                // 원본 v0.8: 다음 시간의 F.
                double intensity = Math.Min(1, deficit / (Rules.FoodDeficitFullIntensityRatio * demand));
                FoodCoefficient = Math.Max(0, FoodCoefficient - Rules.FoodDeclinePerDayMax / 24.0 * intensity);
            }
            else if (demand > 0 && wateredFarm)
            {
                FoodCoefficient = Math.Min(1, FoodCoefficient + Rules.FoodRecoveryPerDay / 24.0);
            }

            var record = new HourRecord
            {
                Hour = h, Cash = Cash, Food = Food, Stone = Stone, FoodCoefficient = FoodCoefficient,
                OreProduced = ore, FoodProduced = food, FoodValid = foodValid, StoneProduced = stone, StoneValid = stoneValid,
                FoodDemand = demand, FoodDeficit = deficit, Upkeep = upkeep,
                GoodsProduced = produced, GoodsConsumed = eaten, DrinkDemand = drinkDemand, Drunk = drunkTotal,
                OreValued = oreValued, FoodValued = foodValued, StoneValued = stoneValued,
                EpidemicSickShare = Disasters?.Epidemic.SickPeopleShare ?? 0,
                Residents = Residents, WorkersAssigned = LastLabor.Assigned, WorkersNeeded = LastLabor.Needed,
                LivingStandard = LivingStandard, LivingTarget = LivingTarget,
                AutoSaleIncome = _autoIncomeSinceRecord, Discarded = _discardedSinceRecord, Spoiled = spoiled,
            };
            _autoIncomeSinceRecord = 0;
            _discardedSinceRecord = 0;
            _history.Add(record);
            RecoverPrices();         // 시장 가격 회복(2026-10-27 이슈 #18)
            Disasters?.EndHour(h); // 전염병 새 환자·회복(주민 떠남·돌아옴을 반영한 뒤)
            CurrentHour = h + 1;
            Minute = 0;
            // 이번 정산으로 석재가 찼으면 이 정각에 바로 착공한다(결정 JQ4). 다음 정산까지 한 시간 더 기다리지 않는다.
            if (Rules.StartJobsOnCommand) StartJobs(Now, h);
            HourSettled?.Invoke(record);
            // 화면 표시용 배치: 이번 정산에서 들어오고 떠난 주민·새 착공을 반영(경매 가치는 위 이벤트에서 이번 시간 배치로 이미 셈, 다음 정산은 새로 계산).
            if (Rules.LaborOn) LastLabor = AssignLabor();
            return record;
        }

        /// <summary>여러 시간을 연속 정산한다(테스트·시뮬레이션용).</summary>
        public void AdvanceHours(int hours)
        {
            for (int i = 0; i < hours; i++) AdvanceHour();
        }
    }
}
