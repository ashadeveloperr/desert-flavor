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
    /// - 타일: 지형 색의 납작한 육각형. 물이 흐르는 수로 옆 사막은 시간이 지나며 푸르게 바뀐다(SG4)
    /// - 시설: 기본 도형을 조합한 종류별 모양(<see cref="FacilityModels"/>). 레벨마다 부품이 붙고 조금 높아짐,
    ///   공사 중이면 비계 안에서 진행률만큼 자라고 회색, 폐쇄는 어둡게. 농장 작물은 완공 뒤 자라고 물이 모자라면 시든다(SG1~SG3)
    /// - 수로: 변 위의 막대. 물이 많이 흐를수록 진한 파랑(부하/용량)이고 반짝임이 흘러간다, 공사 중은 회색
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
        [SerializeField] private Color hospital = new Color(0.95f, 0.95f, 0.95f);
        [SerializeField] private Color waterTank = new Color(0.45f, 0.75f, 0.95f);
        [SerializeField] private Color warehouse = new Color(0.7f, 0.5f, 0.3f);
        [SerializeField] private Color constructing = new Color(0.6f, 0.6f, 0.6f);
        [SerializeField] private Color demolishing = new Color(0.85f, 0.35f, 0.3f);
        [Range(0f, 1f)]
        [SerializeField] private float closedDarken = 0.45f;

        [Header("수로")]
        [SerializeField] private Color canalDry = new Color(0.55f, 0.5f, 0.42f);
        [SerializeField] private Color canalFull = new Color(0.1f, 0.35f, 0.95f);
        [Tooltip("닫힌 수로 색(2026-10-23 수문 폐지 뒤 수로 폐쇄가 물길 차단)")]
        [SerializeField] private Color canalClosed = new Color(0.9f, 0.15f, 0.15f);
        [SerializeField] private Color planned = new Color(1f, 1f, 1f);
        [Tooltip("타일 크기 대비 수로 폭(L1). 레벨마다 50%씩 넓어진다")]
        [SerializeField] private float canalWidth = 0.08f;
        [Tooltip("물이 흐르는 수로의 반짝임 세기(0이면 끔)")]
        [Range(0f, 0.5f)]
        [SerializeField] private float canalShimmer = 0.18f;

        [Header("정착지가 자라는 모습")]
        [Tooltip("물이 흐르는 수로 옆 사막이 바뀌어 가는 색")]
        [SerializeField] private Color oasis = new Color(0.6f, 0.72f, 0.4f);
        [Tooltip("정산 1시간마다 목표 색에 다가가는 비율(작을수록 천천히 푸르러짐)")]
        [Range(0.01f, 1f)]
        [SerializeField] private float greeningRate = 0.08f;
        [Tooltip("물이 모자란 작물 색")]
        [SerializeField] private Color witheredCrop = new Color(0.72f, 0.62f, 0.32f);

        [Header("글자")]
        [SerializeField] private bool showLabels = true;

        private GameSession _session;
        private Transform _tilesRoot, _facilitiesRoot, _plannedRoot;
        private readonly Dictionary<int, GameObject> _facilityObjects = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> _plannedObjects = new Dictionary<int, GameObject>();
        private readonly Dictionary<HexCoord, GameObject> _tileObjects = new Dictionary<HexCoord, GameObject>();
        private readonly Dictionary<HexCoord, float> _green = new Dictionary<HexCoord, float>();
        private long _greenHour = -1;
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
            _tileObjects.Clear();
            _green.Clear();
            _greenHour = -1;

            foreach (HexTile tile in _session.Map.Tiles)
            {
                GameObject go = WorldVisuals.HexTile($"Tile {tile.Coord}", _tilesRoot, TerrainColor(tile.Terrain));
                go.transform.position = _session.ToWorld(_session.Layout.TileCenter(tile.Coord));
                go.transform.localScale = new Vector3(Size * tileGap, 1f, Size * tileGap);
                _tileObjects[tile.Coord] = go;
            }
        }

        /// <summary>
        /// 정산 1시간마다: 물이 흐르는 수로(변)에 닿은 사막 칸의 '푸름'이 목표(그 수로의 부하율)로 조금씩 다가간다(SG4).
        /// 물이 끊기면 다시 천천히 모래색으로 돌아간다.
        /// </summary>
        private void UpdateGreening(Settlement s)
        {
            if (s.CurrentHour == _greenHour || s.LastWater == null) return;
            long steps = _greenHour < 0 ? 1 : Math.Min(24, s.CurrentHour - _greenHour);
            _greenHour = s.CurrentHour;

            var wet = new Dictionary<EdgeId, float>();
            foreach (Facility f in s.Facilities)
            {
                if (f.Kind != BuildingKinds.CanalEdge || f.State != FacilityState.Operating) continue;
                if (!s.LastWater.EdgeLoads.TryGetValue(f.Edge.Value, out double load) || load <= 1e-6) continue;
                wet[f.Edge.Value] = Mathf.Clamp01((float)(load / Math.Max(1e-6, CanalCapacity(s, f))));
            }

            foreach (HexTile tile in _session.Map.Tiles)
            {
                if (tile.Terrain != TerrainType.Desert || !_tileObjects.TryGetValue(tile.Coord, out GameObject go)) continue;
                float target = 0f;
                foreach (EdgeId e in HexTopology.EdgesOf(tile.Coord))
                {
                    if (wet.TryGetValue(e, out float fill)) target = Mathf.Max(target, 0.45f + 0.55f * fill);
                }
                _green.TryGetValue(tile.Coord, out float g);
                for (long i = 0; i < steps; i++) g += (target - g) * greeningRate;
                if (Mathf.Abs(g - target) < 0.01f) g = target;
                _green[tile.Coord] = g;
                // 색은 단계로 끊어 머티리얼 수를 줄인다(20단계).
                WorldVisuals.SetColor(go, Color.Lerp(desert, oasis, Mathf.Round(g * 20f) / 20f));
            }
        }

        private void LateUpdate()
        {
            if (!_session.IsReady) return;
            Settlement s = _session.Settlement;
            UpdateGreening(s);

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
                default:
                    return FacilityModels.Build(f.Kind, _facilitiesRoot, KindColor(f.Kind)).gameObject;
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
                    s.LastWater.EdgeLoads.TryGetValue(f.Edge.Value, out double load) && load > 1e-6)
                {
                    float fill = Mathf.Clamp01((float)(load / Math.Max(1e-6, CanalCapacity(s, f))));
                    c = Color.Lerp(canalDry, canalFull, fill);
                    // 반짝임 띠가 맵을 가로질러 흘러간다(흐름 방향이 아니라 '물이 있다'는 표시, M4).
                    Vector3 p = go.transform.position / Mathf.Max(0.01f, Size);
                    float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f - (p.x + p.z) * 1.7f);
                    c = Color.Lerp(c, Color.white, canalShimmer * wave * (0.4f + 0.6f * fill));
                }
                c = f.State == FacilityState.Closed ? canalClosed : Tint(c, f.State, progress);
                WorldVisuals.SetColor(go, c);
                Vector3 scale = go.transform.localScale;
                scale.x = Size * canalWidth * (1f + 0.5f * (f.Level - 1));
                go.transform.localScale = scale;
                return;
            }

            // 모양은 타일 반지름 1 기준. 레벨마다 부품이 붙고 15%씩 높아지며(SG2), 공사 중이면 비계 안에서 진행률만큼 땅에서 솟는다(SG1).
            FacilityModel model = go.GetComponent<FacilityModel>();
            float height = 1f + 0.15f * (f.Level - 1);
            if (f.State == FacilityState.Constructing) height *= Mathf.Max(0.1f, progress);
            go.transform.localScale = new Vector3(Size, Size, Size);
            go.transform.position = _session.ToWorld(_session.Layout.TileCenter(f.Tile));
            model.Body.localScale = new Vector3(1f, height, 1f);
            model.ApplyLevel(f.Level);
            model.ShowScaffold(f.State == FacilityState.Constructing || f.State == FacilityState.Upgrading);

            Func<Color, Color> crop = null;
            if (f.Kind == BuildingKinds.Farm)
            {
                // 작물(SG3): 완공 뒤 가동 숙성 시간 동안 자라고, 물을 덜 받으면 낮아지고 누렇게 시든다.
                float grown = f.CompletedHour < 0 ? 0f
                    : Mathf.Clamp01((float)((s.CurrentHour - f.CompletedHour) / Math.Max(1.0, s.Rules.MaturityFullHours)));
                float water = 1f;
                if (f.IsOperating && s.LastWater != null && s.LastWater.Consumers.TryGetValue(f.Id.ToString(), out WaterConsumerResult w))
                {
                    water = Mathf.Clamp01((float)w.Rate);
                }
                model.SetGrowth((0.25f + 0.75f * grown) * (0.55f + 0.45f * water));
                float dry = Mathf.Round((1f - water) * 10f) / 10f;
                crop = c => Color.Lerp(c, witheredCrop, dry);
            }
            model.Paint(c => Tint(c, f.State, progress), crop);
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
                case BuildingKinds.Hospital: return hospital;
                case BuildingKinds.WaterTank: return waterTank;
                default: return Color.magenta;
            }
        }

        /// <summary>진행 중 작업의 진행률(0~1). 시계의 시간 안 소수점까지 반영해 부드럽게 움직인다.</summary>
        public float JobProgress(Settlement s, Facility f)
        {
            ConstructionJob job = s.Jobs.FirstOrDefault(j => j.FacilityId == f.Id && j.State == JobState.InProgress);
            if (job == null || job.Hours <= 0) return 1f;
            // 정산된 시간(CurrentHour) + 지금 시간 안의 진행분. StartedHour 틱이 끝난 시점이 진행률 0.
            double fraction = _session.Clock.ElapsedHours - Math.Floor(_session.Clock.ElapsedHours);
            double elapsed = s.CurrentHour + fraction - (job.StartedHour + 1);
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
