using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Hex;
using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 맵 바닥(결정 GR1·GR2): 타일마다 굴곡 있는 모래 메시. 높이는 맵 평면 위치의 노이즈라 이웃 타일과 무늬가 이어지고,
    /// 타일 가장자리에서는 0으로 내려가 수로·경계가 그대로 보인다. 시설은 <see cref="facilitySink"/>만큼 모래에 묻힌다.
    /// 지형별 바닥 모양과 그 위의 장식(샘·광맥·암반·장애물)은 SettlementView.Terrain.cs.
    /// 건설 가능 표시·마우스 강조·건설 예약은 <see cref="GroundMeshFor"/>로 같은 굴곡을 따라 덮는다.
    /// </summary>
    public sealed partial class SettlementView
    {
        [Header("바닥 모래 (타일 반지름 1 기준)")]
        [Tooltip("모래 굴곡의 최고 높이")]
        [SerializeField] private float sandBumpHeight = 0.09f;
        [Tooltip("굴곡 무늬 크기(타일 한 칸에 들어가는 언덕 수 정도). 클수록 잘다")]
        [SerializeField] private float sandBumpScale = 1.6f;
        [SerializeField] private int sandSeed = 13;
        [Tooltip("타일 메시를 쪼개는 정도(변 하나를 몇 칸으로)")]
        [SerializeField, Range(2, 16)] private int sandRings = 8;
        [Tooltip("시설 모델이 모래에 묻히는 깊이")]
        [SerializeField] private float facilitySink = 0.045f;

        private readonly Dictionary<(HexCoord, float, float), Mesh> _groundMeshes = new Dictionary<(HexCoord, float, float), Mesh>();
        private readonly Dictionary<HexCoord, List<(Vector2, Vector2)>> _carves = new Dictionary<HexCoord, List<(Vector2, Vector2)>>();
        private Material _sandTemplate;
        private DesertBackdrop _backdrop;

        /// <summary>
        /// 바닥 높이(타일 반지름 1 단위). <paramref name="local"/>은 타일 가운데 기준 위치(같은 단위).
        /// 가장자리에서 0, 안쪽은 0 ~ <see cref="sandBumpHeight"/>. 지형마다 모양을 덧입힌다(<see cref="TerrainShape"/>).
        /// </summary>
        public float GroundHeight(HexCoord tile, Vector2 local)
        {
            HexPoint c = _session.Layout.TileCenter(tile);
            Vector2 p = new Vector2((float)c.X, (float)c.Z) / Size + local;
            float noise = 0.65f * Mathf.PerlinNoise(sandSeed + p.x * sandBumpScale, sandSeed * 2 + p.y * sandBumpScale)
                          + 0.35f * Mathf.PerlinNoise(sandSeed * 3 + p.x * sandBumpScale * 2.7f, sandSeed * 5 + p.y * sandBumpScale * 2.7f);
            float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.97f, 0.7f, GroundMesh.HexDistance(local)));
            float height = sandBumpHeight * noise * edge;
            if (_session.Map.TryGetTile(tile, out HexTile t)) height = TerrainShape(t.Terrain, local, height, edge);
            return height * Carve(tile, local);
        }

        /// <summary>
        /// 수로 지류가 지나는 자리의 모래를 파낸 배율(0 = 바닥 높이 0, 1 = 그대로). 지류 물길 폭의 약 1.5배까지 0, 그 밖으로 서서히 원래대로.
        /// 지류가 없는 칸은 1.
        /// </summary>
        private float Carve(HexCoord tile, Vector2 local)
        {
            if (!_carves.TryGetValue(tile, out List<(Vector2 a, Vector2 b)> segments) || segments.Count == 0) return 1f;
            float d = float.MaxValue;
            foreach ((Vector2 a, Vector2 b) in segments)
            {
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(local - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                d = Mathf.Min(d, Vector2.Distance(local, a + ab * t));
            }
            float r = canalThickness * 0.5f;
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(r * 1.5f, r * 3f, d));
        }

        /// <summary>
        /// 칸의 파낸 자리(지류 선분, 타일 가운데 기준 타일 단위)를 바꾸고, 바뀌었으면 그 칸 바닥 메시를 다시 만든다.
        /// 그 칸을 덮는 표시 메시도 캐시에서 지워 다음에 새 굴곡으로 만들게 한다.
        /// </summary>
        private void SetCarves(HexCoord tile, List<(Vector2, Vector2)> segments)
        {
            _carves.TryGetValue(tile, out List<(Vector2, Vector2)> old);
            if ((old == null || old.Count == 0) && segments.Count == 0) return;
            if (old != null && old.SequenceEqual(segments)) return;
            _carves[tile] = segments;
            foreach (var key in _groundMeshes.Keys.Where(k => k.Item1.Equals(tile)).ToList())
            {
                Destroy(_groundMeshes[key]);
                _groundMeshes.Remove(key);
            }
            if (_tileObjects.TryGetValue(tile, out GameObject go)) go.GetComponent<MeshFilter>().sharedMesh = GroundMeshFor(tile, tileGap, 0f);
        }

        /// <summary>
        /// 타일 위를 굴곡대로 덮는 육각형 메시(반지름 <paramref name="extent"/>, 바닥보다 <paramref name="lift"/> 위). 오브젝트 크기는 타일 크기로.
        /// 정착지가 바뀌면 다시 만든다. 타일 바닥 자체는 extent = tileGap, lift = 0.
        /// </summary>
        public Mesh GroundMeshFor(HexCoord tile, float extent, float lift)
        {
            if (_groundMeshes.TryGetValue((tile, extent, lift), out Mesh mesh) && mesh != null) return mesh;
            if (_backdrop == null) _backdrop = GetComponent<DesertBackdrop>();
            HexPoint c = _session.Layout.TileCenter(tile);
            var center = new Vector2((float)c.X, (float)c.Z);
            mesh = GroundMesh.Build($"Ground {tile}", extent, sandRings, local => GroundHeight(tile, local) + lift,
                _backdrop != null ? local => _backdrop.RippleUv(center + local * Size) : (System.Func<Vector2, Vector2>)null);
            _groundMeshes[(tile, extent, lift)] = mesh;
            return mesh;
        }

        /// <summary>타일 위에 굴곡을 따라 놓이는 표시 오브젝트(건설 예약 등).</summary>
        public GameObject GroundOverlay(string name, Transform parent, HexCoord tile, float extent, float lift, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = _session.ToWorld(_session.Layout.TileCenter(tile));
            go.transform.localScale = Vector3.one * Size;
            go.AddComponent<MeshFilter>().sharedMesh = GroundMeshFor(tile, extent, lift);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>정착지가 바뀌면: 바닥 메시와 지형 장식을 새로 만든다.</summary>
        private void BuildGround()
        {
            foreach (Mesh m in _groundMeshes.Values) if (m != null) Destroy(m);
            _groundMeshes.Clear();
            _carves.Clear();
            _features.Clear();
            if (_backdrop == null) _backdrop = GetComponent<DesertBackdrop>();
            if (_sandTemplate == null)
            {
                _sandTemplate = new Material(WorldVisuals.Solid(Color.white)) { name = "Sand" };
                if (_backdrop != null) _sandTemplate.mainTexture = _backdrop.Ripples;
            }

            foreach (HexTile tile in _session.Map.Tiles)
            {
                GameObject go = GroundOverlay($"Tile {tile.Coord}", _tilesRoot, tile.Coord, tileGap, 0f, WorldVisuals.Tinted(_sandTemplate, desert));
                go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                _tileObjects[tile.Coord] = go;
                GameObject feature = BuildFeature(tile);
                if (feature != null) _features[tile.Coord] = feature;
            }
        }
    }
}
