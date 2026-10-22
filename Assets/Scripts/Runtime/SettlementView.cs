using System;
using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;
using DesertSettlement.Core.Water;
using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 게임 화면에 정착지를 기본 도형으로 그린다(임시 그래픽).
    /// - 타일: 지형 색의 납작한 육각형
    /// - 시설: 종류별 색의 도형. 높이 = 레벨, 공사 중이면 진행률만큼 자라고 회색, 폐쇄는 어둡게
    /// - 수로: 변 위의 막대. 물이 많이 흐를수록 진한 파랑(부하/용량), 공사 중은 회색
    /// - 수문: 변 중간의 상자. 열림 초록 / 닫힘 빨강
    /// - 대기 중 건설 예약: 바닥의 얇은 표시
    /// - 머리 위 글자: 공사 진행률, 물 부족 이유(설정에서 끌 수 있음)
    /// 색·크기는 인스펙터에서 바꾼다.
    /// </summary>
    [RequireComponent(typeof(GameSession))]
    public sealed class SettlementView : MonoBehaviour
    {
        [Header("지형 색")]
        [SerializeField] private Color desert = new Color(0.86f, 0.74f, 0.52f);
        [SerializeField] private Color waterSource = new Color(0.35f, 0.65f, 0.85f);
        [SerializeField] private Color oreVein = new Color(0.55f, 0.4f, 0.45f);
        [SerializeField] private Color bedrock = new Color(0.58f, 0.56f, 0.52f);
        [SerializeField] private Color obstacle = new Color(0.3f, 0.26f, 0.22f);
        [Range(0.8f, 1f)]
        [SerializeField] private float tileGap = 0.96f;

        [Header("시설 색")]
        [SerializeField] private Color well = new Color(0.2f, 0.45f, 0.95f);
        [SerializeField] private Color farm = new Color(0.35f, 0.75f, 0.3f);
        [SerializeField] private Color quarry = new Color(0.75f, 0.72f, 0.68f);
        [SerializeField] private Color mine = new Color(0.45f, 0.3f, 0.25f);
        [SerializeField] private Color auctionHall = new Color(0.95f, 0.75f, 0.2f);
        [SerializeField] private Color warehouse = new Color(0.7f, 0.5f, 0.3f);
        [SerializeField] private Color constructing = new Color(0.6f, 0.6f, 0.6f);
        [SerializeField] private Color demolishing = new Color(0.85f, 0.35f, 0.3f);
        [Range(0f, 1f)]
        [SerializeField] private float closedDarken = 0.45f;

        [Header("수로")]
        [SerializeField] private Color canalDry = new Color(0.55f, 0.5f, 0.42f);
        [SerializeField] private Color canalFull = new Color(0.1f, 0.35f, 0.95f);
        [SerializeField] private Color gateOpen = new Color(0.2f, 0.85f, 0.3f);
        [SerializeField] private Color gateClosed = new Color(0.9f, 0.15f, 0.15f);
        [SerializeField] private Color planned = new Color(1f, 1f, 1f);
        [Tooltip("타일 크기 대비 수로 폭(L1). 레벨마다 50%씩 넓어진다")]
        [SerializeField] private float canalWidth = 0.08f;

        [Header("글자")]
        [SerializeField] private bool showLabels = true;

        private GameSession _session;
        private Transform _tilesRoot, _facilitiesRoot, _plannedRoot;
        private readonly Dictionary<int, GameObject> _facilityObjects = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> _plannedObjects = new Dictionary<int, GameObject>();
        private Camera _camera;

        private void Awake()
        {
            _session = GetComponent<GameSession>();
            _tilesRoot = NewRoot("Tiles");
            _facilitiesRoot = NewRoot("Facilities");
            _plannedRoot = NewRoot("Planned");
        }

        private void OnEnable()
        {
            _session.Initialized += RebuildAll;
            if (_session.IsReady) RebuildAll();
        }

        private void OnDisable()
        {
            if (_session != null) _session.Initialized -= RebuildAll;
        }

        private Transform NewRoot(string name)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            return root;
        }

        private float Size => _session.TileSize;

        public Color TerrainColor(TerrainType t)
        {
            switch (t)
            {
                case TerrainType.WaterSource: return waterSource;
                case TerrainType.OreVein: return oreVein;
                case TerrainType.Bedrock: return bedrock;
                case TerrainType.Obstacle: return obstacle;
                default: return desert;
            }
        }

        private void RebuildAll()
        {
            foreach (Transform root in new[] { _tilesRoot, _facilitiesRoot, _plannedRoot })
            {
                for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);
            }
            _facilityObjects.Clear();
            _plannedObjects.Clear();

            foreach (HexTile tile in _session.Map.Tiles)
            {
                GameObject go = WorldVisuals.HexTile($"Tile {tile.Coord}", _tilesRoot, TerrainColor(tile.Terrain));
                go.transform.position = _session.ToWorld(_session.Layout.TileCenter(tile.Coord));
                go.transform.localScale = new Vector3(Size * tileGap, 1f, Size * tileGap);
            }
        }

        private void LateUpdate()
        {
            if (!_session.IsReady) return;
            Settlement s = _session.Settlement;

            // 시설 생성·삭제 동기화.
            var alive = new HashSet<int>();
            foreach (Facility f in s.Facilities)
            {
                alive.Add(f.Id);
                if (!_facilityObjects.TryGetValue(f.Id, out GameObject go))
                {
                    go = CreateFacilityObject(f);
                    _facilityObjects.Add(f.Id, go);
                }
                UpdateFacilityObject(s, f, go);
            }
            foreach (int id in _facilityObjects.Keys.Where(id => !alive.Contains(id)).ToList())
            {
                Destroy(_facilityObjects[id]);
                _facilityObjects.Remove(id);
            }

            // 대기 중인 건설 예약 표시.
            var queued = new HashSet<int>();
            foreach (ConstructionJob job in s.Jobs.Where(j => j.Type == JobType.Build && j.State == JobState.Queued))
            {
                queued.Add(job.Id);
                if (_plannedObjects.ContainsKey(job.Id)) continue;
                GameObject marker = job.Edge.HasValue
                    ? CanalBar(job.Edge.Value, _plannedRoot, planned, 0.02f, 0.5f)
                    : WorldVisuals.HexTile("Planned " + job.Kind, _plannedRoot, planned);
                if (!job.Edge.HasValue)
                {
                    marker.transform.position = _session.ToWorld(_session.Layout.TileCenter(job.Tile), 0.01f);
                    marker.transform.localScale = new Vector3(Size * 0.45f, 1f, Size * 0.45f);
                }
                _plannedObjects.Add(job.Id, marker);
            }
            foreach (int id in _plannedObjects.Keys.Where(id => !queued.Contains(id)).ToList())
            {
                Destroy(_plannedObjects[id]);
                _plannedObjects.Remove(id);
            }
        }

        private GameObject CreateFacilityObject(Facility f)
        {
            switch (f.Kind)
            {
                case BuildingKinds.CanalEdge:
                    return CanalBar(f.Edge.Value, _facilitiesRoot, canalDry, 0.04f, 1f);
                case BuildingKinds.CanalGate:
                    GameObject gate = WorldVisuals.Primitive(PrimitiveType.Cube, "Gate", _facilitiesRoot, gateOpen);
                    gate.transform.position = _session.ToWorld(HexTopology.MidpointOf(f.Edge.Value, _session.Layout), 0.08f);
                    gate.transform.localScale = Vector3.one * Size * 0.16f;
                    return gate;
                case BuildingKinds.Well:
                    return WorldVisuals.Primitive(PrimitiveType.Cylinder, "Well", _facilitiesRoot, well);
                default:
                    return WorldVisuals.Primitive(PrimitiveType.Cube, f.Kind, _facilitiesRoot, KindColor(f.Kind));
            }
        }

        private GameObject CanalBar(EdgeId edge, Transform parent, Color color, float height, float lengthRatio)
        {
            HexLayout layout = _session.Layout;
            Vector3 a = _session.ToWorld(HexTopology.PositionOf(edge.VertexA, layout));
            Vector3 b = _session.ToWorld(HexTopology.PositionOf(edge.VertexB, layout));
            GameObject bar = WorldVisuals.Primitive(PrimitiveType.Cube, "Canal " + edge, parent, color);
            bar.transform.position = (a + b) * 0.5f + Vector3.up * height * 0.5f;
            bar.transform.rotation = Quaternion.LookRotation(b - a, Vector3.up);
            bar.transform.localScale = new Vector3(Size * canalWidth, height, Vector3.Distance(a, b) * lengthRatio);
            return bar;
        }

        private void UpdateFacilityObject(Settlement s, Facility f, GameObject go)
        {
            float progress = JobProgress(s, f);

            if (f.Kind == BuildingKinds.CanalEdge)
            {
                Color c = canalDry;
                if (f.State == FacilityState.Operating && s.LastWater != null &&
                    s.LastWater.EdgeLoads.TryGetValue(f.Edge.Value, out double load))
                {
                    c = Color.Lerp(canalDry, canalFull, Mathf.Clamp01((float)(load / Math.Max(1e-6, CanalCapacity(s, f)))));
                }
                c = Tint(c, f.State, progress);
                WorldVisuals.SetColor(go, c);
                Vector3 scale = go.transform.localScale;
                scale.x = Size * canalWidth * (1f + 0.5f * (f.Level - 1));
                go.transform.localScale = scale;
                return;
            }

            if (f.Kind == BuildingKinds.CanalGate)
            {
                WorldVisuals.SetColor(go, Tint(f.GateOpen ? gateOpen : gateClosed, f.State, progress));
                return;
            }

            float baseHeight = f.Kind == BuildingKinds.AuctionHall ? 0.7f : f.Kind == BuildingKinds.Farm ? 0.15f : 0.4f;
            float height = Size * baseHeight * (1f + 0.35f * (f.Level - 1));
            if (f.State == FacilityState.Constructing) height *= Mathf.Max(0.1f, progress);
            float footprint = Size * (f.Kind == BuildingKinds.Farm ? 1.1f : 0.6f);
            go.transform.localScale = f.Kind == BuildingKinds.Well
                ? new Vector3(Size * 0.5f, height * 0.5f, Size * 0.5f)            // 원기둥은 높이 2가 기본
                : new Vector3(footprint, height, footprint);
            go.transform.position = _session.ToWorld(_session.Layout.TileCenter(f.Tile), height * 0.5f);
            WorldVisuals.SetColor(go, Tint(KindColor(f.Kind), f.State, progress));
        }

        private static double CanalCapacity(Settlement s, Facility f) =>
            s.Rules.Config.GetLevel(BuildingKinds.CanalEdge, f.Level)?.CapacityPerHour ?? 100.0;

        private Color Tint(Color c, FacilityState state, float progress)
        {
            switch (state)
            {
                case FacilityState.Constructing: return Color.Lerp(constructing, c, progress * 0.6f);
                case FacilityState.Closed: return Color.Lerp(c, Color.black, closedDarken);
                case FacilityState.Upgrading: return Color.Lerp(c, Color.white, 0.35f + 0.25f * Mathf.Sin(Time.time * 4f));
                case FacilityState.Demolishing: return Color.Lerp(c, demolishing, 0.7f);
                default: return c;
            }
        }

        private Color KindColor(string kind)
        {
            switch (kind)
            {
                case BuildingKinds.Well: return well;
                case BuildingKinds.Farm: return farm;
                case BuildingKinds.Quarry: return quarry;
                case BuildingKinds.Mine: return mine;
                case BuildingKinds.AuctionHall: return auctionHall;
                case BuildingKinds.Warehouse: return warehouse;
                default: return Color.magenta;
            }
        }

        /// <summary>진행 중 작업의 진행률(0~1). 시계의 시간 안 소수점까지 반영해 부드럽게 움직인다.</summary>
        public float JobProgress(Settlement s, Facility f)
        {
            ConstructionJob job = s.Jobs.FirstOrDefault(j => j.FacilityId == f.Id && j.State == JobState.InProgress);
            if (job == null || job.Hours <= 0) return 1f;
            // StartedHour 틱은 시계가 StartedHour+1 시간에 닿을 때 처리된다.
            double elapsed = _session.Clock.ElapsedHours - (job.StartedHour + 1);
            return Mathf.Clamp01((float)(elapsed / job.Hours));
        }

        private void OnGUI()
        {
            if (!showLabels || !_session.IsReady) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;
            Settlement s = _session.Settlement;

            foreach (Facility f in s.Facilities)
            {
                if (f.Edge.HasValue) continue;
                string text = null;
                if (f.State == FacilityState.Constructing) text = $"건설 {JobProgress(s, f):P0}";
                else if (f.State == FacilityState.Upgrading) text = $"개량 {JobProgress(s, f):P0}";
                else if (f.State == FacilityState.Demolishing) text = $"철거 {JobProgress(s, f):P0}";
                else if (f.State == FacilityState.Closed) text = "폐쇄";
                else if (f.IsOperating)
                {
                    // 농장 자기 수지 적자와 물 부족은 원인이 다르므로 따로 적는다.
                    var parts = new List<string>();
                    FacilityOutput o = s.OutputOf(f);
                    if (o.FarmBelowOwnUpkeep) parts.Add($"식량 생산 {o.ProducedPerHour:0.0} < 소비 {o.FoodUsePerHour:0.0}");
                    if (s.LastWater != null && s.LastWater.Consumers.TryGetValue(f.Id.ToString(), out WaterConsumerResult w) && w.Rate < 0.999)
                    {
                        parts.Add($"물 {w.Rate:P0} · {FacilityInfo.ShortageText(w.Shortage)}");
                    }
                    if (parts.Count > 0) text = string.Join("\n", parts);
                }
                if (text == null) continue;

                Vector3 screen = _camera.WorldToScreenPoint(_session.ToWorld(_session.Layout.TileCenter(f.Tile), Size * 0.9f));
                if (screen.z < 0) continue;
                int lineCount = text.Split('\n').Length;
                var rect = new Rect(screen.x - 90, Screen.height - screen.y - 11 * lineCount, 180, 20 * lineCount + 2);
                GUI.Label(rect, text, LabelStyle);
            }
        }

        private static GUIStyle _labelStyle;
        private static GUIStyle LabelStyle
        {
            get
            {
                if (_labelStyle != null) return _labelStyle;
                _labelStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 12 };
                return _labelStyle;
            }
        }
    }
}
