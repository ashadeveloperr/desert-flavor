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
    /// 경제 틱 순서 (C1 확정, economy_balance_v0.8.md 209행):
    ///   1. 이 시각에 끝나는 공사 완료
    ///   2. 대기열 착공 (FIFO, 작업 슬롯 수까지, 착공 시 석재 차감)
    ///   3. 물 배분 및 시설 상태 스냅샷
    ///   4. 생산(물 가동률 × 식량 계수 F) · 고정 식량 소비
    ///   5. 유지비
    ///   6. 저장 한도 적용 · 넘친 생산 폐기
    ///   7. 다음 시간의 F 갱신
    /// 플레이어 명령(건설 예약·구매·폐쇄 등)은 틱 사이에 즉시 반영되고, 착공은 다음 틱에서 일어난다.
    ///
    /// 원본 v0.8 시뮬레이터(Tools/desert_balance_sim_v08.py)와 같은 계산을 하며, 회귀값은 EditMode 테스트로 고정돼 있다.
    /// </summary>
    public sealed class Settlement
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
        /// <summary>가장 최근 틱의 수로망(운영 중인 수로·수문만).</summary>
        public CanalGraph LastCanals { get; private set; }

        public int FoodBought { get; private set; }
        public int StoneBought { get; private set; }
        public double FoodSpent { get; private set; }
        public double StoneSpent { get; private set; }
        public double FoodDiscarded { get; private set; }
        public double StoneDiscarded { get; private set; }
        public double TotalFoodShortage { get; private set; }
        public long? FirstShortageHour { get; private set; }
        public long? FirstInsolvencyHour { get; private set; }
        /// <summary>첫 경매장 완공 시각. 경매 일정(완공 + auctionRepeatsGameHours, 72시간)의 기준.</summary>
        public long? AuctionHallCompletedHour { get; private set; }

        public event Action<Facility> FacilityCompleted;
        public event Action<ConstructionJob> JobStarted;
        public event Action<HourRecord> HourSettled;

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
            double produced = resource == ResourceKind.None ? 0 : max * Efficiency(rate, FoodCoefficient);
            return new FacilityOutput(resource, produced, max, lv.FoodUse ?? 0, rate, FoodCoefficient);
        }

        // ───────────────────────── 플레이어 명령 ─────────────────────────

        /// <summary>
        /// 타일 시설 건설 예약. 석재는 착공 때 차감된다.
        /// 무료 완공 배치(<see cref="FreeBuildsRemaining"/>)가 남아 있으면 대기열 없이 바로 운영 중인 L1 시설이 생긴다(JobId -1).
        /// </summary>
        public CommandResult RequestBuild(string kind, HexCoord tile)
        {
            CommandResult check = CanBuild(kind, tile);
            if (!check.Success) return check;
            if (FreeBuildsRemaining(kind) > 0)
            {
                _freeBuilds[kind]--;
                var f = new Facility
                {
                    Id = _nextFacilityId++, Kind = kind, Tile = tile, State = FacilityState.Operating,
                    CompletedHour = CurrentHour, ActiveSinceHour = CurrentHour,
                };
                _facilities.Add(f.Id, f);
                if (kind == BuildingKinds.AuctionHall && AuctionHallCompletedHour == null) AuctionHallCompletedHour = CurrentHour;
                FacilityCompleted?.Invoke(f);
                return CommandResult.Ok();
            }
            return Enqueue(JobType.Build, kind, tile, null, -1, 1);
        }

        /// <summary>변 위 수로 건설 예약.</summary>
        public CommandResult RequestCanal(EdgeId edge)
        {
            CommandResult check = CanBuildCanal(edge);
            return check.Success ? Enqueue(JobType.Build, BuildingKinds.CanalEdge, default, edge, -1, 1) : check;
        }

        /// <summary>수문 건설 예약. 새 수문은 열린 상태.</summary>
        public CommandResult RequestGate(EdgeId edge)
        {
            CommandResult check = CanBuildGate(edge);
            return check.Success ? Enqueue(JobType.Build, BuildingKinds.CanalGate, default, edge, -1, 1) : check;
        }

        public CommandResult RequestUpgrade(int facilityId)
        {
            CommandResult check = CanUpgrade(facilityId);
            if (!check.Success) return check;
            Facility f = GetFacility(facilityId);
            return Enqueue(JobType.Upgrade, f.Kind, f.Tile, f.Edge, f.Id, f.Level + 1);
        }

        public CommandResult RequestDemolish(int facilityId)
        {
            CommandResult check = CanDemolish(facilityId);
            if (!check.Success) return check;
            Facility f = GetFacility(facilityId);
            return Enqueue(JobType.Demolish, f.Kind, f.Tile, f.Edge, f.Id, f.Level);
        }

        // ── 미리 검사(예약하지 않음). UI가 설치 가능 여부와 이유를 보여 줄 때 쓴다. ──

        public CommandResult CanBuild(string kind, HexCoord tile)
        {
            if (SettlementRules.IsEdgeKind(kind)) return CommandResult.Fail("수로·수문은 RequestCanal / RequestGate로 짓습니다.");
            if (Rules.DisabledKinds.Contains(kind)) return CommandResult.Fail($"{kind}은(는) MVP에서 지을 수 없습니다.");
            if (!Rules.AllowedTerrain.TryGetValue(kind, out TerrainType[] allowed)) return CommandResult.Fail($"알 수 없는 시설 종류: {kind}");
            if (!Map.TryGetTile(tile, out HexTile hexTile)) return CommandResult.Fail("맵 밖의 타일입니다.");
            if (Array.IndexOf(allowed, hexTile.Terrain) < 0)
            {
                return CommandResult.Fail($"{kind}은(는) {string.Join("/", allowed)} 지형에만 지을 수 있습니다(현재 {hexTile.Terrain}).");
            }
            if (FacilityAt(tile) != null || _jobs.Any(j => j.Type == JobType.Build && !j.Edge.HasValue && j.Tile == tile))
            {
                return CommandResult.Fail("이미 시설이 있거나 건설 예약된 타일입니다.");
            }
            return CommandResult.Ok();
        }

        /// <summary>맵 경계 변도 가능.</summary>
        public CommandResult CanBuildCanal(EdgeId edge)
        {
            if (!Topology.Contains(edge)) return CommandResult.Fail("맵 밖의 변입니다.");
            // 확정(2026-10-23): 양쪽 타일이 모두 장애물인 변에는 수로 불가. 장애물 지형(산맥·고원) 안으로는 물이 지나가지 못한다.
            // 맵 경계 변은 맵 안쪽 타일이 하나뿐이라 이 규칙에 걸리지 않는다.
            IReadOnlyList<HexCoord> sides = Topology.TilesOf(edge);
            if (sides.Count == 2 && sides.All(t => Map.GetTile(t).Terrain == TerrainType.Obstacle))
            {
                return CommandResult.Fail("양쪽이 모두 장애물인 변에는 수로를 놓을 수 없습니다.");
            }
            if (FacilityOn(edge, BuildingKinds.CanalEdge) != null || PendingBuildOn(edge, BuildingKinds.CanalEdge))
            {
                return CommandResult.Fail("이미 수로가 있거나 예약된 변입니다.");
            }
            return CommandResult.Ok();
        }

        /// <summary>수로가 있는(또는 예약된) 변에만 가능.</summary>
        public CommandResult CanBuildGate(EdgeId edge)
        {
            if (FacilityOn(edge, BuildingKinds.CanalEdge) == null && !PendingBuildOn(edge, BuildingKinds.CanalEdge))
            {
                return CommandResult.Fail("수로가 있는 변에만 수문을 지을 수 있습니다.");
            }
            if (FacilityOn(edge, BuildingKinds.CanalGate) != null || PendingBuildOn(edge, BuildingKinds.CanalGate))
            {
                return CommandResult.Fail("이미 수문이 있거나 예약된 변입니다.");
            }
            return CommandResult.Ok();
        }

        public CommandResult CanUpgrade(int facilityId)
        {
            Facility f = GetFacility(facilityId);
            if (f == null) return CommandResult.Fail("시설이 없습니다.");
            if (HasPendingJob(f)) return CommandResult.Fail("이미 이 시설의 작업이 예약돼 있습니다.");
            if (f.State == FacilityState.Constructing || f.State == FacilityState.Demolishing) return CommandResult.Fail("공사 중인 시설입니다.");
            if (f.Level >= Rules.MaxLevelOf(f.Kind)) return CommandResult.Fail($"최고 레벨(L{Rules.MaxLevelOf(f.Kind)})입니다.");
            return CommandResult.Ok();
        }

        public CommandResult CanDemolish(int facilityId)
        {
            Facility f = GetFacility(facilityId);
            if (f == null) return CommandResult.Fail("시설이 없습니다.");
            if (HasPendingJob(f)) return CommandResult.Fail("이미 이 시설의 작업이 예약돼 있습니다.");
            if (f.State != FacilityState.Operating && f.State != FacilityState.Closed) return CommandResult.Fail("운영 중이거나 폐쇄된 시설만 철거할 수 있습니다.");
            if (f.Kind == BuildingKinds.CanalEdge && f.Edge.HasValue && FacilityOn(f.Edge.Value, BuildingKinds.CanalGate) != null)
            {
                return CommandResult.Fail("수문을 먼저 철거해야 합니다.");
            }
            string storage = StorageProblemIfRemoved(f);
            if (storage != null) return CommandResult.Fail(storage);
            return CommandResult.Ok();
        }

        /// <summary>
        /// 작업 취소. 대기 중이면 비용 없이 삭제, 진행 중이면 투입 석재의 <see cref="SettlementRules.CancelRefundRatio"/>를 돌려받고 원상태로 되돌린다.
        /// </summary>
        public CommandResult CancelJob(int jobId)
        {
            ConstructionJob job = GetJob(jobId);
            if (job == null) return CommandResult.Fail("작업이 없습니다.");
            _jobs.Remove(job);
            if (job.State == JobState.Queued) return CommandResult.Ok();

            AddStone(job.StoneCost * Rules.CancelRefundRatio);
            Facility f = GetFacility(job.FacilityId);
            switch (job.Type)
            {
                case JobType.Build:
                    _facilities.Remove(job.FacilityId);
                    break;
                case JobType.Upgrade:
                    f.State = FacilityState.Operating;
                    f.StoneInvested -= job.StoneCost;
                    break;
                case JobType.Demolish:
                    f.State = _stateBeforeDemolish.TryGetValue(f.Id, out FacilityState before) ? before : FacilityState.Operating;
                    _stateBeforeDemolish.Remove(f.Id);
                    break;
            }
            return CommandResult.Ok();
        }

        /// <summary>대기 중인 작업의 순서를 바꾼다. newQueuedIndex는 '대기 중 작업들' 사이의 위치(0 = 맨 앞).</summary>
        public CommandResult MoveQueuedJob(int jobId, int newQueuedIndex)
        {
            ConstructionJob job = GetJob(jobId);
            if (job == null) return CommandResult.Fail("작업이 없습니다.");
            if (job.State != JobState.Queued) return CommandResult.Fail("시작한 작업은 순서를 바꿀 수 없습니다.");
            List<ConstructionJob> queued = _jobs.Where(j => j.State == JobState.Queued && j != job).ToList();
            newQueuedIndex = Math.Max(0, Math.Min(newQueuedIndex, queued.Count));
            queued.Insert(newQueuedIndex, job);
            _jobs.RemoveAll(j => j.State == JobState.Queued);
            _jobs.AddRange(queued);
            return CommandResult.Ok(job.Id);
        }

        /// <summary>시설 폐쇄/재개. 폐쇄 중에는 물 수요·식량 소비 0, 유지비 일부.</summary>
        public CommandResult SetClosed(int facilityId, bool closed)
        {
            Facility f = GetFacility(facilityId);
            if (f == null) return CommandResult.Fail("시설이 없습니다.");
            if (closed && Rules.CannotClose.Contains(f.Kind)) return CommandResult.Fail($"{f.Kind}은(는) 폐쇄할 수 없습니다.");
            if (closed && f.State == FacilityState.Operating) f.State = FacilityState.Closed;
            else if (!closed && f.State == FacilityState.Closed) f.State = FacilityState.Operating;
            else return CommandResult.Fail("운영 중인 시설만 폐쇄, 폐쇄된 시설만 재개할 수 있습니다.");
            return CommandResult.Ok();
        }

        /// <summary>수문 개방/차단. 작업 슬롯·자원을 쓰지 않고 즉시 반영(다음 틱 물 배분부터).</summary>
        public CommandResult SetGateOpen(int gateFacilityId, bool open)
        {
            Facility f = GetFacility(gateFacilityId);
            if (f == null || f.Kind != BuildingKinds.CanalGate) return CommandResult.Fail("수문이 아닙니다.");
            f.GateOpen = open;
            return CommandResult.Ok();
        }

        /// <summary>식량 외부 구매(1단위씩, 단가 상승). 저장 한도·자금 안에서만. 실제 구매 수를 돌려준다.</summary>
        public int BuyFood(int units) => Buy(units, isFood: true);

        /// <summary>석재 외부 구매(1단위씩, 단가 상승). 저장 한도·자금 안에서만. 실제 구매 수를 돌려준다.</summary>
        public int BuyStone(int units) => Buy(units, isFood: false);

        /// <summary>다음 1단위 구매 가격.</summary>
        public double NextPrice(bool food)
        {
            PurchasePrice p = food ? Rules.Config.Purchase.Food : Rules.Config.Purchase.Stone;
            return p.BaseCost * Rules.PurchasePriceMultiplier * Math.Pow(p.IncrementMultiplier, food ? FoodBought : StoneBought);
        }

        // ───────────────────────── 경제 틱 ─────────────────────────

        /// <summary>현재 게임 시간(<see cref="CurrentHour"/>)을 정산하고 다음 시간으로 넘어간다.</summary>
        public HourRecord AdvanceHour()
        {
            long h = CurrentHour;
            CompleteJobs(h);
            StartJobs(h);
            WaterAllocation water = SolveWater();

            double demand = 0, upkeep = 0, food = 0, stone = 0, ore = 0;
            double foodValued = 0, stoneValued = 0, oreValued = 0; // 가동 숙성 반영(경매 가치용)
            double f = FoodCoefficient;
            bool wateredFarm = false;
            foreach (Facility facility in _facilities.Values.OrderBy(x => x.Id))
            {
                if (facility.State == FacilityState.Constructing || facility.State == FacilityState.Demolishing) continue;
                BuildingLevel lv = LevelOf(facility);
                if (facility.State == FacilityState.Operating)
                {
                    demand += lv.FoodUse ?? 0;
                    upkeep += lv.Upkeep ?? 0;
                    double rate = (lv.WaterDemand ?? 0) > 0 ? water.RateOf(facility.Id.ToString()) : 1.0;
                    double maturity = Rules.Maturity(h - facility.CompletedHour);
                    double amount;
                    switch (facility.Kind)
                    {
                        case BuildingKinds.Farm:
                            amount = (lv.FoodProduction ?? 0) * Rules.FoodOutputMultiplier * Efficiency(rate, f);
                            food += amount;
                            foodValued += amount * maturity;
                            if (rate > 0) wateredFarm = true;
                            break;
                        case BuildingKinds.Quarry:
                            amount = (lv.StoneProduction ?? 0) * Rules.StoneOutputMultiplier * Efficiency(rate, f);
                            stone += amount;
                            stoneValued += amount * maturity;
                            break;
                        case BuildingKinds.Mine:
                            amount = (lv.OreProduction ?? 0) * Rules.OreMultiplier * Efficiency(rate, f);
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

            // 다음 시간의 F.
            TotalFoodShortage += deficit;
            if (deficit > 1e-9)
            {
                if (FirstShortageHour == null) FirstShortageHour = h;
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
            };
            _history.Add(record);
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
                        FacilityCompleted?.Invoke(f);
                        break;
                    case JobType.Upgrade:
                        f.Level = job.TargetLevel;
                        f.State = FacilityState.Operating;
                        f.ActiveSinceHour = h;
                        FacilityCompleted?.Invoke(f);
                        break;
                    case JobType.Demolish:
                        _facilities.Remove(f.Id);
                        _stateBeforeDemolish.Remove(f.Id);
                        AddStone(f.StoneInvested * Rules.DemolishRefundRatio);
                        break;
                }
            }
        }

        private void StartJobs(long h)
        {
            int running = JobsInProgress;
            foreach (ConstructionJob job in _jobs.Where(j => j.State == JobState.Queued).ToList())
            {
                if (running >= Rules.ConstructionSlots) break;
                if (!CanStart(job)) break; // C2 확정: FIFO. 앞 작업이 막히면 뒤 작업도 기다린다.

                Stone -= job.StoneCost;
                job.State = JobState.InProgress;
                job.StartedHour = h;
                job.DeadlineHour = h + job.Hours;
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
                        break;
                }
                running++;
                JobStarted?.Invoke(job);
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

        private WaterAllocation SolveWater()
        {
            CanalGraph canals;
            LastWater = SolveWater(assumeJobsComplete: false, out canals);
            LastCanals = canals;
            return LastWater;
        }

        /// <summary>
        /// 진행 중인 건설·개량이 모두 끝났다고 가정한 물 배분(개발 중 자산 가치 평가용). 폐쇄·철거 중 시설은 빠진다.
        /// 실제 상태에는 영향을 주지 않는다.
        /// </summary>
        public WaterAllocation SolveWaterAssumingJobsComplete() => SolveWater(assumeJobsComplete: true, out _);

        /// <summary>가정 배분에서 시설이 쓸 레벨(진행 중 개량이면 목표 레벨).</summary>
        public int AssumedLevelOf(Facility f)
        {
            ConstructionJob job = _jobs.FirstOrDefault(j => j.FacilityId == f.Id && j.State == JobState.InProgress && j.Type == JobType.Upgrade);
            return job?.TargetLevel ?? f.Level;
        }

        private WaterAllocation SolveWater(bool assumeJobsComplete, out CanalGraph canals)
        {
            bool Counts(Facility f) => f.IsOperating ||
                (assumeJobsComplete && (f.State == FacilityState.Constructing || f.State == FacilityState.Upgrading));
            BuildingLevel Level(Facility f) => assumeJobsComplete ? Rules.Level(f.Kind, AssumedLevelOf(f)) : LevelOf(f);

            canals = new CanalGraph(Topology);
            foreach (Facility c in _facilities.Values.Where(x => x.Kind == BuildingKinds.CanalEdge && Counts(x)))
            {
                canals.TryInstall(c.Edge.Value);
                canals.TrySetLevel(c.Edge.Value, assumeJobsComplete ? AssumedLevelOf(c) : c.Level);
            }
            foreach (Facility g in _facilities.Values.Where(x => x.Kind == BuildingKinds.CanalGate && Counts(x)))
            {
                if (!canals.IsInstalled(g.Edge.Value)) continue;
                canals.TrySetGate(g.Edge.Value, true);
                canals.TrySetGateOpen(g.Edge.Value, g.GateOpen);
            }

            List<WaterSource> sources = _facilities.Values.Where(x => x.Kind == BuildingKinds.Well && Counts(x))
                .Select(w => new WaterSource(w.Id.ToString(), w.Tile, (Level(w).WaterSupply ?? 0) * Rules.WellSupplyMultiplier)).ToList();
            List<WaterConsumer> consumers = _facilities.Values
                .Where(x => Counts(x) && !x.Edge.HasValue && (Level(x).WaterDemand ?? 0) > 0)
                .Select(x => new WaterConsumer(x.Id.ToString(), x.Tile, Level(x).WaterDemand.Value)).ToList();

            return WaterNetworkSolver.Solve(canals, sources, consumers, Rules.Water);
        }

        // ───────────────────────── 내부 도우미 ─────────────────────────

        private CommandResult Enqueue(JobType type, string kind, HexCoord tile, EdgeId? edge, int facilityId, int targetLevel)
        {
            BuildingLevel lv = Rules.Level(kind, type == JobType.Demolish ? 1 : targetLevel);
            int hours = type == JobType.Demolish
                ? Math.Max(1, (int)Math.Ceiling((lv.Hours ?? 1) * Rules.DemolishHoursRatio))
                : (int)(lv.Hours ?? 1);
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
                return $"철거 후 저장 한도(식량 {foodCapAfter:0}, 석재 {stoneCapAfter:0})를 현재 재고가 넘습니다.";
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

        private int Buy(int units, bool isFood)
        {
            if (units <= 0) return 0;
            double stock = isFood ? Food : Stone;
            double cap = isFood ? FoodCapacity : StoneCapacity;
            int count = Math.Min(units, Math.Max(0, (int)(cap - stock)));
            int bought = 0;
            for (int i = 0; i < count; i++)
            {
                double price = NextPrice(isFood);
                if (Cash < price) break;
                Cash -= price;
                if (isFood) { Food += 1; FoodBought++; FoodSpent += price; }
                else { Stone += 1; StoneBought++; StoneSpent += price; }
                bought++;
            }
            return bought;
        }
    }
}
