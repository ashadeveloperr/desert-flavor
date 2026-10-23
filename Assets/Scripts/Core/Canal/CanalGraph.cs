using System;
using System.Collections.Generic;
using DesertSettlement.Core.Hex;

namespace DesertSettlement.Core.Canal
{
    /// <summary>
    /// 변 위에 설치된 수로 한 구간. '수문'(HasGate·GateOpen)은 이 변을 막는 2단계(개방/차단) 표시다.
    /// 2026-10-23 플레이어용 수문 시설은 폐지됐고, 지금은 <b>닫힌 수로</b>를 막힌 변으로 표현하는 데 쓴다(Settlement.SolveWater).
    /// 용량·손실·개량 비용 같은 수치는 여기 두지 않고 설정(<c>buildingLevels.canalEdge</c>)에서 레벨로 찾는다.
    /// </summary>
    public sealed class CanalSegment
    {
        public EdgeId Edge { get; }
        /// <summary>수로 레벨(1부터). 설정의 canalEdge 레벨 배열 인덱스 = Level - 1.</summary>
        public int Level { get; internal set; } = 1;
        public bool HasGate { get; internal set; }
        public bool GateOpen { get; internal set; } = true;

        /// <summary>물이 지나갈 수 있는지. 수문이 없거나 열려 있으면 true.</summary>
        public bool IsPassable => !HasGate || GateOpen;

        internal CanalSegment(EdgeId edge)
        {
            Edge = edge;
        }
    }

    /// <summary>
    /// 수로망의 구조(어느 변에 수로가 있고, 어떻게 이어지는지)만 다룬다.
    /// 물을 얼마나 보낼지(상류 우선 배분·용량·손실)는 다음 단계의 배분 계산기가 이 그래프를 읽어 처리한다.
    /// 경로 길이 단위는 '변 개수'이며, 변 중간 취수점(CanalIntake)은 아직 반영하지 않는다(잠정 규칙).
    /// </summary>
    public sealed class CanalGraph
    {
        private readonly Dictionary<EdgeId, CanalSegment> _segments = new Dictionary<EdgeId, CanalSegment>();

        public HexTopology Topology { get; }
        public IEnumerable<CanalSegment> Segments => _segments.Values;
        public int SegmentCount => _segments.Count;

        /// <summary>수로 설치·제거·수문 변경 등 구조가 바뀔 때마다 발생. 물 배분 재계산 트리거로 쓴다.</summary>
        public event Action Changed;

        public CanalGraph(HexTopology topology)
        {
            Topology = topology ?? throw new ArgumentNullException(nameof(topology));
        }

        public bool IsInstalled(EdgeId edge) => _segments.ContainsKey(edge);

        public bool TryGetSegment(EdgeId edge, out CanalSegment segment) => _segments.TryGetValue(edge, out segment);

        /// <summary>
        /// 수로를 설치한다. 맵 밖 변이거나 이미 설치돼 있으면 false.
        /// 경계 변에도 설치할 수 있다(확정). 자원 차감·공사 시간은 건설 시스템이 담당한다.
        /// </summary>
        public bool TryInstall(EdgeId edge)
        {
            if (!Topology.Contains(edge) || _segments.ContainsKey(edge)) return false;
            _segments.Add(edge, new CanalSegment(edge));
            Changed?.Invoke();
            return true;
        }

        public bool Remove(EdgeId edge)
        {
            if (!_segments.Remove(edge)) return false;
            Changed?.Invoke();
            return true;
        }

        public bool TrySetLevel(EdgeId edge, int level)
        {
            if (level < 1 || !_segments.TryGetValue(edge, out CanalSegment segment)) return false;
            segment.Level = level;
            Changed?.Invoke();
            return true;
        }

        /// <summary>수로가 있는 변에 수문을 달거나 뗀다. 새로 단 수문은 열린 상태.</summary>
        public bool TrySetGate(EdgeId edge, bool hasGate)
        {
            if (!_segments.TryGetValue(edge, out CanalSegment segment)) return false;
            segment.HasGate = hasGate;
            segment.GateOpen = true;
            Changed?.Invoke();
            return true;
        }

        /// <summary>수문 개방/차단. 수문이 없는 변이면 false.</summary>
        public bool TrySetGateOpen(EdgeId edge, bool open)
        {
            if (!_segments.TryGetValue(edge, out CanalSegment segment) || !segment.HasGate) return false;
            segment.GateOpen = open;
            Changed?.Invoke();
            return true;
        }

        /// <summary>타일의 여섯 변 중 수로가 설치된 변. 시설 자동 취수 후보.</summary>
        public IEnumerable<CanalSegment> SegmentsAdjacentTo(HexCoord tile)
        {
            foreach (EdgeId edge in HexTopology.EdgesOf(tile))
            {
                if (_segments.TryGetValue(edge, out CanalSegment segment)) yield return segment;
            }
        }

        /// <summary>
        /// 시작 꼭짓점들에서 통과 가능한 수로만 따라갔을 때 각 꼭짓점까지의 최단 경로 길이(변 개수).
        /// 닫힌 수문은 지나가지 않는다. 결과에 없는 꼭짓점은 도달 불가.
        /// </summary>
        public Dictionary<VertexId, int> ComputePathLengths(IEnumerable<VertexId> sources)
        {
            var distances = new Dictionary<VertexId, int>();
            var queue = new Queue<VertexId>();

            foreach (VertexId source in sources)
            {
                if (!Topology.Contains(source) || distances.ContainsKey(source)) continue;
                distances.Add(source, 0);
                queue.Enqueue(source);
            }

            while (queue.Count > 0)
            {
                VertexId current = queue.Dequeue();
                int next = distances[current] + 1;
                foreach (EdgeId edge in Topology.EdgesOf(current))
                {
                    if (!_segments.TryGetValue(edge, out CanalSegment segment) || !segment.IsPassable) continue;
                    VertexId other = edge.VertexA == current ? edge.VertexB : edge.VertexA;
                    if (distances.ContainsKey(other)) continue;
                    distances.Add(other, next);
                    queue.Enqueue(other);
                }
            }

            return distances;
        }

        /// <summary>
        /// 설치된 수로끼리 이어진 덩어리별 변 목록(수문 상태 무시, 물리적 연결 기준).
        /// </summary>
        public List<List<EdgeId>> ConnectedComponents()
        {
            var visited = new HashSet<EdgeId>();
            var components = new List<List<EdgeId>>();

            foreach (EdgeId start in _segments.Keys)
            {
                if (!visited.Add(start)) continue;
                var component = new List<EdgeId>();
                var stack = new Stack<EdgeId>();
                stack.Push(start);
                while (stack.Count > 0)
                {
                    EdgeId edge = stack.Pop();
                    component.Add(edge);
                    foreach (VertexId v in new[] { edge.VertexA, edge.VertexB })
                    {
                        foreach (EdgeId neighbor in Topology.EdgesOf(v))
                        {
                            if (_segments.ContainsKey(neighbor) && visited.Add(neighbor)) stack.Push(neighbor);
                        }
                    }
                }
                components.Add(component);
            }

            return components;
        }

        /// <summary>
        /// 설치된 수로 안에 순환(고리)이 있는지. 수문 상태와 무관하게 물리적 구조로 판단한다.
        /// MVP에서 순환을 금지할지는 미정이므로, 여기서는 감지만 하고 막지 않는다.
        /// </summary>
        public bool ContainsCycle()
        {
            var parent = new Dictionary<VertexId, VertexId>();

            VertexId Find(VertexId v)
            {
                if (!parent.TryGetValue(v, out VertexId p))
                {
                    parent[v] = v;
                    return v;
                }
                if (p == v) return v;
                VertexId root = Find(p);
                parent[v] = root;
                return root;
            }

            foreach (EdgeId edge in _segments.Keys)
            {
                VertexId a = Find(edge.VertexA);
                VertexId b = Find(edge.VertexB);
                if (a == b) return true;
                parent[a] = b;
            }
            return false;
        }
    }
}
