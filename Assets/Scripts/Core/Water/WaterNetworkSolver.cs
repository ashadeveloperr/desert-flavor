using System;
using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Canal;
using DesertSettlement.Core.Hex;

namespace DesertSettlement.Core.Water
{
    /// <summary>
    /// 수로망 물 배분 계산기. 확정 규칙(Docs/implementation_notes.md W1~W6)을 그대로 구현한다.
    ///
    /// 그래프: 노드 = 육각형 꼭짓점 + 수로 변 중간점, 간선 = 반쪽 변(길이 1, 손실 1-√(1-L)).
    ///   닫힌 수문이 있는 변은 그래프에서 빠진다.
    /// 1. 우물마다 타일 꼭짓점 6개(수로가 닿은 곳)에서 BFS → 우물별 최단 경로 트리 (W3, W5)
    /// 2. 시설마다 타일 꼭짓점 6개 중 수로가 닿은 점을 취수점으로, 모든 우물에 대한 경로 후보를 만든다 (W2, W4, W5′)
    ///    (W2 변경 2026-10-22: 변 중간점 → 꼭짓점. 수로가 시설 타일의 꼭짓점 하나에만 닿아도 물을 받는다)
    /// 3. 경로 길이(반쪽 변 수) 오름차순으로 그룹을 만들어 처리한다 — 상류 우선, B안 (W1)
    ///    그룹 안에서는 progressive filling: 모든 시설의 충족률을 같은 속도로 올리고(수요 비례),
    ///    수로 용량(변 입구 기준, W6)이나 우물 생산량에 걸린 시설은 그 자리에서 고정한다.
    ///    고정된 시설은 같은 길이의 다음 경로, 그다음 더 긴 경로로 나머지를 받는다.
    ///
    /// 결과는 입력 순서와 무관하게 결정적이다(동률은 거리 → 경로 병목 용량 큰 쪽 → 변 ID → 우물 ID).
    /// </summary>
    public static class WaterNetworkSolver
    {
        // 부동소수점 허용 오차. 비교하는 값의 크기가 달라 용도별로 따로 둔다(하나로 합치면 한쪽이 틀어진다).
        /// <summary>물의 양(시간당 단위) 비교: 남은 수요·흐름·공급이 "사실상 0"인지.</summary>
        private const double AmountEpsilon = 1e-9;
        /// <summary>0~1 비율(충족률 진행) 비교: 어느 제약이 먼저 닿는지, 동시에 닿는지, 충족률 1 도달.</summary>
        private const double RatioEpsilon = 1e-12;
        /// <summary>나눗셈 분모 보호: 사용 속도가 사실상 0이면 제약 계산에서 뺀다. 키우면 용량 초과 배분이 생길 수 있다.</summary>
        private const double RateEpsilon = 1e-15;

        private readonly struct Node : IEquatable<Node>
        {
            public readonly bool IsMid;
            public readonly VertexId Vertex;
            public readonly EdgeId Edge;

            private Node(bool isMid, VertexId vertex, EdgeId edge)
            {
                IsMid = isMid;
                Vertex = vertex;
                Edge = edge;
            }

            public static Node AtVertex(VertexId v) => new Node(false, v, default);
            public static Node AtMidpoint(EdgeId e) => new Node(true, default, e);

            public bool Equals(Node o) => IsMid == o.IsMid && (IsMid ? Edge == o.Edge : Vertex == o.Vertex);
            public override bool Equals(object obj) => obj is Node n && Equals(n);
            public override int GetHashCode() => IsMid ? HashCode.Combine(1, Edge) : HashCode.Combine(0, Vertex);
            public override string ToString() => IsMid ? "M" + Edge : Vertex.ToString();
        }

        private sealed class Arc
        {
            public Node From;
            public Node To;
            public EdgeId Edge;
        }

        private sealed class Route
        {
            public int Consumer;
            public int Source;
            public VertexId IntakeVertex;
            public string IntakeKey;
            public int Length;
            public double SourceFactor;                // 전달 1당 우물 방출량
            public List<KeyValuePair<EdgeId, double>> EdgeFactors = new List<KeyValuePair<EdgeId, double>>(); // 전달 1당 변 진입량
            public double Bottleneck;
            public double Delivered;
        }

        public static WaterAllocation Solve(CanalGraph canals, IReadOnlyList<WaterSource> sources,
            IReadOnlyList<WaterConsumer> consumers, WaterNetworkSettings settings)
        {
            if (canals == null) throw new ArgumentNullException(nameof(canals));
            sources = sources ?? Array.Empty<WaterSource>();
            consumers = consumers ?? Array.Empty<WaterConsumer>();
            settings = settings ?? new WaterNetworkSettings();
            if (settings.LossPerWholeEdge < 0 || settings.LossPerWholeEdge >= 1)
            {
                throw new ArgumentOutOfRangeException(nameof(settings), "LossPerWholeEdge는 0 이상 1 미만이어야 합니다.");
            }

            double halfGain = Math.Sqrt(1.0 - settings.LossPerWholeEdge);
            var allocation = new WaterAllocation();

            // 1) 그래프
            var keys = new Dictionary<Node, string>();
            var adjacency = new Dictionary<Node, List<Arc>>();
            var capacity = new Dictionary<EdgeId, double>();
            foreach (CanalSegment segment in canals.Segments)
            {
                if (!segment.IsPassable) continue;
                EdgeId e = segment.Edge;
                capacity[e] = Math.Max(0, settings.CapacityOf(segment));
                Node a = Node.AtVertex(e.VertexA);
                Node b = Node.AtVertex(e.VertexB);
                Node mid = Node.AtMidpoint(e);
                Link(adjacency, a, mid, e);
                Link(adjacency, mid, b, e);
            }
            foreach (Node node in adjacency.Keys) keys[node] = node.ToString();
            foreach (List<Arc> arcs in adjacency.Values) arcs.Sort((x, y) => string.CompareOrdinal(keys[x.To], keys[y.To]));

            // 우물은 Id 순으로 처리해 입력 순서와 무관하게 만든다.
            List<WaterSource> orderedSources = sources.OrderBy(s => s.Id, StringComparer.Ordinal).ToList();
            var trees = new List<Dictionary<Node, (int dist, Arc parent)>>();
            foreach (WaterSource source in orderedSources)
            {
                if (allocation._sourceReleased.ContainsKey(source.Id)) throw new ArgumentException($"우물 Id가 중복됩니다: {source.Id}");
                trees.Add(BuildTree(source.Tile, adjacency, keys));
                allocation._sourceReleased[source.Id] = 0;
                allocation.TotalSupply += source.SupplyPerHour;
            }

            // 2) 경로 후보
            var routes = new List<Route>();
            var routesByConsumer = new List<List<Route>>();
            for (int c = 0; c < consumers.Count; c++)
            {
                var candidates = new List<Route>();
                foreach (VertexId corner in HexTopology.VerticesOf(consumers[c].Tile))
                {
                    Node intake = Node.AtVertex(corner);
                    if (!adjacency.ContainsKey(intake)) continue;
                    for (int s = 0; s < orderedSources.Count; s++)
                    {
                        if (!trees[s].ContainsKey(intake)) continue;
                        candidates.Add(BuildRoute(c, s, intake, keys[intake], trees[s], halfGain, capacity));
                    }
                }
                candidates.Sort((x, y) =>
                {
                    int cmp = x.Length.CompareTo(y.Length);
                    if (cmp == 0) cmp = y.Bottleneck.CompareTo(x.Bottleneck);
                    if (cmp == 0) cmp = string.CompareOrdinal(x.IntakeKey, y.IntakeKey);
                    if (cmp == 0) cmp = x.Source.CompareTo(y.Source);
                    return cmp;
                });
                routesByConsumer.Add(candidates);
                routes.AddRange(candidates);

                var result = new WaterConsumerResult
                {
                    Id = consumers[c].Id,
                    Demand = consumers[c].DemandPerHour,
                    PriorityDistance = candidates.Count > 0 ? candidates[0].Length : -1,
                };
                if (allocation._consumers.ContainsKey(result.Id)) throw new ArgumentException($"시설 Id가 중복됩니다: {result.Id}");
                allocation._consumers[result.Id] = result;
            }

            // 3) 거리 그룹별 배분
            var delivered = new double[consumers.Count];
            var edgeUsed = new Dictionary<EdgeId, double>();
            var sourceUsed = new double[orderedSources.Count];
            var lastBlock = new (WaterShortageReason reason, EdgeId? edge, int source)[consumers.Count];

            foreach (IGrouping<int, Route> group in routes.GroupBy(r => r.Length).OrderBy(g => g.Key))
            {
                var used = new HashSet<Route>();
                while (true)
                {
                    // 시설마다 아직 안 쓴 가장 앞선 경로 하나씩.
                    var members = new List<Route>();
                    var seen = new HashSet<int>();
                    foreach (Route r in group)
                    {
                        if (used.Contains(r) || seen.Contains(r.Consumer)) continue;
                        if (consumers[r.Consumer].DemandPerHour - delivered[r.Consumer] <= AmountEpsilon) continue;
                        members.Add(r);
                        seen.Add(r.Consumer);
                    }
                    if (members.Count == 0) break;
                    used.UnionWith(members);
                    FillPass(members, consumers, orderedSources, capacity, delivered, edgeUsed, sourceUsed, lastBlock);
                }
            }

            // 4) 결과 정리
            for (int c = 0; c < consumers.Count; c++)
            {
                WaterConsumerResult result = allocation._consumers[consumers[c].Id];
                result.Delivered = Math.Min(delivered[c], consumers[c].DemandPerHour);
                foreach (Route r in routesByConsumer[c])
                {
                    if (r.Delivered <= AmountEpsilon) continue;
                    result._intakes.Add(new WaterIntakeFlow(r.IntakeVertex, orderedSources[r.Source].Id, r.Length, r.Delivered,
                        r.EdgeFactors.Select(ef => ef.Key).Distinct().ToList()));
                }

                if (consumers[c].DemandPerHour - delivered[c] <= AmountEpsilon) result.Shortage = WaterShortageReason.None;
                else if (routesByConsumer[c].Count == 0) result.Shortage = WaterShortageReason.Unreachable;
                else
                {
                    result.Shortage = lastBlock[c].reason;
                    result.BlockingEdge = lastBlock[c].edge;
                    result.BlockingSourceId = lastBlock[c].reason == WaterShortageReason.SourceSupply ? orderedSources[lastBlock[c].source].Id : null;
                }
            }

            foreach (KeyValuePair<EdgeId, double> kv in edgeUsed)
            {
                if (kv.Value > AmountEpsilon) allocation._edgeLoads[kv.Key] = kv.Value;
            }
            for (int s = 0; s < orderedSources.Count; s++)
            {
                allocation._sourceReleased[orderedSources[s].Id] = sourceUsed[s];
                allocation.TotalReleased += sourceUsed[s];
            }
            allocation.TotalDelivered = delivered.Sum();
            return allocation;
        }

        private static void Link(Dictionary<Node, List<Arc>> adjacency, Node a, Node b, EdgeId edge)
        {
            if (!adjacency.TryGetValue(a, out List<Arc> la)) adjacency[a] = la = new List<Arc>();
            if (!adjacency.TryGetValue(b, out List<Arc> lb)) adjacency[b] = lb = new List<Arc>();
            la.Add(new Arc { From = a, To = b, Edge = edge });
            lb.Add(new Arc { From = b, To = a, Edge = edge });
        }

        /// <summary>우물 타일 꼭짓점들에서 시작하는 BFS 최단 경로 트리.</summary>
        private static Dictionary<Node, (int dist, Arc parent)> BuildTree(HexCoord wellTile,
            Dictionary<Node, List<Arc>> adjacency, Dictionary<Node, string> keys)
        {
            var tree = new Dictionary<Node, (int, Arc)>();
            var queue = new Queue<Node>();
            List<Node> starts = HexTopology.VerticesOf(wellTile)
                .Select(Node.AtVertex)
                .Where(adjacency.ContainsKey)
                .OrderBy(n => keys[n], StringComparer.Ordinal)
                .ToList();
            foreach (Node start in starts)
            {
                if (tree.ContainsKey(start)) continue;
                tree[start] = (0, null);
                queue.Enqueue(start);
            }
            while (queue.Count > 0)
            {
                Node current = queue.Dequeue();
                int next = tree[current].Item1 + 1;
                foreach (Arc arc in adjacency[current])
                {
                    if (tree.ContainsKey(arc.To)) continue;
                    tree[arc.To] = (next, arc);
                    queue.Enqueue(arc.To);
                }
            }
            return tree;
        }

        private static Route BuildRoute(int consumer, int source, Node intake, string intakeKey,
            Dictionary<Node, (int dist, Arc parent)> tree, double halfGain, Dictionary<EdgeId, double> capacity)
        {
            var arcs = new List<Arc>();
            Node current = intake;
            while (tree[current].parent is Arc arc)
            {
                arcs.Add(arc);
                current = arc.From;
            }
            arcs.Reverse(); // 우물 → 취수점

            int n = arcs.Count;
            var route = new Route
            {
                Consumer = consumer,
                Source = source,
                IntakeVertex = intake.Vertex,
                IntakeKey = intakeKey,
                Length = n,
                SourceFactor = 1.0 / Math.Pow(halfGain, n),
                Bottleneck = double.PositiveInfinity,
            };
            for (int k = 0; k < n; k++)
            {
                EdgeId e = arcs[k].Edge;
                if (k > 0 && arcs[k - 1].Edge == e) continue; // 같은 변의 두 번째 반쪽
                // 변 입구에서 필요한 양 = 전달량 / (여기서부터 취수점까지의 이득)
                route.EdgeFactors.Add(new KeyValuePair<EdgeId, double>(e, 1.0 / Math.Pow(halfGain, n - k)));
                route.Bottleneck = Math.Min(route.Bottleneck, capacity[e]);
            }
            return route;
        }

        /// <summary>
        /// 한 그룹의 progressive filling. 모든 활성 경로의 충족률 t를 같은 속도로 올리다가,
        /// 변 용량·우물 생산량·수요 중 먼저 닿는 제약에서 해당 경로들을 고정한다.
        /// </summary>
        private static void FillPass(List<Route> members, IReadOnlyList<WaterConsumer> consumers, List<WaterSource> sources,
            Dictionary<EdgeId, double> capacity, double[] delivered, Dictionary<EdgeId, double> edgeUsed, double[] sourceUsed,
            (WaterShortageReason reason, EdgeId? edge, int source)[] lastBlock)
        {
            var target = new Dictionary<Route, double>();
            var progress = new Dictionary<Route, double>();
            var active = new List<Route>();
            foreach (Route r in members)
            {
                target[r] = Math.Max(0, consumers[r.Consumer].DemandPerHour - delivered[r.Consumer]);
                progress[r] = 0;
                if (target[r] > AmountEpsilon) active.Add(r);
            }

            int guard = 0;
            while (active.Count > 0 && guard++ < 100000)
            {
                double step = double.PositiveInfinity;
                foreach (Route r in active) step = Math.Min(step, 1.0 - progress[r]);
                EdgeId? bindingEdge = null;
                int bindingSource = -1;

                var edgeRate = new Dictionary<EdgeId, double>();
                var sourceRate = new Dictionary<int, double>();
                foreach (Route r in active)
                {
                    foreach (KeyValuePair<EdgeId, double> ef in r.EdgeFactors)
                    {
                        edgeRate.TryGetValue(ef.Key, out double v);
                        edgeRate[ef.Key] = v + ef.Value * target[r];
                    }
                    sourceRate.TryGetValue(r.Source, out double sv);
                    sourceRate[r.Source] = sv + r.SourceFactor * target[r];
                }
                foreach (KeyValuePair<EdgeId, double> er in edgeRate)
                {
                    if (er.Value <= RateEpsilon) continue;
                    edgeUsed.TryGetValue(er.Key, out double usedNow);
                    double dt = Math.Max(0, capacity[er.Key] - usedNow) / er.Value;
                    if (dt < step - RatioEpsilon)
                    {
                        step = dt;
                        bindingEdge = er.Key;
                        bindingSource = -1;
                    }
                }
                foreach (KeyValuePair<int, double> sr in sourceRate)
                {
                    if (sr.Value <= RateEpsilon) continue;
                    double dt = Math.Max(0, sources[sr.Key].SupplyPerHour - sourceUsed[sr.Key]) / sr.Value;
                    // 수로 용량과 우물 생산량이 동시에 닿으면 우물 부족을 이유로 보고한다(수로를 늘려도 소용없으므로).
                    bool tieWithEdge = bindingEdge.HasValue && Math.Abs(dt - step) <= RatioEpsilon;
                    if (dt < step - RatioEpsilon || tieWithEdge)
                    {
                        step = dt;
                        bindingEdge = null;
                        bindingSource = sr.Key;
                    }
                }

                foreach (Route r in active)
                {
                    double add = step * target[r];
                    progress[r] += step;
                    r.Delivered += add;
                    delivered[r.Consumer] += add;
                    sourceUsed[r.Source] += add * r.SourceFactor;
                    foreach (KeyValuePair<EdgeId, double> ef in r.EdgeFactors)
                    {
                        edgeUsed.TryGetValue(ef.Key, out double v);
                        edgeUsed[ef.Key] = v + add * ef.Value;
                    }
                }

                for (int i = active.Count - 1; i >= 0; i--)
                {
                    Route r = active[i];
                    bool full = progress[r] >= 1.0 - RatioEpsilon;
                    bool hitEdge = bindingEdge.HasValue && r.EdgeFactors.Exists(ef => ef.Key == bindingEdge.Value);
                    bool hitSource = bindingSource >= 0 && r.Source == bindingSource;
                    if (!full && !hitEdge && !hitSource) continue;
                    if (!full)
                    {
                        lastBlock[r.Consumer] = hitEdge
                            ? (WaterShortageReason.CanalCapacity, bindingEdge, -1)
                            : (WaterShortageReason.SourceSupply, null, bindingSource);
                    }
                    active.RemoveAt(i);
                }
            }
        }
    }
}
