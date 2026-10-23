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
    /// 이번 시간 물 배분 계산(수로 그래프·우물·취수 시설 → WaterNetworkSolver). 물탱크는 Settlement.WaterTanks.cs.
    /// 2026-10-24 Settlement.cs에서 나눔(검토 개선 7, 동작 변화 없음). 정착지 전체 설명은 Settlement.cs.
    /// </summary>
    public sealed partial class Settlement
    {
        /// <summary>이번 시간 물 배분 수치: 모래폭풍 중이면 수로 손실이 커진다(<see cref="DisasterManager.CanalLossFactor"/>).</summary>
        private WaterNetworkSettings WaterNow
        {
            get
            {
                double f = Disasters?.CanalLossFactor ?? 1;
                if (Math.Abs(f - 1) < 1e-12) return Rules.Water;
                return new WaterNetworkSettings { LossPerWholeEdge = Math.Min(0.95, Rules.Water.LossPerWholeEdge * f), CapacityOf = Rules.Water.CapacityOf };
            }
        }

        private WaterAllocation SolveWater()
        {
            WaterAllocation water = SolveWater(false, out CanalGraph canals, out List<WaterSource> wells);
            ApplyWaterTanks(canals, water, wells); // 물탱크 방출·충전(실제 시간 정산에서만, 저장량이 바뀐다)
            LastWater = water;
            LastCanals = canals;
            return LastWater;
        }

        /// <summary>
        /// 진행 중인 건설·개량이 모두 끝났다고 가정한 물 배분(개발 중 자산 가치 평가용). 폐쇄·철거 중 시설은 빠진다.
        /// 실제 상태에는 영향을 주지 않는다.
        /// </summary>
        public WaterAllocation SolveWaterAssumingJobsComplete() => SolveWater(true, out _, out _);

        /// <summary>가정 배분에서 시설이 쓸 레벨(진행 중 개량이면 목표 레벨).</summary>
        public int AssumedLevelOf(Facility f)
        {
            ConstructionJob job = _jobs.FirstOrDefault(j => j.FacilityId == f.Id && j.State == JobState.InProgress && j.Type == JobType.Upgrade);
            return job?.TargetLevel ?? f.Level;
        }

        /// <summary>우물 → 시설 물 배분(물탱크 제외). 진행 중 작업을 끝났다고 가정하는 계산은 물탱크를 넣지 않는다.</summary>
        private WaterAllocation SolveWater(bool assumeJobsComplete, out CanalGraph canals, out List<WaterSource> sources)
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
            // 닫힌 수로(2026-10-23, 수문 폐지 뒤 물길 개폐 수단)는 '막힌 변'으로 남긴다: 물은 못 지나가지만,
            // 물탱크 하류 판정(닫힌 수로도 연 것으로 봄)에 같은 그래프를 쓴다. Docs/decision_log_auto.md T4·T7.
            foreach (Facility c in _facilities.Values.Where(x => x.Kind == BuildingKinds.CanalEdge && x.State == FacilityState.Closed))
            {
                canals.TryInstall(c.Edge.Value);
                canals.TrySetLevel(c.Edge.Value, c.Level);
                canals.TrySetBlocked(c.Edge.Value, true);
            }

            sources = _facilities.Values.Where(x => x.Kind == BuildingKinds.Well && Counts(x))
                .Select(w => new WaterSource(w.Id.ToString(), w.Tile, (Level(w).WaterSupply ?? 0) * Rules.WellSupplyMultiplier)).ToList();
            List<WaterConsumer> consumers = _facilities.Values
                .Where(x => Counts(x) && !x.Edge.HasValue && (Level(x).WaterDemand ?? 0) > 0)
                .Select(x => new WaterConsumer(x.Id.ToString(), x.Tile, Level(x).WaterDemand.Value)).ToList();

            return WaterNetworkSolver.Solve(canals, sources, consumers, assumeJobsComplete ? Rules.Water : WaterNow);
        }
    }
}
