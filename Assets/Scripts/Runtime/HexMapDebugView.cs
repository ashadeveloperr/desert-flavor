using DesertSettlement.Core.Canal;
using DesertSettlement.Core.Hex;
using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 씬 뷰에 헥스 타일·꼭짓점·수로를 Gizmo로 그리는 확인용 뷰. 게임 화면(Game 뷰)에는 보이지 않는다
    /// (Game 뷰 상단의 Gizmos 버튼을 켜면 보인다). 실제 그래픽이 아니므로 나중에 지워도 된다.
    /// 플레이 전에는 GameSession의 반경·크기로 빈 맵을 그리고, 플레이 중에는 실제 수로망을 그린다.
    /// </summary>
    [RequireComponent(typeof(GameSession))]
    public sealed class HexMapDebugView : MonoBehaviour
    {
        [SerializeField] private bool drawTiles = true;
        [SerializeField] private bool drawVertices = false;
        [SerializeField] private bool drawCanals = true;
        [SerializeField] private bool drawLabels = false;

        [SerializeField] private Color tileColor = new Color(0.85f, 0.7f, 0.45f, 0.8f);
        [SerializeField] private Color boundaryColor = new Color(1f, 0.45f, 0.2f, 1f);
        [SerializeField] private Color vertexColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color canalColor = new Color(0.2f, 0.6f, 1f, 1f);
        [SerializeField] private Color closedGateColor = new Color(0.9f, 0.15f, 0.15f, 1f);
        [SerializeField] private Color openGateColor = new Color(0.2f, 0.85f, 0.3f, 1f);

        private GameSession _session;
        private HexTopology _previewTopology;
        private int _previewRadius = -1;

        private void OnDrawGizmos()
        {
            if (_session == null) _session = GetComponent<GameSession>();
            if (_session == null) return;

            HexTopology topology;
            HexLayout layout;
            CanalGraph canals = null;
            if (Application.isPlaying && _session.IsReady)
            {
                topology = _session.Topology;
                layout = _session.Layout;
                canals = _session.Canals;
            }
            else
            {
                if (_previewRadius != _session.MapRadius)
                {
                    _previewTopology = new HexTopology(new HexMap(_session.MapRadius));
                    _previewRadius = _session.MapRadius;
                }
                topology = _previewTopology;
                layout = new HexLayout(_session.TileSize);
            }

            float size = (float)layout.Size;

            if (drawTiles)
            {
                foreach (EdgeId edge in topology.Edges)
                {
                    Gizmos.color = topology.IsBoundary(edge) ? boundaryColor : tileColor;
                    Gizmos.DrawLine(VertexWorld(edge.VertexA, layout), VertexWorld(edge.VertexB, layout));
                }
            }

            if (drawVertices)
            {
                Gizmos.color = vertexColor;
                foreach (VertexId vertex in topology.Vertices)
                {
                    Gizmos.DrawSphere(VertexWorld(vertex, layout), size * 0.05f);
                }
            }

            if (drawCanals && canals != null)
            {
                foreach (CanalSegment segment in canals.Segments)
                {
                    Vector3 a = VertexWorld(segment.Edge.VertexA, layout);
                    Vector3 b = VertexWorld(segment.Edge.VertexB, layout);
                    DrawThickLine(a, b, size * 0.06f, canalColor);

                    if (segment.HasGate)
                    {
                        Gizmos.color = segment.GateOpen ? openGateColor : closedGateColor;
                        Gizmos.DrawCube((a + b) * 0.5f, Vector3.one * size * 0.18f);
                    }
                }
            }

#if UNITY_EDITOR
            if (drawLabels)
            {
                foreach (HexTile tile in topology.Map.Tiles)
                {
                    UnityEditor.Handles.Label(_session.ToWorld(layout.TileCenter(tile.Coord)), tile.Coord.ToString());
                }
            }
#endif
        }

        private Vector3 VertexWorld(VertexId vertex, HexLayout layout) =>
            _session.ToWorld(HexTopology.PositionOf(vertex, layout));

        // Gizmos 선은 1픽셀이라, 수로는 나란한 선 여러 개로 굵게 그린다.
        private static void DrawThickLine(Vector3 a, Vector3 b, float width, Color color)
        {
            Gizmos.color = color;
            Vector3 side = Vector3.Cross((b - a).normalized, Vector3.up) * width;
            for (int i = -2; i <= 2; i++)
            {
                Vector3 offset = side * (i / 2f) + Vector3.up * 0.01f;
                Gizmos.DrawLine(a + offset, b + offset);
            }
        }
    }
}
