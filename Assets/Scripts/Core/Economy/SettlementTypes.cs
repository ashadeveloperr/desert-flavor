using DesertSettlement.Core.Hex;

namespace DesertSettlement.Core.Economy
{
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

    /// <summary>정착지 위의 시설 하나. 타일 시설은 <see cref="Tile"/>, 수로·수문은 <see cref="Edge"/>에 놓인다.</summary>
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
        /// <summary>수문 개방 상태(수문 시설만 의미 있음). 조작은 작업 슬롯을 쓰지 않는다.</summary>
        public bool GateOpen { get; internal set; } = true;
        /// <summary>운영을 시작한(완공된) 게임 시간. 미완공이면 -1.</summary>
        public long CompletedHour { get; internal set; } = -1;
        /// <summary>마지막으로 건설·개량을 마치고 지금 레벨로 운영을 시작한 시간. 미완공이면 -1.</summary>
        public long ActiveSinceHour { get; internal set; } = -1;

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
        public long StartedHour { get; internal set; } = -1;
        /// <summary>이 게임 시간이 시작될 때 완료된다.</summary>
        public long DeadlineHour { get; internal set; } = -1;

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
        Food,
        Stone,
        Ore,
    }

    /// <summary>
    /// 시설 하나의 시간당 생산·소비 예상치(가장 최근 물 배분과 현재 식량 계수 F 기준).
    /// 실제 정산은 다음 정각의 <see cref="Settlement.AdvanceHour"/>에서 일어나므로 약간 다를 수 있다.
    /// </summary>
    public readonly struct FacilityOutput
    {
        public readonly ResourceKind Resource;
        /// <summary>지금 상태의 시간당 생산량 = 최대 × 물 가동률 × F.</summary>
        public readonly double ProducedPerHour;
        /// <summary>물 100%·F 100%일 때의 시간당 생산량.</summary>
        public readonly double MaxPerHour;
        /// <summary>운영 중일 때의 고정 식량 소비(가동률과 무관).</summary>
        public readonly double FoodUsePerHour;
        public readonly double WaterRate;
        public readonly double FoodCoefficient;

        public FacilityOutput(ResourceKind resource, double produced, double max, double foodUse, double waterRate, double f)
        {
            Resource = resource;
            ProducedPerHour = produced;
            MaxPerHour = max;
            FoodUsePerHour = foodUse;
            WaterRate = waterRate;
            FoodCoefficient = f;
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
    }
}
