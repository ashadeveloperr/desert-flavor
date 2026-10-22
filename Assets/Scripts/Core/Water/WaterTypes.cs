using System;
using System.Collections.Generic;
using DesertSettlement.Core.Canal;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Hex;

namespace DesertSettlement.Core.Water
{
    /// <summary>물 공급원(우물). 공급량은 게임 1시간당. 운영 중이 아닌 우물은 넘기지 않는다.</summary>
    public sealed class WaterSource
    {
        public string Id { get; }
        public HexCoord Tile { get; }
        public double SupplyPerHour { get; }

        public WaterSource(string id, HexCoord tile, double supplyPerHour)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Tile = tile;
            SupplyPerHour = Math.Max(0, supplyPerHour);
        }
    }

    /// <summary>물을 쓰는 시설. 수요는 게임 1시간당. 폐쇄·개량 중인 시설은 넘기지 않거나 수요 0으로 넘긴다.</summary>
    public sealed class WaterConsumer
    {
        public string Id { get; }
        public HexCoord Tile { get; }
        public double DemandPerHour { get; }

        public WaterConsumer(string id, HexCoord tile, double demandPerHour)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Tile = tile;
            DemandPerHour = Math.Max(0, demandPerHour);
        }
    }

    /// <summary>
    /// 물 배분에 쓰는 숫자. 규칙(어디서 방출·취수하는지 등)은 <see cref="WaterNetworkSolver"/>에 고정돼 있고,
    /// 여기에는 플레이테스트로 바꿀 값만 둔다. 보통 <see cref="FromConfig"/>로 설정 JSON에서 만든다.
    /// </summary>
    public sealed class WaterNetworkSettings
    {
        /// <summary>수로 변 하나를 끝까지 지날 때의 손실률(0.05 = 5%). 반쪽 변은 1-√(1-L).</summary>
        public double LossPerWholeEdge { get; set; } = 0.05;

        /// <summary>수로 구간의 시간당 용량(변 입구 기준). 기본값은 모든 구간 100.</summary>
        public Func<CanalSegment, double> CapacityOf { get; set; } = _ => 100.0;

        /// <summary>설정 JSON 값으로 만든다: 손실 = prototype.canalLossPerWholeEdge, 용량 = canalEdge 레벨의 capacityPerHour.</summary>
        public static WaterNetworkSettings FromConfig(EconomyConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            return new WaterNetworkSettings
            {
                LossPerWholeEdge = config.Prototype?.CanalLossPerWholeEdge ?? 0.05,
                CapacityOf = segment => config.GetLevel(BuildingKinds.CanalEdge, segment.Level)?.CapacityPerHour ?? 0.0,
            };
        }
    }

    /// <summary>시설이 다 받지 못한 이유.</summary>
    public enum WaterShortageReason
    {
        /// <summary>수요를 모두 받음.</summary>
        None,
        /// <summary>인접한 수로가 없거나 어떤 우물과도 이어지지 않음.</summary>
        Unreachable,
        /// <summary>경로의 수로 용량이 가득 참(<see cref="WaterConsumerResult.BlockingEdge"/>).</summary>
        CanalCapacity,
        /// <summary>우물 생산량이 바닥남(<see cref="WaterConsumerResult.BlockingSourceId"/>).</summary>
        SourceSupply,
    }

    /// <summary>시설 하나가 한 경로(취수 꼭짓점 + 우물)로 받은 물.</summary>
    public readonly struct WaterIntakeFlow
    {
        /// <summary>물을 받은 시설 타일의 꼭짓점(수로가 닿은 점).</summary>
        public readonly VertexId IntakeVertex;
        public readonly string SourceId;
        /// <summary>우물 방출 꼭짓점 → 취수 꼭짓점까지 반쪽 변 개수(변 하나 = 2).</summary>
        public readonly int Distance;
        public readonly double Delivered;

        public WaterIntakeFlow(VertexId intakeVertex, string sourceId, int distance, double delivered)
        {
            IntakeVertex = intakeVertex;
            SourceId = sourceId;
            Distance = distance;
            Delivered = delivered;
        }
    }

    public sealed class WaterConsumerResult
    {
        public string Id { get; internal set; }
        public double Demand { get; internal set; }
        public double Delivered { get; internal set; }

        /// <summary>물 기본 가동률 = min(1, 받은 양 / 수요). 수요 0이면 1.</summary>
        public double Rate => Demand > 0 ? Math.Min(1.0, Delivered / Demand) : 1.0;

        /// <summary>공급 순위 거리(가장 가까운 경로의 반쪽 변 개수). 도달 불가면 -1.</summary>
        public int PriorityDistance { get; internal set; } = -1;

        /// <summary>실제로 물이 들어온 경로들(가까운 순).</summary>
        public IReadOnlyList<WaterIntakeFlow> Intakes => _intakes;
        internal readonly List<WaterIntakeFlow> _intakes = new List<WaterIntakeFlow>();

        public WaterShortageReason Shortage { get; internal set; }
        public EdgeId? BlockingEdge { get; internal set; }
        public string BlockingSourceId { get; internal set; }
    }

    /// <summary>한 번의 배분 계산 결과. 수로망·시설 상태가 바뀔 때마다 다시 계산한다.</summary>
    public sealed class WaterAllocation
    {
        internal readonly Dictionary<string, WaterConsumerResult> _consumers = new Dictionary<string, WaterConsumerResult>();
        internal readonly Dictionary<EdgeId, double> _edgeLoads = new Dictionary<EdgeId, double>();
        internal readonly Dictionary<string, double> _sourceReleased = new Dictionary<string, double>();

        public IReadOnlyDictionary<string, WaterConsumerResult> Consumers => _consumers;
        /// <summary>수로 변별 진입 유량 합(용량과 같은 기준). 물이 흐르지 않는 변은 없다.</summary>
        public IReadOnlyDictionary<EdgeId, double> EdgeLoads => _edgeLoads;
        /// <summary>우물별 실제 방출량.</summary>
        public IReadOnlyDictionary<string, double> SourceReleased => _sourceReleased;

        public double TotalSupply { get; internal set; }
        public double TotalReleased { get; internal set; }
        public double TotalDelivered { get; internal set; }
        /// <summary>수로 손실 합 = 방출 - 전달.</summary>
        public double TotalLoss => TotalReleased - TotalDelivered;

        /// <summary>시설의 물 기본 가동률. 모르는 Id면 0.</summary>
        public double RateOf(string consumerId) =>
            _consumers.TryGetValue(consumerId, out WaterConsumerResult r) ? r.Rate : 0.0;
    }
}
