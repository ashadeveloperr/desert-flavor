using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Hex;

namespace DesertSettlement.Core.Water
{
    /// <summary>
    /// 물길 표시용 조회(2026-10-23). 물 배분 결과에서 '이 시설이 받은 물이 지난 수로'와 '이 공급원(우물·물탱크)의 물이 지난 수로'를 꺼낸다.
    /// 시설·공급원 id는 물 배분에 넘긴 문자열 그대로(시설 id 숫자, 물탱크 공급원은 "tank" + id).
    /// </summary>
    public static class WaterRoutes
    {
        /// <summary>consumerId가 받은 물의 경로들(가까운 순).</summary>
        public static IReadOnlyList<WaterIntakeFlow> ReceivedBy(WaterAllocation water, string consumerId) =>
            water != null && water.Consumers.TryGetValue(consumerId, out WaterConsumerResult r) ? r.Intakes : new List<WaterIntakeFlow>();

        /// <summary>sourceId(우물 id 또는 "tank" + id)에서 나간 물의 모든 경로.</summary>
        public static List<WaterIntakeFlow> SentBy(WaterAllocation water, string sourceId) =>
            water == null ? new List<WaterIntakeFlow>() : water.Consumers.Values.SelectMany(r => r.Intakes).Where(f => f.SourceId == sourceId).ToList();

        /// <summary>경로들이 지난 수로 변(중복 없이).</summary>
        public static HashSet<EdgeId> EdgesOf(IEnumerable<WaterIntakeFlow> flows) => new HashSet<EdgeId>(flows.SelectMany(f => f.Edges));

        /// <summary>consumerId가 수로 용량 때문에 물을 덜 받았다면 그 막힌 수로.</summary>
        public static EdgeId? BlockingEdgeOf(WaterAllocation water, string consumerId) =>
            water != null && water.Consumers.TryGetValue(consumerId, out WaterConsumerResult r) && r.Shortage == WaterShortageReason.CanalCapacity
                ? r.BlockingEdge
                : null;
    }
}
