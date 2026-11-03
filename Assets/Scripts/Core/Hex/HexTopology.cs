using System.Collections.Generic;

namespace DesertSettlement.Core.Hex
{
    /// <summary>
    /// 맵 안의 타일·꼭짓점·변 사이의 관계표. 맵 바깥 경계의 변·꼭짓점도 포함한다(경계선에도 수로 설치 가능).
    /// 생성 시 한 번 계산하고 이후에는 조회만 한다. 반경 3 맵: 꼭짓점 96개, 변 132개.
    /// </summary>
    public sealed class HexTopology
    {
        private static readonly IReadOnlyList<HexCoord> NoTiles = new HexCoord[0];
        private static readonly IReadOnlyList<EdgeId> NoEdges = new EdgeId[0];

        private readonly Dictionary<VertexId, List<HexCoord>> _vertexTiles = new Dictionary<VertexId, List<HexCoord>>();
        private readonly Dictionary<VertexId, List<EdgeId>> _vertexEdges = new Dictionary<VertexId, List<EdgeId>>();
        private readonly Dictionary<EdgeId, List<HexCoord>> _edgeTiles = new Dictionary<EdgeId, List<HexCoord>>();

        public HexMap Map { get; }
        public IEnumerable<VertexId> Vertices => _vertexTiles.Keys;
        public IEnumerable<EdgeId> Edges => _edgeTiles.Keys;
        public int VertexCount => _vertexTiles.Count;
        public int EdgeCount => _edgeTiles.Count;

        public HexTopology(HexMap map)
        {
            Map = map;

            foreach (HexTile tile in map.Tiles)
            {
                for (int i = 0; i < 6; i++)
                {
                    VertexId vertex = VertexId.Of(tile.Coord, (HexCorner)i);
                    GetOrAdd(_vertexTiles, vertex).Add(tile.Coord);

                    EdgeId edge = EdgeId.Of(tile.Coord, (HexDirection)i);
                    List<HexCoord> edgeTiles = GetOrAdd(_edgeTiles, edge);
                    if (edgeTiles.Count == 0)
                    {
                        // 처음 만난 변일 때만 꼭짓점-변 연결을 기록한다(중복 방지).
                        GetOrAdd(_vertexEdges, edge.VertexA).Add(edge);
                        GetOrAdd(_vertexEdges, edge.VertexB).Add(edge);
                    }
                    edgeTiles.Add(tile.Coord);
                }
            }
        }

        public bool Contains(VertexId vertex) => _vertexTiles.ContainsKey(vertex);
        public bool Contains(EdgeId edge) => _edgeTiles.ContainsKey(edge);

        /// <summary>꼭짓점에 닿은 맵 안의 타일(1~3개).</summary>
        public IReadOnlyList<HexCoord> TilesOf(VertexId vertex) =>
            _vertexTiles.TryGetValue(vertex, out List<HexCoord> tiles) ? tiles : NoTiles;

        /// <summary>변 양쪽에 있는 맵 안의 타일(경계 변은 1개, 내부 변은 2개).</summary>
        public IReadOnlyList<HexCoord> TilesOf(EdgeId edge) =>
            _edgeTiles.TryGetValue(edge, out List<HexCoord> tiles) ? tiles : NoTiles;

        /// <summary>꼭짓점에서 뻗어 나가는 맵 안의 변(2~3개).</summary>
        public IReadOnlyList<EdgeId> EdgesOf(VertexId vertex) =>
            _vertexEdges.TryGetValue(vertex, out List<EdgeId> edges) ? edges : NoEdges;

        /// <summary>타일의 여섯 변(East부터 시계 방향).</summary>
        public static IEnumerable<EdgeId> EdgesOf(HexCoord tile)
        {
            for (int i = 0; i < 6; i++) yield return EdgeId.Of(tile, (HexDirection)i);
        }

        /// <summary>타일의 여섯 꼭짓점(North부터 시계 방향).</summary>
        public static IEnumerable<VertexId> VerticesOf(HexCoord tile)
        {
            for (int i = 0; i < 6; i++) yield return VertexId.Of(tile, (HexCorner)i);
        }

        /// <summary>경계 변(맵 안쪽 타일이 하나뿐인 변)인지.</summary>
        public bool IsBoundary(EdgeId edge) => TilesOf(edge).Count == 1;

        /// <summary>꼭짓점의 월드 위치.</summary>
        public static HexPoint PositionOf(VertexId vertex, HexLayout layout) =>
            layout.CornerPosition(vertex.Owner, vertex.OwnerCorner);

        /// <summary>변 중간 지점의 월드 위치.</summary>
        public static HexPoint MidpointOf(EdgeId edge, HexLayout layout)
        {
            HexPoint a = PositionOf(edge.VertexA, layout);
            HexPoint b = PositionOf(edge.VertexB, layout);
            return new HexPoint((a.X + b.X) / 2.0, (a.Z + b.Z) / 2.0);
        }

        private static List<TValue> GetOrAdd<TKey, TValue>(Dictionary<TKey, List<TValue>> dict, TKey key)
        {
            if (!dict.TryGetValue(key, out List<TValue> list))
            {
                list = new List<TValue>();
                dict.Add(key, list);
            }
            return list;
        }
    }
}
