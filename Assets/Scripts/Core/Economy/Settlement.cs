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
    /// 정착지 하나의 경제 상태와 규칙. 게임 시계의 정각마다 <see cref="AdvanceHour"/>를 한 번 호출한다.
    ///
    /// 경제 틱 순서는 economy_balance_v0.8.md 209행(결정 C1)을 따르고, 그 앞뒤에 재해·시장 단계가 붙는다.
    /// 실제 순서는 <see cref="AdvanceHour"/>(Settlement.Tick.cs) 본문의 단계 주석과 Docs/code_guide.md 3-2절.
    /// 플레이어 명령(건설 예약·구매·폐쇄 등)은 틱 사이에 즉시 반영되고, 설정 <c>startJobsOnCommand</c>가 켜져 있으면
    /// 착공도 명령 순간에 일어난다(진행 중인 시간을 첫 1시간으로 센다, 결정 JQ2). 끄면 다음 틱에서 착공한다(원본 v0.8).
    ///
    /// 원본 v0.8 시뮬레이터와 같은 계산을 하며(나중에 넣은 규칙을 끈 상태), 회귀값은 EditMode 테스트와 Tools/SettlementSim으로 고정돼 있다.
    /// 파일 나눔: 상태·조회·내부 도우미 = 이 파일, 명령 처리(Do*·Can*) = Settlement.Actions.cs, 공개 명령·기록 = Settlement.Commands.cs,
    /// 경제 틱·작업 큐 = Settlement.Tick.cs, 물 계산 = Settlement.Water.cs, 물탱크 = Settlement.WaterTanks.cs, 구매·판매·시장 가격 = Settlement.Market.cs.
    /// </summary>
    public sealed partial class Settlement
    {
        private readonly Dictionary<int, Facility> _facilities = new Dictionary<int, Facility>();
        private readonly List<ConstructionJob> _jobs = new List<ConstructionJob>();
        private readonly List<HourRecord> _history = new List<HourRecord>();
        private readonly Dictionary<int, FacilityState> _stateBeforeDemolish = new Dictionary<int, FacilityState>();
        private int _nextFacilityId = 1;
        private int _nextJobId = 1;

        public HexMap Map { get; }
        public HexTopology Topology { get; }
        public SettlementRules Rules { get; }

        public double Cash { get; private set; }
        public double Food { get; private set; }
        public double Stone { get; private set; }
        /// <summary>전역 식량 계수 F (0~1). 우물 물 생산에는 적용하지 않는다(확정).</summary>
        public double FoodCoefficient { get; private set; } = 1.0;

        /// <summary>다음에 정산할 게임 시간(정착지 시작 = 0).</summary>
        public long CurrentHour { get; private set; }

        public IReadOnlyCollection<Facility> Facilities => _facilities.Values;
        /// <summary>대기열 순서 그대로(진행 중 작업 포함).</summary>
        public IReadOnlyList<ConstructionJob> Jobs => _jobs;
        public IReadOnlyList<HourRecord> History => _history;
        /// <summary>가장 최근 틱의 물 배분 결과.</summary>
        public WaterAllocation LastWater { get; private set; }
        /// <summary>가장 최근 틱의 수로망(운영 중인 수로만).</summary>
        public CanalGraph LastCanals { get; private set; }

        public double FoodDiscarded { get; private set; }
        public double StoneDiscarded { get; private set; }
        public double TotalFoodShortage { get; private set; }
        public long? FirstShortageHour { get; private set; }
        public long? FirstInsolvencyHour { get; private set; }
        /// <summary>첫 경매장 완공 시각. 경매 일정(완공 + auctionRepeatsGameHours, 48시간)의 기준.</summary>
        public long? AuctionHallCompletedHour { get; private set; }

        public event Action<Facility> FacilityCompleted;
        public event Action<ConstructionJob> JobStarted;
        public event Action<HourRecord> HourSettled;

        /// <summary>재해(전염병 등). 없으면 null. <see cref="DisasterManager.Attach"/>가 붙인다.</summary>
        public DisasterManager Disasters { get; internal set; }

        public Settlement(HexMap map, SettlementRules rules)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Topology = new HexTopology(map);
            Cash = rules.StartingCash;
            Stone = Math.Min(rules.StarterStone, rules.BaseStoneStorage);
            Food = Math.Min(rules.StarterFood, rules.BaseFoodStorage);
            foreach (KeyValuePair<string, int> kv in rules.FreeBuilds)
            {
                if (kv.Value > 0) _freeBuilds[kv.Key] = kv.Value;
            }
        }

        private readonly Dictionary<string, int> _freeBuilds = new Dictionary<string, int>();

        /// <summary>남은 무료 완공 배치 수(이 종류). 0이면 일반 건설.</summary>
        public int FreeBuildsRemaining(string kind) => _freeBuilds.TryGetValue(kind, out int n) ? n : 0;

        // ───────────────────────── 조회 ─────────────────────────

        public double FoodCapacity => Rules.BaseFoodStorage + OperatingOf(BuildingKinds.Warehouse).Sum(w => LevelOf(w).FoodStorageBonus ?? 0);
        public double StoneCapacity => Rules.BaseStoneStorage + OperatingOf(BuildingKinds.Warehouse).Sum(w => LevelOf(w).StoneStorageBonus ?? 0);
        public int JobsInProgress => _jobs.Count(j => j.State == JobState.InProgress);

        public Facility GetFacility(int id) => _facilities.TryGetValue(id, out Facility f) ? f : null;
        public Facility FacilityAt(HexCoord tile) => _facilities.Values.FirstOrDefault(f => !f.Edge.HasValue && f.Tile == tile);
        public Facility FacilityOn(EdgeId edge, string kind) => _facilities.Values.FirstOrDefault(f => f.Edge == edge && f.Kind == kind);
        public ConstructionJob GetJob(int id) => _jobs.FirstOrDefault(j => j.Id == id);

        public IEnumerable<Facility> OperatingOf(string kind) => _facilities.Values.Where(f => f.Kind == kind && f.IsOperating);

        public BuildingLevel LevelOf(Facility f) => Rules.Level(f.Kind, f.Level);

        /// <summary>시설의 현재 물 기본 가동률(최근 틱 기준). 물을 쓰지 않는 시설은 1.</summary>
        public double WaterRateOf(Facility f)
        {
            if ((LevelOf(f).WaterDemand ?? 0) <= 0) return 1.0;
            return LastWater?.RateOf(f.Id.ToString()) ?? 0.0;
        }

        /// <summary>생산 효율 = 물 공급률 × 식량 계수, 지역·장비의 효율 상한(고지대 90% 등)을 넘지 않는다.</summary>
        private double Efficiency(double waterRate, double foodCoefficient) => Math.Min(Rules.EfficiencyCap, waterRate * foodCoefficient);

        /// <summary>시설의 시간당 생산·식량 소비 예상치. 운영 중이 아니면 생산·소비 모두 0.</summary>
        public FacilityOutput OutputOf(Facility f)
        {
            BuildingLevel lv = LevelOf(f);
            ResourceKind resource;
            double max;
            switch (f.Kind)
            {
                case BuildingKinds.Farm: resource = ResourceKind.Food; max = (lv.FoodProduction ?? 0) * Rules.FoodOutputMultiplier; break;
                case BuildingKinds.Quarry: resource = ResourceKind.Stone; max = (lv.StoneProduction ?? 0) * Rules.StoneOutputMultiplier; break;
                case BuildingKinds.Mine: resource = ResourceKind.Ore; max = (lv.OreProduction ?? 0) * Rules.OreMultiplier; break;
                default: resource = ResourceKind.None; max = 0; break;
            }
            if (!f.IsOperating) return new FacilityOutput(resource, 0, max, 0, 0, FoodCoefficient);
            double rate = WaterRateOf(f);
            double produced = resource == ResourceKind.None ? 0 : max * Efficiency(rate, FoodCoefficient) * (Disasters?.ProductionFactor(f) ?? 1);
            return new FacilityOutput(resource, produced, max, lv.FoodUse ?? 0, rate, FoodCoefficient);
        }

        // ───────────────────────── 내부 도우미 ─────────────────────────

        private CommandResult Enqueue(JobType type, string kind, HexCoord tile, EdgeId? edge, int facilityId, int targetLevel)
        {
            BuildingLevel lv = Rules.Level(kind, type == JobType.Demolish ? 1 : targetLevel);
            int hours = Rules.JobHours(lv.Hours, type == JobType.Demolish);
            var job = new ConstructionJob
            {
                Id = _nextJobId++, Type = type, Kind = kind, Tile = tile, Edge = edge, FacilityId = facilityId,
                TargetLevel = targetLevel, StoneCost = type == JobType.Demolish ? 0 : (lv.Stone ?? 0) * Rules.StoneCostMultiplier, Hours = hours,
                State = JobState.Queued, RequestedHour = CurrentHour,
            };
            _jobs.Add(job);
            return CommandResult.Ok(job.Id);
        }

        private bool PendingBuildOn(EdgeId edge, string kind) =>
            _jobs.Any(j => j.Type == JobType.Build && j.Edge == edge && j.Kind == kind);

        private bool HasPendingJob(Facility f) => _jobs.Any(j => j.FacilityId == f.Id);

        /// <summary>창고를 없앴을 때 현재 재고가 줄어든 저장 한도를 넘으면 그 이유(넘지 않으면 null).</summary>
        private string StorageProblemIfRemoved(Facility f)
        {
            if (f.Kind != BuildingKinds.Warehouse || !f.IsOperating && f.State != FacilityState.Closed) return null;
            BuildingLevel lv = LevelOf(f);
            double foodCapAfter = FoodCapacity - (f.IsOperating ? lv.FoodStorageBonus ?? 0 : 0);
            double stoneCapAfter = StoneCapacity - (f.IsOperating ? lv.StoneStorageBonus ?? 0 : 0);
            if (Food > foodCapAfter + 1e-9 || Stone > stoneCapAfter + 1e-9)
            {
                return $"철거·폐쇄 후 저장 한도(식량 {foodCapAfter:0}, 석재 {stoneCapAfter:0})를 현재 재고가 넘습니다.";
            }
            return null;
        }

        private void AddStone(double amount)
        {
            double room = Math.Max(0, StoneCapacity - Stone);
            double added = Math.Min(room, Math.Max(0, amount));
            Stone += added;
            StoneDiscarded += Math.Max(0, amount) - added;
        }
    }
}
