using System;
using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Water;

namespace DesertSettlement.Core.Economy
{
    /// <summary>
    /// 주택·노동력(결정 LB1~). 노동력 규칙이 꺼져 있으면(<see cref="SettlementRules.Labor"/> null, 원본 v0.8) 아무 일도 하지 않는다.
    /// - 사람은 1명 단위(결정 PI1): 움직이는 값은 진행도(<see cref="Facility.ResidentFill"/>)이고 주민 수는 1명분이 다 차거나 다 빠질 때만 바뀐다.
    /// - 주택에 주민이 산다: 모든 주택(무료 배치 포함)은 빈 채로 시작해 유입 속도대로 차고, 개량으로 늘어난 정원도 그렇다(결정 HI1), 물(그 주택의 수로 공급률)·식량(정착지 전체 충족률)이 모자라면
    ///   목표 인원(정원 × 둘 중 작은 값)까지 서서히 떠나고, 충분해지면 더 천천히 들어온다(속도는 생활 수준이 크게 바꿈)(<see cref="UpdateResidents"/>).
    ///   정착지 시작 유예(<c>labor.startGraceHours</c>, 결정 BQ3) 동안은 물 부족을 세지 않는다(<see cref="FeltWaterRate"/>).
    /// - 매시간 배치(<see cref="AssignLabor"/>): 공사(대기열 순서, 기본 인부 먼저) → 우선 고용 목록 → 물 100% 시설(완공 순) → 물이 일부만 오는 시설(완공 순).
    ///   물 0% 시설에는 자동 배치하지 않는다. 공사가 먼저라 남는 사람이 없으면 시설은 배치 순서의 역순으로 비게 된다.
    /// - 생산 = 최대 × 물 공급률 × 배치율, 우물 방출 × 배치율, 공사는 모인 비율만큼 느리게(<see cref="SlowJobs"/>).
    /// 정착지 전체 설명은 Settlement.cs.
    /// </summary>
    public sealed partial class Settlement
    {
        private readonly List<int> _hirePriority = new List<int>();

        /// <summary>우선 고용 목록(시설 id, 0 = 1순위). 마지막에 누른 시설이 맨 앞.</summary>
        public IReadOnlyList<int> HirePriority => _hirePriority;

        /// <summary>가장 최근의 인원 배치(정각 정산과 명령 직후에 갱신). 노동력이 꺼져 있으면 비어 있다.</summary>
        public LaborAssignment LastLabor { get; private set; } = new LaborAssignment();

        /// <summary>직전 정산의 식량 충족률(0~1) = min(1, (재고 + 생산) ÷ 수요). 주민 목표 인원에 쓴다.</summary>
        public double FoodSatisfaction { get; private set; } = 1;

        /// <summary>모든 주택 주민 합(기본 인부 제외, 온전한 사람 수, 결정 PI1).</summary>
        public double Residents => _facilities.Values.Where(IsHouse).Sum(f => f.Residents);

        /// <summary>모든 주택의 정원 합(공사 중 제외).</summary>
        public double HousingCapacity => _facilities.Values.Where(f => IsHouse(f) && HasResidents(f)).Sum(HouseCapacity);

        private static bool IsHouse(Facility f) => f.Kind == BuildingKinds.House;

        /// <summary>주민이 살 수 있는 상태(운영·개량 중). 공사·철거 중이면 비어 있다.</summary>
        private static bool HasResidents(Facility f) => f.State == FacilityState.Operating || f.State == FacilityState.Upgrading;

        /// <summary>주택 정원(지금 레벨).</summary>
        public double HouseCapacity(Facility house) => LevelOf(house).Residents ?? 0;

        /// <summary>정원이 cap인 주택에 시간당 들어오는 주민 수 = 정원 × labor.returnPerHour × 생활 수준 배율(<see cref="ReturnFactor"/>, 결정 HI1~) × 유입 모디파이어.</summary>
        public double ArrivalPerHour(double cap) => Rules.LaborOn ? Rules.Labor.ReturnPerHour * ReturnFactor * Rules.Effects.ArrivalMultiplier * cap : 0;

        /// <summary>주민(·기본 인부) 1인당 시간당 식량 = labor.foodPerResidentPerHour × 1인당 식량 모디파이어(결정 BM1).</summary>
        public double FoodPerResident => Rules.LaborOn ? Rules.Labor.FoodPerResidentPerHour * Rules.Effects.FoodPerResidentMultiplier : 0;

        /// <summary>이 주택에 시간당 들어오는 주민 수 = <see cref="ArrivalPerHour(double)"/> × (1 + 주택 밀집 보너스, 결정 AJ3).</summary>
        public double ArrivalPerHour(Facility house) => ArrivalPerHour(HouseCapacity(house)) * (1 + HouseArrivalBonus(house.Tile));

        /// <summary>시설 운영에 필요한 인원(레벨표 <c>workers</c>). 노동력이 꺼져 있거나 사람이 필요 없으면 0.</summary>
        public double WorkersNeeded(Facility f) => Rules.LaborOn ? LevelOf(f).Workers ?? 0 : 0;

        /// <summary>작업에 필요한 인원(레벨표 <c>buildWorkers</c>).</summary>
        public double WorkersNeeded(ConstructionJob job) => Rules.JobWorkers(job.Type, job.Kind, job.TargetLevel);

        /// <summary>시설 배치율(배치 ÷ 필요, 0~1). 사람이 필요 없으면 1.</summary>
        public double StaffRatio(Facility f)
        {
            double need = WorkersNeeded(f);
            return need <= 0 ? 1 : Math.Min(1, LastLabor.OfFacility(f.Id) / need);
        }

        /// <summary>작업 배치율(배치 ÷ 필요, 0~1). 공사 속도에 곱한다. 사람이 필요 없으면 1.</summary>
        public double StaffRatio(ConstructionJob job)
        {
            double need = WorkersNeeded(job);
            return need <= 0 ? 1 : Math.Min(1, LastLabor.OfJob(job.Id) / need);
        }

        /// <summary>생산 효율의 일손: 노동력이 켜져 있으면 배치율, 꺼져 있으면 식량 계수 F(원본 v0.8).</summary>
        private double WorkforceOf(Facility f) => Rules.LaborOn ? StaffRatio(f) : FoodCoefficient;

        /// <summary>
        /// 경매 개발 중 자산이 쓰는 일손. 공사 중인 시설은 다 차는 것으로 가정하고(완공 가정 물 배분과 같은 생각),
        /// 운영 중인 시설은 지금 배치율. 노동력이 꺼져 있으면 F.
        /// </summary>
        public double ExpectedWorkforce(Facility f, bool underConstruction) =>
            !Rules.LaborOn ? FoodCoefficient : underConstruction ? 1 : StaffRatio(f);

        /// <summary>
        /// 주택의 물 수요 = 1인당 물 × (사는 인원 + 이번 시간 돌아올 수 있는 인원, 정원까지).
        /// 돌아올 몫을 넣어야 빈 주택도 물이 오는지 알 수 있다. assumeComplete면 정원 전부(개발 중 자산 평가용).
        /// </summary>
        private double HouseWaterDemand(Facility house, int level, bool assumeComplete)
        {
            LaborSettings l = Rules.Labor;
            double cap = Rules.Level(BuildingKinds.House, level).Residents ?? 0;
            double people = assumeComplete ? cap : Math.Min(cap, house.ResidentFill + ArrivalPerHour(cap) * (1 + HouseArrivalBonus(house.Tile)));
            return l.WaterPerResidentPerHour * people;
        }

        /// <summary>주택의 물 공급률(최근 물 배분). 물을 쓰지 않는 설정이면 1, 공사·철거 중이면 0.</summary>
        public double HouseWaterRate(Facility house)
        {
            if (!HasResidents(house)) return 0;
            if (Rules.Labor.WaterPerResidentPerHour <= 0) return 1;
            return LastWater?.RateOf(WaterIds.Of(house)) ?? 0;
        }

        /// <summary>정착지 시작 유예가 남은 시간(결정 BQ3). 노동력이 꺼져 있거나 유예가 끝났으면 0.</summary>
        public double StartGraceHoursLeft => Rules.LaborOn ? Math.Max(0, Rules.Labor.StartGraceHours - CurrentHour) : 0;

        /// <summary>
        /// 주민이 느끼는 주택 물 공급률: 물을 쓰지 않는 설정이거나 시작 유예 중이면 1, 아니면 그 물 배분의 공급률(배분이 없으면 0).
        /// 떠남(<see cref="UpdateResidents"/>)·생활 수준 부족(<see cref="ResidentWaterRate"/>)·떠나는 중 표시가 함께 쓴다.
        /// </summary>
        private double FeltWaterRate(Facility house, WaterAllocation water) =>
            Rules.Labor.WaterPerResidentPerHour <= 0 || StartGraceHoursLeft > 0 ? 1 : water?.RateOf(WaterIds.Of(house)) ?? 0;

        /// <summary>주택 주민이 떠나는 중인지(최근 물 공급률·식량 충족률로 본 목표보다 많음).</summary>
        public bool IsLeaving(Facility house) =>
            Rules.LaborOn && IsHouse(house) && HasResidents(house)
            && house.ResidentFill > HouseCapacity(house) * Math.Min(FeltWaterRate(house, LastWater), FoodSatisfaction) + 1e-6;

        /// <summary>배치 순서를 정할 때 보는 물 공급률: 직전 물 배분 기준. 물을 쓰지 않거나(우물) 아직 배분 기록이 없으면(막 가동) 1.</summary>
        private double OrderWaterRate(Facility f)
        {
            if ((LevelOf(f).WaterDemand ?? 0) <= 0) return 1;
            return LastWater != null && LastWater.Consumers.TryGetValue(WaterIds.Of(f), out WaterConsumerResult r) ? r.Rate : 1;
        }

        /// <summary>
        /// 시설 자동 배치 순서: 우선 고용 목록(물 조건 무시) → 물 100%(완공 순) → 물이 일부만(완공 순). 물 0% 시설은 빠진다.
        /// '완공 순'은 처음 운영을 시작한 시각(개량해도 그대로), 같으면 시설 번호 순.
        /// </summary>
        public IEnumerable<Facility> HiringOrder()
        {
            if (!Rules.LaborOn) return Enumerable.Empty<Facility>();
            List<Facility> eligible = _facilities.Values.Where(f => f.IsOperating && WorkersNeeded(f) > 0).ToList();
            List<Facility> prioritized = _hirePriority.Select(GetFacility).Where(f => f != null && eligible.Contains(f)).ToList();
            List<Facility> rest = eligible.Except(prioritized).OrderBy(f => f.CompletedHour).ThenBy(f => f.Id).ToList();
            IEnumerable<Facility> full = rest.Where(f => OrderWaterRate(f) >= 1 - 1e-9);
            IEnumerable<Facility> partial = rest.Where(f => OrderWaterRate(f) > 1e-9 && OrderWaterRate(f) < 1 - 1e-9);
            return prioritized.Concat(full).Concat(partial).ToList();
        }

        /// <summary>이번 시간 인원 배치를 계산한다(상태를 바꾸지 않음). 노동력이 꺼져 있으면 빈 배치.</summary>
        private LaborAssignment AssignLabor()
        {
            var a = new LaborAssignment();
            if (!Rules.LaborOn) return a;
            a.Crew = Rules.BaseCrew;
            a.Residents = Residents;
            a.Sick = Disasters?.SickResidents ?? 0; // 아픈 주민은 일하지 못한다(전염병, 결정 EV2)
            double crew = a.Crew, people = Math.Max(0, a.Residents - a.Sick);

            foreach (ConstructionJob job in _jobs.Where(j => j.State == JobState.InProgress))
            {
                double need = WorkersNeeded(job);
                a.Needed += need;
                double fromCrew = Math.Min(need, crew);
                crew -= fromCrew;
                double fromPeople = Math.Min(need - fromCrew, people);
                people -= fromPeople;
                if (fromCrew + fromPeople > 0) a.Jobs[job.Id] = fromCrew + fromPeople;
                a.Assigned += fromCrew + fromPeople;
            }

            a.Needed += _facilities.Values.Where(f => f.IsOperating).Sum(WorkersNeeded);
            foreach (Facility f in HiringOrder())
            {
                double give = Math.Min(WorkersNeeded(f), people);
                if (give <= 0) break;
                people -= give;
                a.Facilities[f.Id] = give;
                a.Assigned += give;
            }
            a.IdleResidents = people;
            return a;
        }

        /// <summary>
        /// 정산 끝에 주민 수를 고친다. 목표 = 정원 × min(그 주택 물 공급률, 식량 충족률).
        /// 많으면 시간당 정원 × leavePerHour씩 떠나고, 적으면 정원 × returnPerHour씩 돌아온다(개량 중엔 들어오지 않음).
        /// 움직이는 것은 진행도(ResidentFill)이고, 주민 수는 1명분이 다 차거나 빠질 때만 바뀐다(결정 PI1).
        /// </summary>
        private void UpdateResidents(WaterAllocation water, double foodRatio)
        {
            LaborSettings l = Rules.Labor;
            foreach (Facility house in _facilities.Values.Where(IsHouse).OrderBy(f => f.Id))
            {
                if (!HasResidents(house))
                {
                    house.Residents = house.ResidentFill = 0;
                    continue;
                }
                double cap = HouseCapacity(house);
                double target = cap * Math.Min(FeltWaterRate(house, water), foodRatio);
                double fill = house.ResidentFill;
                if (fill > target) fill = Math.Max(target, fill - l.LeavePerHour * LeaveFactor * cap);
                else if (house.State == FacilityState.Operating) fill = Math.Min(target, fill + ArrivalPerHour(house));
                house.ResidentFill = fill;
                house.Residents = WholePeople.Next(house.Residents, fill);
            }
        }

        /// <summary>시설이 사라질 때(철거 완료·건설 취소) 우선 고용 목록에서도 뺀다.</summary>
        private void RemoveFacility(int facilityId)
        {
            _facilities.Remove(facilityId);
            _hirePriority.Remove(facilityId);
        }

        // ── 우선 고용 명령(Settlement.Commands.cs가 기록과 함께 부른다) ──

        public CommandResult CanPrioritizeHiring(int facilityId)
        {
            Facility f = GetFacility(facilityId);
            if (f == null) return CommandResult.Fail("시설이 없습니다.");
            if (WorkersNeeded(f) <= 0) return CommandResult.Fail("사람이 필요 없는 시설입니다.");
            if (f.State == FacilityState.Demolishing) return CommandResult.Fail("철거 중인 시설입니다.");
            if (_hirePriority.Count > 0 && _hirePriority[0] == facilityId) return CommandResult.Fail("이미 우선 고용 1순위입니다.");
            return CommandResult.Ok();
        }

        public CommandResult CanClearHiringPriority(int facilityId) =>
            _hirePriority.Contains(facilityId) ? CommandResult.Ok() : CommandResult.Fail("우선 고용 목록에 없습니다.");

        /// <summary>우선 고용: 이 시설을 목록 맨 앞(1순위)으로. 전에 누른 시설은 한 칸씩 밀린다.</summary>
        private CommandResult DoPrioritizeHiring(int facilityId)
        {
            CommandResult check = CanPrioritizeHiring(facilityId);
            if (!check.Success) return check;
            _hirePriority.Remove(facilityId);
            _hirePriority.Insert(0, facilityId);
            return CommandResult.Ok();
        }

        private CommandResult DoClearHiringPriority(int facilityId)
        {
            CommandResult check = CanClearHiringPriority(facilityId);
            if (!check.Success) return check;
            _hirePriority.Remove(facilityId);
            return CommandResult.Ok();
        }
    }
}
