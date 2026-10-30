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
    /// 경제 틱(<see cref="AdvanceHour"/>)과 작업 큐 진행(완공·착공·모래폭풍 지연). 정착지 전체 설명은 Settlement.cs.
    /// </summary>
    public sealed partial class Settlement
    {
        // ───────────────────────── 경제 틱 ─────────────────────────

        /// <summary>현재 게임 시간(<see cref="CurrentHour"/>)을 정산하고 다음 시간으로 넘어간다.</summary>
        public HourRecord AdvanceHour()
        {
            long h = CurrentHour;
            Disasters?.BeginHour(h); // 재해 예고·시작·끝
            LastLabor = AssignLabor(); // 이번 시간 공사 인원(공사가 시설보다 먼저 받는다, 결정 LB1~)
            SlowJobs(h);             // 모래폭풍·공사 인원 부족이면 공사 속도 감소
            CompleteJobs(h);
            StartJobs(h);
            LastLabor = AssignLabor(); // 완공·착공을 반영한 이번 시간 배치(우물 방출·생산이 쓴다)
            WaterAllocation water = SolveWater();

            double demand = 0, upkeep = 0, food = 0, stone = 0, ore = 0;
            double foodValued = 0, stoneValued = 0, oreValued = 0; // 가동 숙성 반영(경매 가치용)
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
                    switch (SettlementRules.ResourceOf(facility.Kind))
                    {
                        case ResourceKind.Food:
                            food += amount;
                            foodValued += amount * maturity;
                            if (rate > 0) wateredFarm = true;
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

            if (Rules.LaborOn) demand = (Residents + Rules.BaseCrew) * Rules.Labor.FoodPerResidentPerHour; // 주민·기본 인부가 먹는다
            double foodRatio = demand > 0 ? Math.Min(1, (Food + food) / demand) : 1; // 식량 충족률(주민 목표 인원)
            FoodSatisfaction = foodRatio;

            upkeep *= Rules.UpkeepMultiplier;
            Cash -= upkeep;
            if (Cash < 0 && FirstInsolvencyHour == null) FirstInsolvencyHour = h;

            // 저장: 소비 먼저, 남은 생산은 저장 한도까지만 유효.
            double foodCap = FoodCapacity, stoneCap = StoneCapacity;
            double deficit = Math.Max(0, demand - (Food + food));
            double foodAfter = Math.Max(0, Food + food - demand);
            double foodOverflow = Math.Max(0, foodAfter - foodCap);
            double foodValid = Math.Max(0, food - foodOverflow);
            FoodDiscarded += foodOverflow;
            Food = Math.Min(foodCap, foodAfter);

            double stoneOverflow = Math.Max(0, Stone + stone - stoneCap);
            double stoneValid = Math.Max(0, stone - stoneOverflow);
            StoneDiscarded += stoneOverflow;
            Stone = Math.Min(stoneCap, Stone + stone);

            TotalFoodShortage += deficit;
            if (deficit > 1e-9 && FirstShortageHour == null) FirstShortageHour = h;
            if (Rules.LaborOn)
            {
                UpdateResidents(water, foodRatio); // 물·식량이 모자란 주택에서 주민이 떠나고, 충분하면 돌아온다
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
                OreValued = oreValued, FoodValued = foodValued, StoneValued = stoneValued,
                EpidemicSickShare = Disasters?.Epidemic.SickShare ?? 0,
                Residents = Residents, WorkersAssigned = LastLabor.Assigned, WorkersNeeded = LastLabor.Needed,
            };
            _history.Add(record);
            RecoverPrices();         // 시장 가격 회복(2026-10-27 이슈 #18)
            Disasters?.EndHour(h); // 전염병 새 환자·회복(주민 떠남·돌아옴을 반영한 뒤)
            CurrentHour = h + 1;
            HourSettled?.Invoke(record);
            return record;
        }

        /// <summary>여러 시간을 연속 정산한다(테스트·시뮬레이션용).</summary>
        public void AdvanceHours(int hours)
        {
            for (int i = 0; i < hours; i++) AdvanceHour();
        }

        private void CompleteJobs(long h)
        {
            foreach (ConstructionJob job in _jobs.Where(j => j.State == JobState.InProgress && j.DeadlineHour == h).OrderBy(j => j.Id).ToList())
            {
                _jobs.Remove(job);
                Facility f = GetFacility(job.FacilityId);
                switch (job.Type)
                {
                    case JobType.Build:
                        f.State = FacilityState.Operating;
                        f.CompletedHour = h;
                        f.ActiveSinceHour = h;
                        if (f.Kind == BuildingKinds.AuctionHall && AuctionHallCompletedHour == null) AuctionHallCompletedHour = h;
                        FillHouse(f);
                        FacilityCompleted?.Invoke(f);
                        break;
                    case JobType.Upgrade:
                        double capacityBefore = IsHouse(f) ? HouseCapacity(f) : 0;
                        f.Level = job.TargetLevel;
                        f.State = FacilityState.Operating;
                        f.ActiveSinceHour = h;
                        FillHouse(f, capacityBefore);
                        FacilityCompleted?.Invoke(f);
                        break;
                    case JobType.Demolish:
                        RemoveFacility(f.Id);
                        _stateBeforeDemolish.Remove(f.Id);
                        AddStone(f.StoneInvested * Rules.DemolishRefundRatio);
                        break;
                }
            }
        }

        /// <summary>
        /// 대기 작업을 슬롯·석재가 되는 만큼 착공한다. 정각 정산(h = 지금 정산하는 시간)과 명령 직후(onCommand, h = 진행 중인 시간)에 부른다.
        /// 명령 직후 착공은 진행 중인 이 시간을 첫 1시간으로 세서 완공 정산이 한 시간 이르다(JQ2): 12:30에 5시간 작업 → 17:00 완공.
        /// 정각 착공은 정산하는 시간이 이미 지나간 시간이라 그다음 정각부터 센다(예전과 같음).
        /// </summary>
        private void StartJobs(long h, bool onCommand = false)
        {
            int running = JobsInProgress;
            bool blocked = false;
            foreach (ConstructionJob job in _jobs.Where(j => j.State == JobState.Queued).ToList())
            {
                if (running >= Rules.ConstructionSlots) break;
                // C2 확정: FIFO. 앞 작업이 막히면 뒤 건설·개량도 기다린다.
                // 2026-10-26 JQ1(이슈 #3): 철거는 석재가 들지 않으므로 막힌 작업을 건너뛰어 시작한다(빈 슬롯이 있을 때).
                if (blocked && job.Type != JobType.Demolish) continue;
                if (!CanStart(job))
                {
                    blocked = true;
                    continue;
                }

                Stone -= job.StoneCost;
                job.State = JobState.InProgress;
                job.StartedHour = h;
                job.DeadlineHour = h + job.Hours - (onCommand ? 1 : 0);
                switch (job.Type)
                {
                    case JobType.Build:
                        var f = new Facility
                        {
                            Id = _nextFacilityId++, Kind = job.Kind, Tile = job.Tile, Edge = job.Edge,
                            State = FacilityState.Constructing, StoneInvested = job.StoneCost,
                        };
                        _facilities.Add(f.Id, f);
                        job.FacilityId = f.Id;
                        break;
                    case JobType.Upgrade:
                        Facility up = GetFacility(job.FacilityId);
                        up.State = FacilityState.Upgrading;
                        up.StoneInvested += job.StoneCost;
                        break;
                    case JobType.Demolish:
                        Facility down = GetFacility(job.FacilityId);
                        _stateBeforeDemolish[down.Id] = down.State;
                        down.State = FacilityState.Demolishing;
                        down.Residents = 0; // 철거하는 주택의 주민은 떠난다
                        break;
                }
                running++;
                JobStarted?.Invoke(job);
            }
        }

        /// <summary>
        /// 공사 속도가 1보다 느린 시간에는 진행 중 작업의 완공 시각을 늦춘다: 시간마다 (1 − 속도)씩 쌓아 1이 되면 한 시간 늦춤.
        /// 속도 = 모래폭풍 공사 속도(이번 시간에 막 시작한 작업은 다음 시간부터, 2026-10-23 D1) × 공사 배치율(결정 LB1~, 0명이면 멈춤).
        /// 명령 순간 착공한 작업은 이 시간을 첫 1시간으로 세므로 배치율도 이 시간부터 걸린다.
        /// </summary>
        private void SlowJobs(long h)
        {
            double storm = Disasters?.ConstructionSpeed ?? 1;
            bool stormy = storm < 1 - 1e-12;
            if (!stormy && !Rules.LaborOn) return;
            foreach (ConstructionJob job in _jobs.Where(j => j.State == JobState.InProgress))
            {
                double speed = (stormy && job.StartedHour < h ? storm : 1) * (Rules.LaborOn ? StaffRatio(job) : 1);
                if (speed >= 1 - 1e-12) continue;
                job.DelayCarry += 1 - speed;
                while (job.DelayCarry >= 1 - 1e-9)
                {
                    job.DeadlineHour++;
                    job.DelayCarry -= 1;
                }
            }
        }

        private bool CanStart(ConstructionJob job)
        {
            if (Stone + 1e-8 < job.StoneCost) return false;
            switch (job.Type)
            {
                case JobType.Upgrade:
                    return GetFacility(job.FacilityId)?.State == FacilityState.Operating;
                case JobType.Demolish:
                    Facility f = GetFacility(job.FacilityId);
                    return f != null && (f.State == FacilityState.Operating || f.State == FacilityState.Closed) && StorageProblemIfRemoved(f) == null;
                default:
                    return true;
            }
        }
    }
}
