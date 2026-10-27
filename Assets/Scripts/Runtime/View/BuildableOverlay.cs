using System.Collections.Generic;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;
using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 건설 가능 타일 강조(2026-10-27 BO1~BO3, 이슈 #7): 도구 막대에서 지을 시설을 고르면 그 시설을 지을 수 있는 타일 전체가
    /// 반투명 육각형으로 천천히 짙어졌다 옅어진다. 판정은 툴팁 '✓ 건설 가능'과 같은 <see cref="Settlement.CanBuild"/>(지형·빈자리).
    /// 지금 석재로 바로 착공할 수 없으면(무료 완공도 없음) 초록 대신 노랑 — 지을 수는 있지만 대기열에서 석재를 기다린다.
    /// 수로·관리 도구에서는 보이지 않는다. 육각형은 정착지마다 맵 타일 수만큼 한 번 만들고 켜고 끈다.
    /// </summary>
    [RequireComponent(typeof(GameSession))]
    public sealed class BuildableOverlay : MonoBehaviour
    {
        [SerializeField] private Color buildableColor = new Color(0.3f, 1f, 0.4f);
        [Tooltip("지을 수는 있지만 지금 석재가 모자라 대기열에서 기다리는 경우")]
        [SerializeField] private Color waitingColor = new Color(1f, 0.85f, 0.25f);
        [Tooltip("투명도 최소·최대(0~1)")]
        [SerializeField, Range(0f, 1f)] private float alphaMin = 0.15f;
        [SerializeField, Range(0f, 1f)] private float alphaMax = 0.45f;
        [Tooltip("맥동 한 번(옅음 → 짙음 → 옅음)에 걸리는 실제 초")]
        [SerializeField] private float periodSeconds = 2f;
        [Tooltip("육각형 크기(타일 대비). 1보다 작으면 타일 경계가 보인다")]
        [SerializeField] private float inset = 0.82f;
        [Tooltip("가능한 타일 다시 계산 간격(실제 초)")]
        [SerializeField] private float refreshSeconds = 0.2f;

        private GameSession _session;
        private BuildInputController _input;
        private Transform _root;
        private Material _material;
        private Mesh _mesh;
        private readonly List<(HexCoord tile, GameObject hex)> _hexes = new List<(HexCoord, GameObject)>();
        private HexMap _builtFor;
        private float _nextRefresh;
        private string _shownKind;
        private bool _ready;

        private void Awake()
        {
            _session = GetComponent<GameSession>();
            _input = GetComponent<BuildInputController>();
            _root = new GameObject("Buildable Overlay").transform;
            _root.SetParent(transform, false);
            // 반투명·조명 없음. Sprites/Default는 그래픽 설정의 '항상 포함' 셰이더라 빌드에서도 빠지지 않는다.
            _material = new Material(Shader.Find("Sprites/Default")) { name = "Buildable Overlay" };
            // Sprites/Default는 정점 색 × 재질 색이라 정점 색(흰색)이 있는 육각형 사본을 쓴다.
            _mesh = Instantiate(WorldVisuals.HexMesh);
            _mesh.name = "Buildable Hex";
            var white = new Color[_mesh.vertexCount];
            for (int i = 0; i < white.Length; i++) white[i] = Color.white;
            _mesh.colors = white;
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
            if (_mesh != null) Destroy(_mesh);
        }

        private void Update()
        {
            Settlement s = _session.IsReady ? _session.Settlement : null;
            string kind = _input != null && _input.Tool == BuildTool.Build ? _input.BuildKind : null;
            bool show = s != null && kind != null && _session.AcceptsWorldInput;
            _root.gameObject.SetActive(show);
            if (!show)
            {
                _nextRefresh = 0;
                return;
            }
            if (_builtFor != s.Map) Build(s.Map);
            if (Time.unscaledTime >= _nextRefresh || kind != _shownKind) // 시설을 바꾸면 바로 다시
            {
                _shownKind = kind;
                _nextRefresh = Time.unscaledTime + refreshSeconds;
                foreach ((HexCoord tile, GameObject hex) in _hexes) hex.SetActive(s.CanBuild(kind, tile).Success);
                _ready = s.FreeBuildsRemaining(kind) > 0 || s.Stone >= StoneCost(s, kind);
            }
            float wave = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * 2f * Mathf.PI / Mathf.Max(0.1f, periodSeconds));
            Color c = _ready ? buildableColor : waitingColor;
            c.a = Mathf.Lerp(alphaMin, alphaMax, wave);
            _material.color = c;
        }

        /// <summary>L1 건설 석재(지역·장비 배율 포함). 대기열에 넣을 때와 같은 값.</summary>
        private static double StoneCost(Settlement s, string kind) =>
            (s.Rules.Config.GetLevel(kind, 1)?.Stone ?? 0) * s.Rules.StoneCostMultiplier;

        /// <summary>정착지가 바뀌면 그 맵의 타일마다 육각형을 하나씩 만든다(반경 7이면 127개).</summary>
        private void Build(HexMap map)
        {
            foreach ((HexCoord _, GameObject hex) in _hexes) Destroy(hex);
            _hexes.Clear();
            _builtFor = map;
            float size = _session.TileSize * inset;
            foreach (HexTile t in map.Tiles)
            {
                var go = new GameObject("Buildable " + t.Coord);
                go.transform.SetParent(_root, false);
                // 마우스를 올린 타일 표시(높이 0.02)보다 아래, 지면보다 위.
                go.transform.position = _session.ToWorld(_session.Layout.TileCenter(t.Coord), 0.012f);
                go.transform.localScale = new Vector3(size, 1f, size);
                go.AddComponent<MeshFilter>().sharedMesh = _mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = _material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                _hexes.Add((t.Coord, go));
            }
        }
    }
}
