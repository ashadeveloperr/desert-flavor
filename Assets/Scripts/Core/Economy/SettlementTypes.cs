using System;
using System.Collections.Generic;
using DesertSettlement.Core.Hex;

namespace DesertSettlement.Core.Economy
{
    /// <summary>물탱크가 한 시간에 하는 일. 한 시간에 충전과 방출 중 하나만(방출 우선, Docs/decision_log_auto.md T6).</summary>
    public enum WaterTankMode
    {
        /// <summary>아무것도 안 함(가득 찼거나, 남는 물이 없거나, 운영 중이 아님).</summary>
        Idle,
        /// <summary>모든 시설이 받고 남은 물로 충전.</summary>
        Charging,
        /// <summary>하류의 물이 모자란 시설에 방출.</summary>
        Discharging,
    }

    public enum FacilityState
    {
        /// <summary>건설 공사 중. 생산·식량 소비·유지비 없음.</summary>
        Constructing,
        /// <summary>정상 운영.</summary>
        Operating,
        /// <summary>개량 공사 중. 가동 중단, 유지비는 폐쇄와 같은 비율.</summary>
        Upgrading,
        /// <summary>플레이어가 폐쇄. 물 수요·식량 소비 0, 유지비 일부.</summary>
        Closed,
        /// <summary>철거 공사 중. 건설 중과 같이 비용 없음. 완료되면 사라진다.</summary>
        Demolishing,
    }

    public enum JobType
    {
        Build,
        Upgrade,
        Demolish,
    }

    public enum JobState
    {
        /// <summary>대기열에서 착공을 기다림(석재는 아직 차감 안 됨).</summary>
        Queued,
        /// <summary>작업 슬롯을 차지하고 진행 중.</summary>
        InProgress,
    }

    /// <summary>정착지 위의 시설 하나. 타일 시설은 <see cref="Tile"/>, 수로는 <see cref="Edge"/>에 놓인다.</summary>
    public sealed class Facility
    {
        public int Id { get; internal set; }
        public string Kind { get; internal set; }
        public HexCoord Tile { get; internal set; }
        public EdgeId? Edge { get; internal set; }
        public int Level { get; internal set; } = 1;
        public FacilityState State { get; internal set; }
        /// <summary>건설·개량에 실제로 들어간 석재 합(철거 환급 계산용).</summary>
        public double StoneInvested { get; internal set; }
        /// <summary>운영을 시작한(완공된) 게임 시간. 미완공이면 -1.</summary>
        public long CompletedHour { get; internal set; } = -1;
        /// <summary>마지막으로 건설·개량을 마치고 지금 레벨로 운영을 시작한 시간. 미완공이면 -1.</summary>
        public long ActiveSinceHour { get; internal set; } = -1;
        /// <summary>물탱크에 저장된 물(물탱크만 의미 있음). 폐쇄·개량 중에도 그대로 남는다.</summary>
        public double StoredWater { get; internal set; }
        /// <summary>물탱크가 지난 시간에 한 일(물탱크만 의미 있음).</summary>
        public WaterTankMode TankMode { get; internal set; }
        /// <summary>물탱크가 지난 시간에 충전(+) 또는 방출(−)한 양.</summary>
        public double TankFlowLastHour { get; internal set; }
        /// <summary>주택에 사는 주민 수(주택만 의미 있음, 결정 LB1~). 소수로 서서히 늘고 준다(화면은 반올림).</summary>
        public double Residents { get; internal set; }
        /// <summary>가공 시설이 지난 시간 원료가 있어 돌린 비율(1 = 다 돌림, 결정 FD4). 가공 시설만 의미 있음.</summary>
        public double InputRatio { get; internal set; } = 1;

        public bool IsOperating => State == FacilityState.Operating;
        public override string ToString() => $"#{Id} {Kind} L{Level} {State} @{(Edge.HasValue ? Edge.ToString() : Tile.ToString())}";
    }

    /// <summary>건설·개량·철거 작업. 동시 진행은 작업 슬롯 수까지, 나머지는 FIFO 대기.</summary>
    public sealed class ConstructionJob
    {
        public int Id { get; internal set; }
        public JobType Type { get; internal set; }
        public string Kind { get; internal set; }
        public HexCoord Tile { get; internal set; }
        public EdgeId? Edge { get; internal set; }
        /// <summary>개량·철거 대상(건설은 착공 시 생성되는 시설).</summary>
        public int FacilityId { get; internal set; } = -1;
        public int TargetLevel { get; internal set; }
        /// <summary>착공 시 차감할 석재.</summary>
        public double StoneCost { get; internal set; }
        public int Hours { get; internal set; }
        public JobState State { get; internal set; }
        public long RequestedHour { get; internal set; }
        /// <summary>착공한 게임 시간(정산 단위). 명령 착공은 진행 중인 시간, 정각 착공은 막 정산한 시간. 경매 가치의 공사 진행 계산이 쓴다.</summary>
        public long StartedHour { get; internal set; } = -1;
        /// <summary>착공 시각(게임 시간, 분 단위까지). 12:30 명령이면 12.5.</summary>
        public double StartTime { get; internal set; } = -1;
        /// <summary>완공 시각 = 착공 시각 + 공사 시간 + 지연(결정 JQ4). 이 시각이 되면 정각을 기다리지 않고 완공된다.</summary>
        public double EndTime { get; internal set; } = -1;
        /// <summary>모래폭풍·공사 인원 부족으로 늘어난 시간의 합(완공 시각에 이미 더해져 있다).</summary>
        public double DelayHours { get; internal set; }
        /// <summary>완공을 정산하는 게임 시간: 완공 시각이 든 시간(17:30 완공 → 17, 정각 17:00 완공 → 16). 그 시간의 생산부터 낸다.</summary>
        public long DeadlineHour => EndTime < 0 ? -1 : (long)Math.Ceiling(EndTime - 1e-9) - 1;

        public override string ToString() => $"Job#{Id} {Type} {Kind} L{TargetLevel} {State}";
    }

    /// <summary>플레이어 명령의 결과. 실패하면 Message에 이유가 있다.</summary>
    public readonly struct CommandResult
    {
        public readonly bool Success;
        public readonly string Message;
        /// <summary>성공 시 만들어진 작업 Id(작업이 없는 명령은 -1).</summary>
        public readonly int JobId;

        private CommandResult(bool success, string message, int jobId)
        {
            Success = success;
            Message = message;
            JobId = jobId;
        }

        public static CommandResult Ok(int jobId = -1) => new CommandResult(true, null, jobId);
        public static CommandResult Fail(string message) => new CommandResult(false, message, -1);
        public override string ToString() => Success ? $"OK(job {JobId})" : "실패: " + Message;
    }

    /// <summary>생산 시설이 만드는 자원.</summary>
    public enum ResourceKind
    {
        None,
        /// <summary>식량 재화(곡물·과일·빵·고기).</summary>
        Food,
        Stone,
        Ore,
        /// <summary>기호품·향신료(맥주·와인·향신료).</summary>
        Goods,
    }

    /// <summary>
    /// 시설 하나의 시간당 생산·소비 예상치(가장 최근 물 배분·인원 배치 기준).
    /// 실제 정산은 다음 정각의 <see cref="Settlement.AdvanceHour"/>에서 일어나므로 약간 다를 수 있다.
    /// </summary>
    public readonly struct FacilityOutput
    {
        public readonly ResourceKind Resource;
        /// <summary>지금 상태의 시간당 생산량 = 최대 × 물 가동률 × 일손.</summary>
        public readonly double ProducedPerHour;
        /// <summary>물 100%·일손 100%일 때의 시간당 생산량.</summary>
        public readonly double MaxPerHour;
        /// <summary>이 시설 몫의 식량 소비: 노동력이 켜져 있으면 배치된 인원이 먹는 양, 꺼져 있으면 시설의 고정 소비(원본 v0.8).</summary>
        public readonly double FoodUsePerHour;
        public readonly double WaterRate;
        /// <summary>일손: 노동력이 켜져 있으면 배치율(배치 ÷ 필요 인원, 사람이 필요 없으면 1), 꺼져 있으면 식량 계수 F.</summary>
        public readonly double Workforce;
        /// <summary>배치된 인원과 필요 인원(노동력이 꺼져 있거나 사람이 필요 없으면 0).</summary>
        public readonly double WorkersAssigned, WorkersNeeded;

        public FacilityOutput(ResourceKind resource, double produced, double max, double foodUse, double waterRate, double workforce,
            double workersAssigned = 0, double workersNeeded = 0)
        {
            Resource = resource;
            ProducedPerHour = produced;
            MaxPerHour = max;
            FoodUsePerHour = foodUse;
            WaterRate = waterRate;
            Workforce = workforce;
            WorkersAssigned = workersAssigned;
            WorkersNeeded = workersNeeded;
        }

        public bool IsProducer => Resource != ResourceKind.None;

        /// <summary>농장이 자기 식량 소비보다 적게 생산하는지(농장 하나의 식량 수지 적자).</summary>
        public bool FarmBelowOwnUpkeep => Resource == ResourceKind.Food && ProducedPerHour < FoodUsePerHour;
    }

    /// <summary>한 게임 시간 정산 기록. 경매 가치(직전 24시간 실적) 계산에 쓴다.</summary>
    public sealed class HourRecord
    {
        public long Hour;
        public double Cash, Food, Stone, FoodCoefficient;
        public double OreProduced;
        public double FoodProduced, FoodValid;
        public double StoneProduced, StoneValid;
        public double FoodDemand, FoodDeficit, Upkeep;
        /// <summary>가동 숙성을 반영한 생산(경매 가치가 쓴다). 숙성이 꺼져 있으면 *Produced와 같다.</summary>
        public double OreValued, FoodValued, StoneValued;
        /// <summary>이 시간 아픈 주민 비율(전염병, 결정 EV1, 일하지 못한 몫). 0 = 없음.</summary>
        public double EpidemicSickShare;
        /// <summary>노동력(결정 LB1~): 정산 뒤 주택 주민 합, 이 시간 공사·시설에 배치된 인원(기본 인부 포함), 필요 인원 합. 꺼져 있으면 0.</summary>
        public double Residents, WorkersAssigned, WorkersNeeded;
        /// <summary>
        /// 재화(결정 FD1~, 인덱스 = <see cref="Goods"/>): 이 시간 만든 양, 먹고 마신 몫(식량은 식량 단위, 기호품은 단위).
        /// FoodProduced는 순 식량 가치(식량 효율로 환산, 가공에 쓴 원료는 뺌), FoodDemand·FoodDeficit은 식량 단위.
        /// </summary>
        public double[] GoodsProduced, GoodsConsumed;
        /// <summary>정산 끝 생활 수준과 목표(결정 LS1~).</summary>
        public double LivingStandard, LivingTarget;
        /// <summary>기호품 수요와 마신 양(주민 × 1인당).</summary>
        public double DrinkDemand, Drunk;
        /// <summary>
        /// 지난 기록 뒤 창고·석재 한도를 넘어 자동 판매로 번 돈과 버린 양(재화 단위 + 석재, 결정 AS1~AS3).
        /// 정산 중 넘침과 그 사이 명령(철거·취소 석재 환급)의 넘침을 함께 센다.
        /// </summary>
        public double AutoSaleIncome, Discarded;
    }

    /// <summary>
    /// 한 시간의 인원 배치(결정 LB1~). 공사(대기열 순서) → 우선 고용 목록 → 물 100% 시설(완공 순) → 물이 일부만 오는 시설(완공 순).
    /// 기본 인부는 공사에만, 주민은 공사의 남은 몫과 시설에 배치된다.
    /// </summary>
    public sealed class LaborAssignment
    {
        /// <summary>주택 주민 합.</summary>
        public double Residents;
        /// <summary>그중 아파서 일하지 못하는 주민(전염병).</summary>
        public double Sick;
        /// <summary>기본 인부(공사 전용).</summary>
        public double Crew;
        /// <summary>작업 id → 배치 인원(기본 인부 + 주민).</summary>
        public readonly Dictionary<int, double> Jobs = new Dictionary<int, double>();
        /// <summary>시설 id → 배치 인원.</summary>
        public readonly Dictionary<int, double> Facilities = new Dictionary<int, double>();
        /// <summary>공사·시설이 필요로 하는 인원 합.</summary>
        public double Needed;
        /// <summary>배치된 인원 합(기본 인부 포함).</summary>
        public double Assigned;
        /// <summary>일이 없는 주민(기본 인부 제외).</summary>
        public double IdleResidents;

        public double OfJob(int jobId) => Jobs.TryGetValue(jobId, out double n) ? n : 0;
        public double OfFacility(int facilityId) => Facilities.TryGetValue(facilityId, out double n) ? n : 0;
    }
}
