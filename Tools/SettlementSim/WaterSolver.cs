using System;
using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Canal;
using DesertSettlement.Core.Hex;

namespace SettlementSim
{
    // 물 배분 규칙 후보. 시뮬레이션으로 비교하기 위해 옵션으로 둔다.
    public enum ReleaseMode { TileCorners, EdgeMidpoints }      // W3: 우물 방출 지점
    public enum IntakeMode { EdgeMidpoint, TileCorner }         // W2: 시설 취수 지점
    public enum RoutingMode { SingleIntake, MultiIntake }       // W4: 취수 경로 수
    public enum SourceMode { NearestWellOnly, AllWells }        // W5: 여러 우물이 이어진 경우
    public enum CapacityMode { Entry, Exit }                    // W6: 수로 용량 측정 위치

    public sealed class WaterOptions
    {
        public ReleaseMode Release = ReleaseMode.TileCorners;
        public IntakeMode Intake = IntakeMode.EdgeMidpoint;
        public RoutingMode Routing = RoutingMode.SingleIntake;
        public SourceMode Sources = SourceMode.NearestWellOnly;
        public CapacityMode Capacity = CapacityMode.Entry;
        public double LossPerEdge = 0.05;
        /// <summary>변의 용량. null이면 100.</summary>
        public Func<EdgeId, double> EdgeCapacity;

        public override string ToString() =>
            $"방출={Release}, 취수={Intake}, 경로={Routing}, 우물={Sources}, 용량={Capacity}";
    }

    public sealed class WellSpec
    {
        public string Name;
        public HexCoord Tile;
        public double Supply;
    }

    public sealed class ConsumerSpec
    {
        public string Name;
        public HexCoord Tile;
        public double Demand;
    }

    public sealed class WaterResult
    {
        public Dictionary<string, double> Delivered = new Dictionary<string, double>();
        public Dictionary<string, double> Rate = new Dictionary<string, double>();
        /// <summary>시설별 주 취수 경로 길이(반쪽 변 단위). 도달 불가면 -1.</summary>
        public Dictionary<string, int> Distance = new Dictionary<string, int>();
        public Dictionary<string, string> Intake = new Dictionary<string, string>();
        public Dictionary<EdgeId, double> EdgeLoad = new Dictionary<EdgeId, double>();
        public Dictionary<string, double> WellReleased = new Dictionary<string, double>();
        public double TotalSupply, TotalReleased, TotalDelivered;
        public double TotalLoss => TotalReleased - TotalDelivered;
        /// <summary>시설별로 막힌 이유(병목). 디버그·불변식 검사용.</summary>
        public Dictionary<string, string> BlockedBy = new Dictionary<string, string>();
        public bool HasCycle;
        public int ConnectedWellGroups;
    }

    /// <summary>
    /// 범용 수로망 물 배분기(프로토타입). 노드 = 꼭짓점 + 수로 변 중간점, 간선 = 반쪽 변.
    /// 1) 우물마다 방출 지점에서 BFS → 최단 경로 트리 (반쪽 변 길이 1, 동률은 결정적 ID 순)
    /// 2) 시설마다 취수 후보(인접 수로 변 중간점 또는 타일 꼭짓점)와 경로 길이 계산
    /// 3) 경로 길이 오름차순 그룹(B안 상류 우선)마다 progressive filling:
    ///    같은 그룹은 수요 충족률을 같은 속도로 올리고, 수로 용량·우물 생산량 제약에 걸린 시설은 고정
    /// </summary>
    public static class WaterSolver
    {
        private readonly struct Node : IEquatable<Node>
        {
            public readonly bool IsMid;
            public readonly VertexId V;
            public readonly EdgeId E;
            private Node(bool isMid, VertexId v, EdgeId e) { IsMid = isMid; V = v; E = e; }
            public static Node Vertex(VertexId v) => new Node(false, v, default);
            public static Node Mid(EdgeId e) => new Node(true, default, e);
            public bool Equals(Node o) => IsMid == o.IsMid && (IsMid ? E == o.E : V == o.V);
            public override bool Equals(object obj) => obj is Node n && Equals(n);
            public override int GetHashCode() => IsMid ? HashCode.Combine(1, E) : HashCode.Combine(0, V);
            public string Key => IsMid ? "M" + E : V.ToString();
            public override string ToString() => Key;
        }

        private sealed class Arc
        {
            public Node From, To;
            public EdgeId Edge;
        }

        private sealed class Tree
        {
            public Dictionary<Node, int> Dist = new Dictionary<Node, int>();
            public Dictionary<Node, Arc> Parent = new Dictionary<Node, Arc>();
        }

        private sealed class Route
        {
            public int Consumer;
            public int Well;
            public Node Intake;
            public int Length;               // 반쪽 변 개수
            public double SourceFactor;      // 전달 1당 우물에서 내보내야 하는 양
            public Dictionary<EdgeId, double> EdgeFactor = new Dictionary<EdgeId, double>();
            public double Bottleneck;        // 경로 최소 용량
        }

        public static WaterResult Solve(CanalGraph canals, IList<WellSpec> wells, IList<ConsumerSpec> consumers, WaterOptions options)
        {
            double halfGain = Math.Sqrt(1.0 - options.LossPerEdge);
            Func<EdgeId, double> capacity = options.EdgeCapacity ?? (_ => 100.0);

            // 그래프 구성: 통과 가능한(수문 열림) 수로만.
            var adjacency = new Dictionary<Node, List<Arc>>();
            void AddArc(Node a, Node b, EdgeId e)
            {
                if (!adjacency.TryGetValue(a, out var la)) adjacency[a] = la = new List<Arc>();
                if (!adjacency.TryGetValue(b, out var lb)) adjacency[b] = lb = new List<Arc>();
                la.Add(new Arc { From = a, To = b, Edge = e });
                lb.Add(new Arc { From = b, To = a, Edge = e });
            }
            foreach (CanalSegment seg in canals.Segments.OrderBy(s => s.Edge.ToString(), StringComparer.Ordinal))
            {
                if (!seg.IsPassable) continue;
                Node mid = Node.Mid(seg.Edge);
                AddArc(Node.Vertex(seg.Edge.VertexA), mid, seg.Edge);
                AddArc(mid, Node.Vertex(seg.Edge.VertexB), seg.Edge);
            }
            foreach (List<Arc> list in adjacency.Values) list.Sort((x, y) => string.CompareOrdinal(x.To.Key, y.To.Key));

            var result = new WaterResult { HasCycle = canals.ContainsCycle() };

            // 우물별 최단 경로 트리.
            var trees = new List<Tree>();
            foreach (WellSpec well in wells)
            {
                var tree = new Tree();
                var queue = new Queue<Node>();
                foreach (Node s in ReleaseNodes(well.Tile, options.Release, adjacency).OrderBy(n => n.Key, StringComparer.Ordinal))
                {
                    if (tree.Dist.ContainsKey(s)) continue;
                    tree.Dist[s] = 0;
                    queue.Enqueue(s);
                }
                while (queue.Count > 0)
                {
                    Node cur = queue.Dequeue();
                    if (!adjacency.TryGetValue(cur, out var arcs)) continue;
                    foreach (Arc arc in arcs)
                    {
                        if (tree.Dist.ContainsKey(arc.To)) continue;
                        tree.Dist[arc.To] = tree.Dist[cur] + 1;
                        tree.Parent[arc.To] = arc;
                        queue.Enqueue(arc.To);
                    }
                }
                trees.Add(tree);
                result.WellReleased[well.Name] = 0;
                result.TotalSupply += well.Supply;
            }

            // 서로 이어진 우물 묶음 수(정보용).
            var groups = new List<HashSet<int>>();
            for (int w = 0; w < wells.Count; w++)
            {
                HashSet<int> g = groups.FirstOrDefault(x => x.Any(o => trees[o].Dist.Keys.Any(trees[w].Dist.ContainsKey)));
                if (g == null) groups.Add(new HashSet<int> { w }); else g.Add(w);
            }
            result.ConnectedWellGroups = groups.Count;

            // 시설별 경로 후보.
            var routes = new List<Route>();
            for (int c = 0; c < consumers.Count; c++)
            {
                ConsumerSpec consumer = consumers[c];
                var candidates = new List<Route>();
                foreach (Node intake in IntakeNodes(consumer.Tile, options.Intake, adjacency))
                {
                    for (int w = 0; w < wells.Count; w++)
                    {
                        if (!trees[w].Dist.ContainsKey(intake)) continue;
                        candidates.Add(BuildRoute(c, w, intake, trees[w], halfGain, capacity, options.Capacity));
                    }
                }

                if (options.Sources == SourceMode.NearestWellOnly && candidates.Count > 0)
                {
                    // 각 취수점은 가장 가까운 우물에만 속한다(동률은 우물 순서).
                    candidates = candidates.GroupBy(r => r.Intake)
                        .Select(g => g.OrderBy(r => r.Length).ThenBy(r => r.Well).First()).ToList();
                }

                candidates = candidates
                    .OrderBy(r => r.Length)
                    .ThenByDescending(r => r.Bottleneck)
                    .ThenBy(r => r.Intake.Key, StringComparer.Ordinal)
                    .ThenBy(r => r.Well)
                    .ToList();

                if (options.Routing == RoutingMode.SingleIntake && candidates.Count > 1) candidates = candidates.Take(1).ToList();

                routes.AddRange(candidates);
                result.Distance[consumer.Name] = candidates.Count > 0 ? candidates[0].Length : -1;
                result.Intake[consumer.Name] = candidates.Count > 0 ? $"{candidates[0].Intake}@{wells[candidates[0].Well].Name}" : "-";
                result.Delivered[consumer.Name] = 0;
            }

            // 제약: 변 용량, 우물 생산량.
            var edgeUsed = new Dictionary<EdgeId, double>();
            var wellUsed = new double[wells.Count];
            var delivered = new double[consumers.Count];

            foreach (IGrouping<int, Route> group in routes.GroupBy(r => r.Length).OrderBy(g => g.Key))
            {
                // 같은 길이의 경로가 시설당 여러 개면(MultiIntake) 한 번에 하나씩: 앞 경로가 막히면 다음 경로로 다시 채운다.
                var usedRoutes = new HashSet<Route>();
                while (true)
                {
                    List<Route> members = group
                        .Where(r => !usedRoutes.Contains(r) && consumers[r.Consumer].Demand - delivered[r.Consumer] > 1e-9)
                        .GroupBy(r => r.Consumer).Select(g => g.First()).ToList();
                    if (members.Count == 0) break;
                    usedRoutes.UnionWith(members);
                    FillPass(members);
                }
            }

            void FillPass(List<Route> members)
            {
                var target = members.ToDictionary(r => r, r => Math.Max(0, consumers[r.Consumer].Demand - delivered[r.Consumer]));
                var t = members.ToDictionary(r => r, r => 0.0);
                var active = new HashSet<Route>(members.Where(r => target[r] > 1e-12));

                int guard = 0;
                while (active.Count > 0 && guard++ < 10000)
                {
                    double best = active.Min(r => 1.0 - t[r]);
                    string reason = "full";
                    object binding = null;

                    foreach (EdgeId e in active.SelectMany(r => r.EdgeFactor.Keys).Distinct())
                    {
                        double rate = active.Sum(r => r.EdgeFactor.TryGetValue(e, out double f) ? f * target[r] : 0);
                        if (rate <= 1e-15) continue;
                        double slack = capacity(e) - edgeUsed.GetValueOrDefault(e);
                        double dt = Math.Max(0, slack) / rate;
                        if (dt < best - 1e-12) { best = dt; reason = "edge " + e; binding = e; }
                    }
                    foreach (int w in active.Select(r => r.Well).Distinct())
                    {
                        double rate = active.Where(r => r.Well == w).Sum(r => r.SourceFactor * target[r]);
                        if (rate <= 1e-15) continue;
                        double dt = Math.Max(0, wells[w].Supply - wellUsed[w]) / rate;
                        if (dt < best - 1e-12) { best = dt; reason = "well " + wells[w].Name; binding = w; }
                    }

                    foreach (Route r in active)
                    {
                        double add = best * target[r];
                        t[r] += best;
                        delivered[r.Consumer] += add;
                        wellUsed[r.Well] += add * r.SourceFactor;
                        foreach (var kv in r.EdgeFactor) edgeUsed[kv.Key] = edgeUsed.GetValueOrDefault(kv.Key) + add * kv.Value;
                    }

                    List<Route> frozen = active.Where(r =>
                        t[r] >= 1.0 - 1e-12 ||
                        (binding is EdgeId be && r.EdgeFactor.ContainsKey(be)) ||
                        (binding is int bw && r.Well == bw)).ToList();
                    foreach (Route r in frozen)
                    {
                        active.Remove(r);
                        if (t[r] < 1.0 - 1e-12) result.BlockedBy[consumers[r.Consumer].Name] = reason;
                    }
                }
            }

            for (int c = 0; c < consumers.Count; c++)
            {
                result.Delivered[consumers[c].Name] = delivered[c];
                result.Rate[consumers[c].Name] = consumers[c].Demand > 0 ? Math.Min(1.0, delivered[c] / consumers[c].Demand) : 1.0;
                if (delivered[c] >= consumers[c].Demand - 1e-9) result.BlockedBy.Remove(consumers[c].Name);
            }
            result.EdgeLoad = edgeUsed;
            for (int w = 0; w < wells.Count; w++) result.WellReleased[wells[w].Name] = wellUsed[w];
            result.TotalReleased = wellUsed.Sum();
            result.TotalDelivered = delivered.Sum();
            return result;
        }

        private static IEnumerable<Node> ReleaseNodes(HexCoord well, ReleaseMode mode, Dictionary<Node, List<Arc>> adjacency)
        {
            if (mode == ReleaseMode.TileCorners)
            {
                foreach (VertexId v in HexTopology.VerticesOf(well))
                {
                    Node n = Node.Vertex(v);
                    if (adjacency.ContainsKey(n)) yield return n;
                }
            }
            else
            {
                foreach (EdgeId e in HexTopology.EdgesOf(well))
                {
                    Node n = Node.Mid(e);
                    if (adjacency.ContainsKey(n)) yield return n;
                }
            }
        }

        private static IEnumerable<Node> IntakeNodes(HexCoord tile, IntakeMode mode, Dictionary<Node, List<Arc>> adjacency) =>
            mode == IntakeMode.EdgeMidpoint
                ? HexTopology.EdgesOf(tile).Select(Node.Mid).Where(adjacency.ContainsKey)
                : HexTopology.VerticesOf(tile).Select(Node.Vertex).Where(adjacency.ContainsKey);

        private static Route BuildRoute(int consumer, int well, Node intake, Tree tree, double g,
            Func<EdgeId, double> capacity, CapacityMode capMode)
        {
            // 우물 → 취수점 방향의 반쪽 변 목록.
            var arcs = new List<Arc>();
            Node cur = intake;
            while (tree.Parent.TryGetValue(cur, out Arc arc))
            {
                arcs.Add(arc);
                cur = arc.From;
            }
            arcs.Reverse();

            int n = arcs.Count;
            var route = new Route
            {
                Consumer = consumer,
                Well = well,
                Intake = intake,
                Length = n,
                SourceFactor = 1.0 / Math.Pow(g, n),
                Bottleneck = double.PositiveInfinity,
            };
            for (int k = 0; k < n; k++)
            {
                EdgeId e = arcs[k].Edge;
                int first = arcs.FindIndex(a => a.Edge == e);
                int last = arcs.FindLastIndex(a => a.Edge == e);
                if (k != first) continue;
                // Entry: 변에 들어가는 순간(손실 전). Exit: 변의 마지막 반쪽을 지난 뒤(손실 후).
                int remainingArcs = capMode == CapacityMode.Entry ? n - first : n - last - 1;
                route.EdgeFactor[e] = 1.0 / Math.Pow(g, remainingArcs);
                route.Bottleneck = Math.Min(route.Bottleneck, capacity(e));
            }
            if (n == 0) route.Bottleneck = double.PositiveInfinity;
            return route;
        }
    }
}
