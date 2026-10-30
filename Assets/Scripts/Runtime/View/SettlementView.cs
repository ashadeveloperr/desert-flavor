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
    /// - 닫힌 수로: 빨강(수로 닫기가 물길 차단)
    /// - 대기 중 건설 예약: 바닥의 얇은 표시
    /// - 등불: SettlementView.Lanterns.cs, 머리 위 글자(공사 진행률·물 부족 이유): SettlementView.Labels.cs
    /// 색·크기는 인스펙터에서 바꾼다.
    /// </summary>
    /// <summary>인스펙터에서 시설 색을 덮어쓰는 한 줄(종류 이름 = 설정 JSON buildingLevels 키).</summary>
    [System.Serializable]
    public sealed class KindColorOverride
    {
        public string kind;
        public Color color = Color.white;
    }

    [RequireComponent(typeof(GameSession))]
    public sealed partial class SettlementView : MonoBehaviour
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
        [Tooltip("시설 기본 색은 시설 목록(Core/Economy/FacilityCatalog.cs)에 있다. 여기 넣은 종류만 덮어쓴다(2026-10-24 CL3)")]
        [SerializeField] private List<KindColorOverride> facilityColors = new List<KindColorOverride>();
        [SerializeField] private Color constructing = new Color(0.6f, 0.6f, 0.6f);
        [SerializeField] private Color demolishing = new Color(0.85f, 0.35f, 0.3f);
        [Range(0f, 1f)]
        [SerializeField] private float closedDarken = 0.45f;

        [Header("수로")]
        [Tooltip("물이 없는 수로 색. 2026-10-23 검토 UX6: 사막과 구분되게 더 어둡게(예전 필드 canalDry를 대신함)")]
        [SerializeField] private Color canalDryColor = new Color(0.38f, 0.31f, 0.23f);
        [SerializeField] private Color canalFull = new Color(0.1f, 0.35f, 0.95f);
        [Tooltip("닫힌 수로 색(2026-10-23 수문 폐지 뒤 수로 폐쇄가 물길 차단)")]
        [SerializeField] private Color canalClosed = new Color(0.9f, 0.15f, 0.15f);
        [SerializeField] private Color planned = new Color(1f, 1f, 1f);
        [Tooltip("타일 크기 대비 수로 폭(L1). 레벨마다 50%씩 넓어진다. 2026-10-23 검토 UX6: 0.08 → 0.14(예전 필드 canalWidth를 대신함)")]
        [SerializeField] private float canalThickness = 0.14f;
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

        private GameSession _session;
        private Transform _tilesRoot, _facilitiesRoot, _plannedRoot;
        private readonly Dictionary<int, GameObject> _facilityObjects = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> _plannedObjects = new Dictionary<int, GameObject>();
        private readonly Dictionary<HexCoord, GameObject> _tileObjects = new Dictionary<HexCoord, GameObject>();
        private readonly Dictionary<HexCoord, float> _green = new Dictionary<HexCoord, float>();
        private long _greenHour = -1;

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
                double load = ShownLoad(s.LastWater, f.Edge.Value);
                if (load <= 1e-6) continue;
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
            UpdateLanternMaterials();

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
                    return CanalBar(f.Edge.Value, _facilitiesRoot, canalDryColor, 0.06f, 1f);
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
            bar.transform.localScale = new Vector3(Size * canalThickness, height, Vector3.Distance(a, b) * lengthRatio);
            return bar;
        }

        /// <summary>수로 색에 쓰는 흐름 = 변을 지난 물 + 이 수로가 이어 준 우물 옆 직접 취수(WD1, 용량은 안 씀).</summary>
        private static double ShownLoad(WaterAllocation water, EdgeId edge) =>
            (water.EdgeLoads.TryGetValue(edge, out double load) ? load : 0) + (water.DirectIntakeLoads.TryGetValue(edge, out double direct) ? direct : 0);

        private void UpdateFacilityObject(Settlement s, Facility f, GameObject go)
        {
            float progress = JobProgress(s, f);

            if (f.Kind == BuildingKinds.CanalEdge)
            {
                Color c = canalDryColor;
                double load = f.State == FacilityState.Operating && s.LastWater != null ? ShownLoad(s.LastWater, f.Edge.Value) : 0;
                if (load > 1e-6)
                {
                    float fill = Mathf.Clamp01((float)(load / Math.Max(1e-6, CanalCapacity(s, f))));
                    c = Color.Lerp(canalDryColor, canalFull, fill);
                    // 반짝임 띠가 맵을 가로질러 흘러간다(흐름 방향이 아니라 '물이 있다'는 표시, M4).
                    Vector3 p = go.transform.position / Mathf.Max(0.01f, Size);
                    float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f - (p.x + p.z) * 1.7f);
                    c = Color.Lerp(c, Color.white, canalShimmer * wave * (0.4f + 0.6f * fill));
                }
                c = f.State == FacilityState.Closed ? canalClosed : Tint(c, f.State, progress);
                WorldVisuals.SetColor(go, c);
                Vector3 scale = go.transform.localScale;
                scale.x = Size * canalThickness * (1f + 0.5f * (f.Level - 1));
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
            if (SettlementRules.IsCrop(f.Kind))
            {
                // 작물(SG3, 경작지·과수원·허브 농장): 완공 뒤 가동 숙성 시간 동안 자라고, 물을 덜 받으면 낮아지고 누렇게 시든다.
                float grown = f.CompletedHour < 0 ? 0f
                    : Mathf.Clamp01((float)((s.CurrentHour - f.CompletedHour) / Math.Max(1.0, s.Rules.MaturityFullHours)));
                float water = 1f;
                if (f.IsOperating && s.LastWater != null && s.LastWater.Consumers.TryGetValue(WaterIds.Of(f), out WaterConsumerResult w))
                {
                    water = Mathf.Clamp01((float)w.Rate);
                }
                model.SetGrowth((0.25f + 0.75f * grown) * (0.55f + 0.45f * water));
                float dry = Mathf.Round((1f - water) * 10f) / 10f;
                crop = c => Color.Lerp(c, witheredCrop, dry);
            }
            model.Paint(c => Tint(c, f.State, progress), crop);
            ApplyLantern(f, model);
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

        /// <summary>시설 색: 인스펙터 덮어쓰기 → 시설 목록 기본 색 → (목록에 없으면) 자홍색.</summary>
        private Color KindColor(string kind)
        {
            foreach (KindColorOverride o in facilityColors)
                if (o.kind == kind) return o.color;
            FacilityType type = FacilityCatalog.Get(kind);
            return type != null ? new Color(type.Color.R, type.Color.G, type.Color.B) : Color.magenta;
        }

        /// <summary>진행 중 작업의 진행률(0~1). 시계의 시간 안 소수점까지 반영해 부드럽게 움직인다.</summary>
        public float JobProgress(Settlement s, Facility f)
        {
            ConstructionJob job = s.Jobs.FirstOrDefault(j => j.FacilityId == f.Id && j.State == JobState.InProgress);
            if (job == null || job.Hours <= 0) return 1f;
            // 정산된 시간(CurrentHour) + 지금 시간 안의 진행분.
            double fraction = _session.Clock.ElapsedHours - Math.Floor(_session.Clock.ElapsedHours);
            return JobProgress(job, s.CurrentHour + fraction);
        }

        /// <summary>
        /// 완공 시각(DeadlineHour 정산이 끝나는 DeadlineHour + 1)까지 남은 시간으로 잰 진행률. 정각 착공은 다음 정각부터 0%,
        /// 명령 착공(JQ2)은 진행 중인 시간을 첫 1시간으로 세므로 명령 순간부터 차오르고 완공 때 100%에 닿는다(이슈 #15).
        /// 모래폭풍·공사 인원 부족으로 완공이 늦춰지면 그만큼 뒤로 물러난다(아직 시각에 반영 안 된 지연까지 더해 한 시간씩 튀지 않게).
        /// </summary>
        public static float JobProgress(ConstructionJob job, double now) =>
            job.Hours <= 0 ? 1f : Mathf.Clamp01((float)(1 - (job.DeadlineHour + job.PendingDelayHours + 1 - now) / job.Hours));
    }
}
