using System.Collections.Generic;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;
using DesertSettlement.Core.Water;
using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 선택한 시설의 물길을 맵에 그린다(2026-10-23, Docs/decision_log_auto.md V1~V3).
    /// - 물을 쓰는 시설: 실제로 물이 온 수로를 하늘색 띠로, 수로 용량 때문에 덜 받았다면 막힌 수로를 빨간 띠로.
    /// - 우물·물탱크: 그 물이 간 모든 수로를 하늘색 띠로.
    /// 선택이 바뀌거나 한 시간이 지날 때만 다시 그린다.
    /// </summary>
    [RequireComponent(typeof(GameSession))]
    public sealed class WaterPathOverlay : MonoBehaviour
    {
        [SerializeField] private Color routeColor = new Color(0.35f, 0.95f, 1f);
        [SerializeField] private Color blockedColor = new Color(1f, 0.2f, 0.2f);
        [Tooltip("띠 두께(타일 크기 대비)")]
        [SerializeField] private float thickness = 0.05f;

        private GameSession _session;
        private BuildInputController _input;
        private Transform _root;
        private readonly List<GameObject> _bars = new List<GameObject>();
        private int _drawnFor = int.MinValue;
        private long _drawnAt = long.MinValue;

        private void Awake()
        {
            _session = GetComponent<GameSession>();
            _input = GetComponent<BuildInputController>();
            _root = new GameObject("Water Path Overlay").transform;
            _root.SetParent(transform, false);
        }

        private void Update()
        {
            Settlement s = _session.IsReady ? _session.Settlement : null;
            int id = s != null && _input != null ? _input.SelectedFacilityId : -1;
            long hour = s?.CurrentHour ?? -1;
            if (id == _drawnFor && hour == _drawnAt) return;
            _drawnFor = id;
            _drawnAt = hour;

            int used = 0;
            Facility f = s?.GetFacility(id);
            if (f != null && !f.Edge.HasValue)
            {
                WaterAllocation water = s.LastWater;
                string key = WaterIds.Of(f);
                IEnumerable<WaterIntakeFlow> flows = f.Kind == BuildingKinds.Well ? WaterRoutes.SentBy(water, key)
                    : f.Kind == BuildingKinds.WaterTank ? WaterRoutes.SentBy(water, WaterIds.Tank(f.Id))
                    : WaterRoutes.ReceivedBy(water, key);
                EdgeId? blocked = WaterRoutes.BlockingEdgeOf(water, key);
                foreach (EdgeId e in WaterRoutes.EdgesOf(flows))
                    if (e != blocked) Place(used++, e, routeColor, 1f);
                if (blocked.HasValue) Place(used++, blocked.Value, blockedColor, 1.6f);
            }
            for (int i = used; i < _bars.Count; i++) _bars[i].SetActive(false);
        }

        private void Place(int index, EdgeId edge, Color color, float widthScale)
        {
            if (index >= _bars.Count) _bars.Add(WorldVisuals.Primitive(PrimitiveType.Cube, "Water Path", _root, color));
            GameObject bar = _bars[index];
            bar.SetActive(true);
            WorldVisuals.SetColor(bar, color);
            Vector3 a = _session.ToWorld(HexTopology.PositionOf(edge.VertexA, _session.Layout));
            Vector3 b = _session.ToWorld(HexTopology.PositionOf(edge.VertexB, _session.Layout));
            float size = _session.TileSize;
            bar.transform.position = (a + b) * 0.5f + Vector3.up * size * 0.1f;
            bar.transform.rotation = Quaternion.LookRotation(b - a, Vector3.up);
            bar.transform.localScale = new Vector3(size * thickness * widthScale, size * 0.02f, Vector3.Distance(a, b) * 0.9f);
        }
    }
}
