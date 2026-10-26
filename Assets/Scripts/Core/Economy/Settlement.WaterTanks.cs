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
    /// 물탱크(로드맵 1-②, 2026-10-23). 규칙과 이유는 Docs/decision_log_auto.md T1~T7.
    /// 한 시간의 물 배분 순서:
    ///   1. 우물 → 시설(기존 <see cref="WaterNetworkSolver"/>, 탱크 없음)
    ///   2. 방출: 저장된 물이 있는 탱크가 '하류'의 물이 모자란 시설에 보낸다(탱크 Id 순, 시간당 방출 상한).
    ///      하류 = 우물 → 시설 최단 경로가 탱크를 지나는 시설(T3). 판정은 닫힌 수로도 연 것으로 본다(T4),
    ///      실제로 흐르는 길은 닫힌 수로를 못 지난다.
    ///   3. 충전: 이번 시간에 방출하지 않은 탱크가 우물의 남은 물을 받는다(항상 마지막 순위, 시간당 충전 상한, T2·T6).
    /// 방출·충전도 수로 손실과 용량(앞 단계가 쓴 만큼 뺀 나머지)을 따른다.
    /// </summary>
    public sealed partial class Settlement
    {
        private const double TankEpsilon = 1e-9;

        /// <summary>운영 중인 물탱크의 저장 한도(지금 레벨).</summary>
        public double TankCapacityOf(Facility tank) => LevelOf(tank).Capacity ?? 0;

        private void ApplyWaterTanks(CanalGraph canals, WaterAllocation water, List<WaterSource> wells)
        {
            List<Facility> tanks = _facilities.Values.Where(x => x.Kind == BuildingKinds.WaterTank).OrderBy(x => x.Id).ToList();
            foreach (Facility t in tanks)
            {
                t.TankMode = WaterTankMode.Idle;
                t.TankFlowLastHour = 0;
                double cap = TankCapacityOf(t);
                if (t.StoredWater > cap) t.StoredWater = cap; // 레벨이 내려갈 일은 없지만 안전하게
            }
            List<Facility> operating = tanks.Where(t => t.IsOperating).ToList();
            if (operating.Count == 0) return;

            // 수로 변마다 이미 쓴 용량(변 입구 기준). 단계마다 더해 간다.
            var used = new Dictionary<EdgeId, double>(water.EdgeLoads.ToDictionary(kv => kv.Key, kv => kv.Value));
            Func<CanalSegment, double> baseCapacity = WaterNow.CapacityOf;
            var remaining = new WaterNetworkSettings
            {
                LossPerWholeEdge = WaterNow.LossPerWholeEdge,
                CapacityOf = seg => Math.Max(0, baseCapacity(seg) - (used.TryGetValue(seg.Edge, out double u) ? u : 0)),
            };

            // ── 2. 방출 ──
            // 하류는 수로 구조로 정한다: 닫혔거나 개량 중인 우물도 기준에 넣는다(우물을 닫아도 탱크가 하류를 잃지 않게).
            List<HexCoord> wellTiles = _facilities.Values
                .Where(x => x.Kind == BuildingKinds.Well && x.State != FacilityState.Constructing && x.State != FacilityState.Demolishing)
                .Select(x => x.Tile).ToList();
            DownstreamIndex downstream = new DownstreamIndex(canals, wellTiles);
            foreach (Facility t in operating)
            {
                if (t.StoredWater <= TankEpsilon) continue;
                var needs = new List<WaterConsumer>();
                foreach (WaterConsumerResult r in water.Consumers.Values.OrderBy(r => r.Id, StringComparer.Ordinal))
                {
                    double missing = r.Demand - r.Delivered;
                    if (missing <= TankEpsilon) continue;
                    Facility f = GetFacility(int.Parse(r.Id));
                    if (f == null || !downstream.IsDownstream(t.Tile, f.Tile)) continue;
                    needs.Add(new WaterConsumer(r.Id, f.Tile, missing));
                }
                if (needs.Count == 0) continue;

                double offer = Math.Min(t.StoredWater, LevelOf(t).DischargePerHour ?? 0);
                if (offer <= TankEpsilon) continue;
                string sourceId = "tank" + t.Id;
                WaterAllocation pass = WaterNetworkSolver.Solve(canals, new[] { new WaterSource(sourceId, t.Tile, offer) }, needs, remaining);
                double released = pass.SourceReleased.TryGetValue(sourceId, out double rel) ? rel : 0;
                if (released <= TankEpsilon) continue;

                Merge(water, pass, used);
                t.StoredWater = Math.Max(0, t.StoredWater - released);
                t.TankMode = WaterTankMode.Discharging;
                t.TankFlowLastHour = -released;
            }

            // ── 3. 충전 ──
            var chargers = operating.Where(t => t.TankMode == WaterTankMode.Idle && TankCapacityOf(t) - t.StoredWater > TankEpsilon).ToList();
            if (chargers.Count == 0) return;
            var leftovers = wells
                .Select(w => new WaterSource(w.Id, w.Tile, w.SupplyPerHour - (water.SourceReleased.TryGetValue(w.Id, out double r) ? r : 0)))
                .Where(w => w.SupplyPerHour > TankEpsilon).ToList();
            if (leftovers.Count == 0) return;
            var tankConsumers = chargers.Select(t => new WaterConsumer("tank" + t.Id, t.Tile,
                Math.Min(LevelOf(t).ChargePerHour ?? 0, TankCapacityOf(t) - t.StoredWater))).Where(c => c.DemandPerHour > TankEpsilon).ToList();
            if (tankConsumers.Count == 0) return;
            WaterAllocation charge = WaterNetworkSolver.Solve(canals, leftovers, tankConsumers, remaining);
            foreach (Facility t in chargers)
            {
                if (!charge.Consumers.TryGetValue("tank" + t.Id, out WaterConsumerResult got) || got.Delivered <= TankEpsilon) continue;
                t.StoredWater = Math.Min(TankCapacityOf(t), t.StoredWater + got.Delivered);
                t.TankMode = WaterTankMode.Charging;
                t.TankFlowLastHour = got.Delivered;
            }
            // 충전에 쓴 우물 방출·수로 흐름도 기록(우물 툴팁·수로 색). 탱크는 시설 결과 목록에 넣지 않는다.
            foreach (KeyValuePair<EdgeId, double> kv in charge.EdgeLoads) AddTo(water._edgeLoads, kv.Key, kv.Value);
            foreach (KeyValuePair<EdgeId, double> kv in charge.DirectIntakeLoads) AddTo(water._directIntakeLoads, kv.Key, kv.Value);
            foreach (KeyValuePair<string, double> kv in charge.SourceReleased) AddTo(water._sourceReleased, kv.Key, kv.Value);
            water.TotalReleased += charge.TotalReleased;
        }

        /// <summary>방출 결과를 이번 시간 배분에 합친다: 시설이 받은 물, 수로 흐름, 탱크 방출.</summary>
        private static void Merge(WaterAllocation water, WaterAllocation pass, Dictionary<EdgeId, double> used)
        {
            foreach (WaterConsumerResult got in pass.Consumers.Values)
            {
                if (got.Delivered <= TankEpsilon || !water._consumers.TryGetValue(got.Id, out WaterConsumerResult target)) continue;
                target.Delivered += got.Delivered;
                target._intakes.AddRange(got.Intakes);
                if (target.Demand - target.Delivered <= TankEpsilon)
                {
                    target.Shortage = WaterShortageReason.None;
                    target.BlockingEdge = null;
                    target.BlockingSourceId = null;
                }
            }
            foreach (KeyValuePair<EdgeId, double> kv in pass.EdgeLoads)
            {
                AddTo(water._edgeLoads, kv.Key, kv.Value);
                AddTo(used, kv.Key, kv.Value);
            }
            foreach (KeyValuePair<EdgeId, double> kv in pass.DirectIntakeLoads) AddTo(water._directIntakeLoads, kv.Key, kv.Value); // 용량은 안 씀(WD1)
            foreach (KeyValuePair<string, double> kv in pass.SourceReleased) AddTo(water._sourceReleased, kv.Key, kv.Value);
            water.TotalReleased += pass.TotalReleased;
            water.TotalDelivered += pass.TotalDelivered;
        }

        private static void AddTo<TKey>(Dictionary<TKey, double> d, TKey key, double value) =>
            d[key] = (d.TryGetValue(key, out double old) ? old : 0) + value;

        /// <summary>
        /// 물탱크 하류 판정(T3·T4). 닫힌 수로도 연 것으로 본 그래프에서, 시설 F의 가장 가까운 우물 W에 대해
        /// 탱크 타일의 꼭짓점(수로가 닿은 점) v 중 하나가 W → F 최단 경로 위에 있으면(거리(W, v) + 거리(v, F) = 거리(W, F)) 하류다.
        /// 거리는 수로 변 개수, 타일까지의 거리는 그 타일 꼭짓점 중 수로가 닿은 점까지의 최소값.
        /// 탱크보다 우물에 가까운 시설은 이 식을 만족할 수 없다(거리(W, v) ≥ 거리(W, 탱크) > 거리(W, F)).
        /// 꼭짓점 단위로 보는 이유: 탱크 타일은 수로에 여러 꼭짓점으로 닿아, 물이 탱크 둘레를 따라 흐를 수 있다.
        /// </summary>
        private sealed class DownstreamIndex
        {
            private readonly CanalGraph _open;
            private readonly HashSet<VertexId> _touched;
            private readonly List<Dictionary<VertexId, int>> _fromWells;
            private readonly Dictionary<HexCoord, Dictionary<VertexId, int>> _fromFacility = new Dictionary<HexCoord, Dictionary<VertexId, int>>();

            public DownstreamIndex(CanalGraph canals, List<HexCoord> wellTiles)
            {
                _open = new CanalGraph(canals.Topology);
                foreach (CanalSegment seg in canals.Segments)
                {
                    _open.TryInstall(seg.Edge);
                    _open.TrySetLevel(seg.Edge, seg.Level);
                }
                _touched = new HashSet<VertexId>(_open.Segments.SelectMany(s => new[] { s.Edge.VertexA, s.Edge.VertexB }));
                _fromWells = wellTiles.Select(w => _open.ComputePathLengths(Touched(w))).ToList();
            }

            private IEnumerable<VertexId> Touched(HexCoord tile) => HexTopology.VerticesOf(tile).Where(_touched.Contains);

            private int Distance(Dictionary<VertexId, int> from, HexCoord tile)
            {
                int best = int.MaxValue;
                foreach (VertexId v in Touched(tile))
                    if (from.TryGetValue(v, out int d) && d < best) best = d;
                return best;
            }

            public bool IsDownstream(HexCoord tank, HexCoord facility)
            {
                if (!_fromFacility.TryGetValue(facility, out Dictionary<VertexId, int> fromFacility))
                {
                    fromFacility = _open.ComputePathLengths(Touched(facility));
                    _fromFacility[facility] = fromFacility;
                }
                int nearest = _fromWells.Select(d => Distance(d, facility)).DefaultIfEmpty(int.MaxValue).Min();
                if (nearest == int.MaxValue) return false;
                List<VertexId> tankVertices = Touched(tank).ToList();
                foreach (Dictionary<VertexId, int> well in _fromWells)
                {
                    if (Distance(well, facility) != nearest) continue; // 가장 가까운 우물(들)만
                    foreach (VertexId v in tankVertices)
                    {
                        if (well.TryGetValue(v, out int toTank) && fromFacility.TryGetValue(v, out int toFacility) && toTank + toFacility == nearest) return true;
                    }
                }
                return false;
            }
        }
    }
}
