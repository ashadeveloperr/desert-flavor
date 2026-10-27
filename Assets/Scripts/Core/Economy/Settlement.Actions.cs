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
    /// 플레이어 명령(건설·수로·개량·철거·취소·순서 변경·폐쇄·구매)의 실제 처리(Do*)와 미리 검사(Can*). 기록 래퍼는 Settlement.Commands.cs.
    /// 2026-10-24 Settlement.cs에서 나눔(검토 개선 7, 동작 변화 없음). 정착지 전체 설명은 Settlement.cs.
    /// </summary>
    public sealed partial class Settlement
    {
        // ───────────────────────── 플레이어 명령 ─────────────────────────

        /// <summary>
        /// 타일 시설 건설 예약. 석재는 착공 때 차감된다.
        /// 무료 완공 배치(<see cref="FreeBuildsRemaining"/>)가 남아 있으면 대기열 없이 바로 운영 중인 L1 시설이 생긴다(JobId -1).
        /// </summary>
        private CommandResult DoRequestBuild(string kind, HexCoord tile)
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
        private CommandResult DoRequestCanal(EdgeId edge)
        {
            CommandResult check = CanBuildCanal(edge);
            return check.Success ? Enqueue(JobType.Build, BuildingKinds.CanalEdge, default, edge, -1, 1) : check;
        }

        private CommandResult DoRequestUpgrade(int facilityId)
        {
            CommandResult check = CanUpgrade(facilityId);
            if (!check.Success) return check;
            Facility f = GetFacility(facilityId);
            return Enqueue(JobType.Upgrade, f.Kind, f.Tile, f.Edge, f.Id, f.Level + 1);
        }

        private CommandResult DoRequestDemolish(int facilityId)
        {
            CommandResult check = CanDemolish(facilityId);
            if (!check.Success) return check;
            Facility f = GetFacility(facilityId);
            return Enqueue(JobType.Demolish, f.Kind, f.Tile, f.Edge, f.Id, f.Level);
        }

        // ── 미리 검사(예약하지 않음). UI가 설치 가능 여부와 이유를 보여 줄 때 쓴다. ──

        public CommandResult CanBuild(string kind, HexCoord tile)
        {
            if (SettlementRules.IsEdgeKind(kind)) return CommandResult.Fail("수로는 RequestCanal로 짓습니다.");
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
            string storage = StorageProblemIfRemoved(f);
            if (storage != null) return CommandResult.Fail(storage);
            return CommandResult.Ok();
        }

        /// <summary>
        /// 작업 취소. 대기 중이면 비용 없이 삭제, 진행 중이면 투입 석재의 <see cref="SettlementRules.CancelRefundRatio"/>를 돌려받고 원상태로 되돌린다.
        /// </summary>
        private CommandResult DoCancelJob(int jobId)
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
        private CommandResult DoMoveQueuedJob(int jobId, int newQueuedIndex)
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
        private CommandResult DoSetClosed(int facilityId, bool closed)
        {
            Facility f = GetFacility(facilityId);
            if (f == null) return CommandResult.Fail("시설이 없습니다.");
            if (closed && Rules.CannotClose.Contains(f.Kind)) return CommandResult.Fail($"{f.Kind}은(는) 폐쇄할 수 없습니다.");
            if (closed && f.State == FacilityState.Operating)
            {
                string storage = StorageProblemIfRemoved(f);
                if (storage != null) return CommandResult.Fail(storage);
            }
            if (closed && f.State == FacilityState.Operating) f.State = FacilityState.Closed;
            else if (!closed && f.State == FacilityState.Closed) f.State = FacilityState.Operating;
            else return CommandResult.Fail("운영 중인 시설만 폐쇄, 폐쇄된 시설만 재개할 수 있습니다.");
            return CommandResult.Ok();
        }

        /// <summary>식량 외부 구매(1단위씩, 단가 상승). 저장 한도·자금 안에서만. 실제 구매 수를 돌려준다.</summary>
        private int DoBuyFood(int units) => Buy(units, isFood: true);

        /// <summary>석재 외부 구매(1단위씩, 단가 상승). 저장 한도·자금 안에서만. 실제 구매 수를 돌려준다.</summary>
        private int DoBuyStone(int units) => Buy(units, isFood: false);

        /// <summary>식량 재고를 비율만큼 잃는다(메뚜기떼, 2026-10-23 D4).</summary>
        internal void LoseFood(double fraction)
        {
            Food = Math.Max(0, Food * (1 - Math.Max(0, Math.Min(1, fraction))));
        }

        /// <summary>식량 시장 판매(시장이 있어야 함). 실제 판매 수를 돌려준다.</summary>
        private int DoSellFood(int units) => Sell(units, isFood: true);

        /// <summary>석재 시장 판매(시장이 있어야 함). 실제 판매 수를 돌려준다.</summary>
        private int DoSellStone(int units) => Sell(units, isFood: false);

        /// <summary>다음 1단위 구매 가격. 시장이 없으면 떠돌이 상인 배율(2026-10-27 이슈 #18).</summary>
        public double NextPrice(bool food)
        {
            PurchasePrice p = food ? Rules.Config.Purchase.Food : Rules.Config.Purchase.Stone;
            double peddler = HasMarket ? 1 : Rules.Trade.NoMarketPriceMultiplier;
            return p.BaseCost * Rules.PurchasePriceMultiplier * peddler * Math.Pow(p.IncrementMultiplier, food ? FoodBuyPressure : StoneBuyPressure);
        }

        /// <summary>다음 1단위 판매 가격. 시장이 없으면(또는 시장 규칙이 없으면) 0.</summary>
        public double NextSellPrice(bool food)
        {
            if (Rules.Trade == null || !HasMarket) return 0;
            PurchasePrice p = food ? Rules.Config.Purchase.Food : Rules.Config.Purchase.Stone;
            if (p.SellLimit <= 0) return 0;
            double x = (food ? FoodSellPressure : StoneSellPressure) / p.SellLimit;
            return p.BaseCost * p.SellRatio * Math.Max(0, 1 - x * x);
        }
    }
}
