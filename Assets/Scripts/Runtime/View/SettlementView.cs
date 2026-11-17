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
    /// 게임 화면에 정착지를 그린다.
    /// - 타일: 굴곡 있는 모래 육각형 — SettlementView.Ground.cs. 지형은 색 대신 샘·광맥 수정·암반 판석·장애물 바위 — SettlementView.Terrain.cs
    /// - 녹지: 물이 흐르는 수로 옆 사막에 풀이 돋고 야자수가 자란다(SG4) — SettlementView.Greenery.cs
    /// - 시설: 종류별 텍스처 모델(<see cref="FacilityModels"/>). 레벨마다 조금씩 커짐,
    ///   모래에 살짝 묻힌다. 공사 중이면 비계 안에서 진행률만큼 솟고 회색, 폐쇄는 채도를 낮추고 조금 어둡게. 작물 시설은 물이 모자라면 누렇게 시든다(SG1·SG3)
    /// - 수로: 둑으로 둘러싼 물길, 물이 많을수록 수면이 높고 진함, 닫히면 수문 — SettlementView.Canals.cs
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
        [Header("바닥")]
        [Tooltip("모래 색. 모든 칸이 이 색이고 지형은 그 위의 모양(샘·수정·판석·바위)으로 구분한다(결정 GR3·GR5)")]
        [SerializeField] private Color desert = new Color(0.86f, 0.74f, 0.52f);
        [Range(0.8f, 1f)]
        [SerializeField] private float tileGap = 0.96f;

        [Header("시설 색")]
        [Tooltip("시설 기본 색은 시설 목록(Core/Economy/FacilityCatalog.cs)에 있다. 여기 넣은 종류만 덮어쓴다(2026-10-24 CL3)")]
        [SerializeField] private List<KindColorOverride> facilityColors = new List<KindColorOverride>();
        [SerializeField] private Color constructing = new Color(0.6f, 0.6f, 0.6f);
        [SerializeField] private Color demolishing = new Color(0.85f, 0.35f, 0.3f);
        [Tooltip("폐쇄 시설을 어둡게 하는 정도. 채도는 FacilityModels.MutedSaturation만큼 낮아진다")]
        [Range(0f, 1f)]
        [SerializeField] private float closedDim = 0.12f;
        [Tooltip("개량 한 레벨마다 시설 모델이 커지는 비율(L2 = 1 + 이 값, L3 = 1 + 2 × 이 값). 구운 모델은 L1에서 타일 반지름의 BuildingModelBaker.FootprintRadius 안에 들어간다")]
        [Range(0f, 0.2f)]
        [SerializeField] private float upgradeScalePerLevel = DefaultUpgradeScalePerLevel;

        /// <summary>개량 크기 비율의 기본값(에디터 미리보기 캡처도 이 값을 쓴다).</summary>
        public const float DefaultUpgradeScalePerLevel = 0.1f;

        [Header("건설 예약")]
        [SerializeField] private Color planned = new Color(1f, 1f, 1f);

        [Header("작물 시듦")]
        [Tooltip("물이 모자란 작물 시설(경작지·과수원·허브 농장)에 곱하는 색")]
        [SerializeField] private Color witheredCrop = new Color(0.72f, 0.62f, 0.32f);
        [Tooltip("물을 전혀 못 받을 때 시듦 색을 섞는 비율(물이 모자란 만큼 비례)")]
        [Range(0f, 1f)]
        [SerializeField] private float witherTint = 0.6f;

        private GameSession _session;
        private Transform _tilesRoot, _natureRoot, _facilitiesRoot, _plannedRoot;
        private readonly Dictionary<int, GameObject> _facilityObjects = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> _plannedObjects = new Dictionary<int, GameObject>();
        private readonly Dictionary<HexCoord, GameObject> _tileObjects = new Dictionary<HexCoord, GameObject>();
        private readonly HashSet<HexCoord> _occupied = new HashSet<HexCoord>();

        private void Awake()
        {
            _session = GetComponent<GameSession>();
            _tilesRoot = NewRoot("Tiles");
            _natureRoot = NewRoot("Nature"); // 샘·풀·야자수
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

        private void RebuildAll()
        {
            foreach (Transform root in new[] { _tilesRoot, _natureRoot, _facilitiesRoot, _plannedRoot })
            {
                for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);
            }
            _facilityObjects.Clear();
            _plannedObjects.Clear();
            _tileObjects.Clear();
            ResetGreenery();
            ResetSpurs();
            BuildGround();
        }

        private void LateUpdate()
        {
            if (!_session.IsReady) return;
            Settlement s = _session.Settlement;
            UpdateGreening(s);
            _occupied.Clear();
            foreach (Facility f in s.Facilities) if (!f.Edge.HasValue) _occupied.Add(f.Tile);
            UpdateFeatures(_occupied);
            ApplyGreenery(_occupied);
            UpdateLanternMaterials();
            UpdateLabels(s); // 시설 머리 위 글자(정식 UI 아래 캔버스)

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
            UpdateSpurs(s); // 수로 갱신이 모은 꼭짓점 물로 시설 지류를 맞춘다

            // 대기 중인 건설 예약 표시.
            var queued = new HashSet<int>();
            foreach (ConstructionJob job in s.Jobs.Where(j => j.Type == JobType.Build && j.State == JobState.Queued))
            {
                queued.Add(job.Id);
                if (_plannedObjects.ContainsKey(job.Id)) continue;
                GameObject marker = job.Edge.HasValue
                    ? CanalBar(job.Edge.Value, _plannedRoot, planned, 0.02f, 0.5f)
                    : GroundOverlay("Planned " + job.Kind, _plannedRoot, job.Tile, 0.45f, 0.01f, WorldVisuals.Solid(planned));
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
                    return BuildCanal(f.Edge.Value);
                default:
                    return FacilityModels.Build(f.Kind, _facilitiesRoot, KindColor(f.Kind)).gameObject;
            }
        }

        private void UpdateFacilityObject(Settlement s, Facility f, GameObject go)
        {
            float progress = JobProgress(s, f);

            if (f.Kind == BuildingKinds.CanalEdge)
            {
                UpdateCanal(s, f, go.GetComponent<CanalModel>(), progress);
                return;
            }

            // 모양은 타일 반지름 1 기준. 레벨마다 모델 전체가 upgradeScalePerLevel씩 커지고,
            // 공사 중이면 비계 안에서 진행률만큼 땅에서 솟는다(결정 SG1).
            FacilityModel model = go.GetComponent<FacilityModel>();
            float size = 1f + upgradeScalePerLevel * (f.Level - 1);
            float rise = f.State == FacilityState.Constructing ? Mathf.Max(0.1f, progress) : 1f;
            go.transform.localScale = new Vector3(Size, Size, Size);
            go.transform.position = _session.ToWorld(_session.Layout.TileCenter(f.Tile), -facilitySink * Size);
            model.Body.localScale = new Vector3(size, size * rise, size);
            model.ShowScaffold(f.State == FacilityState.Constructing || f.State == FacilityState.Upgrading);

            // 작물 시설(경작지·과수원·허브 농장): 물을 덜 받으면 모델 전체가 누렇게 시든다(결정 SG3).
            float dry = 0f;
            if (SettlementRules.IsCrop(f.Kind) && f.IsOperating && s.LastWater != null
                && s.LastWater.Consumers.TryGetValue(WaterIds.Of(f), out WaterConsumerResult w))
            {
                dry = Mathf.Round((1f - Mathf.Clamp01((float)w.Rate)) * 10f) / 10f;
            }
            model.Paint(c => Tint(Color.Lerp(c, c * witheredCrop, dry * witherTint), f.State, progress), f.State == FacilityState.Closed);
            ApplyLantern(f, model);
        }

        private Color Tint(Color c, FacilityState state, float progress)
        {
            switch (state)
            {
                case FacilityState.Constructing: return Color.Lerp(constructing, c, progress * 0.6f);
                case FacilityState.Closed: return Color.Lerp(c, Color.black, closedDim);
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
            return (float)s.JobProgress(job, s.CurrentHour + fraction);
        }
    }
}
