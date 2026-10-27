using System.Collections.Generic;
using DesertSettlement.Core.Hex;
using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 맵 바깥 사막(2026-10-27 DB1~DB4, 이슈 #17): 맵 둘레를 끝없는 모래언덕으로 채운다.
    /// - 언덕: 맵 가장자리 1~3칸은 거의 평평하고, 멀어질수록 타일 크기의 0.8~1.5배 높이로 바람 방향을 따라 굽이친다(완만한 앞면·가파른 뒷면).
    /// - 구분: 바깥 모래는 사막 타일보다 아주 조금 어둡고, 맵 가장자리 육각형 윤곽을 따라 양옆으로 흐려지는 옅은 경계선을 긋는다
    ///   (처음의 진한 선·붉은 모래는 타일이 떠 보여 2026-10-27 사용자 요청으로 흐리게). 평지 모래는 타일 바로 밑이다.
    /// - 잔물결: 바람에 수직인 가는 무늬(반복 텍스처). 먼 곳은 선형 안개로 하늘색에 녹아들고 밤에는 어두워진다.
    ///   안개는 맵을 덮지 않도록 카메라 ~ 맵 가장 먼 곳 뒤에서 시작한다. 카메라 배경도 안개 색으로 칠한다.
    /// 정착지가 바뀔 때(<see cref="GameSession.Initialized"/>) 다시 만든다. 모양 수치는 인스펙터.
    /// </summary>
    [RequireComponent(typeof(GameSession))]
    public sealed class DesertBackdrop : MonoBehaviour
    {
        [Header("모래 색·경계")]
        [Tooltip("바깥 모래 색(맵 사막 타일보다 아주 조금 어둡게. 2026-10-27 사용자: 타일과 더 비슷하게)")]
        [SerializeField] private Color duneColor = new Color(0.83f, 0.70f, 0.49f);
        [Tooltip("맵 가장자리 윤곽선 색과 진하기(알파). 가운데가 가장 진하고 양옆으로 투명해지는 부드러운 띠")]
        [SerializeField] private Color borderColor = new Color(0.36f, 0.27f, 0.18f, 0.35f);
        [Tooltip("윤곽선 굵기(타일 크기 대비)")]
        [SerializeField] private float borderWidth = 0.16f;
        [Tooltip("맵 아래·가장자리 평지 모래의 높이(타일 크기 대비, 타일 바로 밑)")]
        [SerializeField] private float baseHeight = -0.004f;

        [Header("언덕 모양 (타일 크기 기준)")]
        [Tooltip("맵 가장자리에서 이 거리까지는 평평하고, 그 뒤 '언덕 시작 완료'까지 서서히 솟는다")]
        [SerializeField] private float flatTiles = 0.5f;
        [SerializeField] private float riseTiles = 2.5f;
        [Tooltip("가까운 언덕 높이 → 먼 언덕 높이, 먼 높이에 다다르는 거리")]
        [SerializeField] private float nearHeight = 0.8f;
        [SerializeField] private float farHeight = 1.5f;
        [SerializeField] private float farTiles = 10f;
        [Tooltip("언덕 사이 간격과 바람 방향(도, 북쪽에서 시계 방향)")]
        [SerializeField] private float duneSpacingTiles = 4.5f;
        [SerializeField] private float windDegrees = 25f;
        [Tooltip("바깥 사막의 반 폭과 격자 수(넓을수록 멀리까지, 격자가 많을수록 매끈)")]
        [SerializeField] private float extentTiles = 90f;
        [SerializeField, Range(40, 250)] private int gridCells = 200;
        [SerializeField] private int seed = 7;

        [Header("잔물결")]
        [Tooltip("잔물결 간격(타일 크기 대비)과 진하기(0 = 없음)")]
        [SerializeField] private float rippleSpacing = 0.22f;
        [SerializeField, Range(0f, 0.3f)] private float rippleStrength = 0.045f;

        [Header("먼 곳 흐림 (안개)")]
        [SerializeField] private Color dayHaze = new Color(0.78f, 0.84f, 0.90f);
        [SerializeField] private Color nightHaze = new Color(0.07f, 0.09f, 0.16f);
        [Tooltip("맵 가장 먼 곳 뒤 안개가 시작하는 여유와, 완전히 흐려지기까지의 거리(타일 크기 대비)")]
        [SerializeField] private float fogStartMarginTiles = 2f;
        [SerializeField] private float fogRangeTiles = 40f;

        private GameSession _session;
        private Transform _root;
        private Material _sandMaterial, _borderMaterial;
        private Texture2D _rippleTexture;
        private Mesh _duneMesh, _borderMesh;
        private float _mapRadiusWorld;

        private void Awake()
        {
            _session = GetComponent<GameSession>();
            _root = new GameObject("Desert Backdrop").transform;
            _root.SetParent(transform, false);
        }

        private void OnEnable()
        {
            _session.Initialized += Rebuild;
            if (_session.IsReady) Rebuild();
        }

        private void OnDisable()
        {
            if (_session != null) _session.Initialized -= Rebuild;
        }

        private void OnDestroy()
        {
            foreach (Object o in new Object[] { _sandMaterial, _borderMaterial, _rippleTexture, _duneMesh, _borderMesh })
            {
                if (o != null) Destroy(o);
            }
        }

        private float Size => _session.TileSize;

        private void Rebuild()
        {
            for (int i = _root.childCount - 1; i >= 0; i--) Destroy(_root.GetChild(i).gameObject);
            if (_sandMaterial == null)
            {
                _rippleTexture = RippleTexture(rippleStrength);
                _sandMaterial = WorldVisuals.Solid(duneColor);
                _sandMaterial = new Material(_sandMaterial) { name = "Desert Backdrop Sand", mainTexture = _rippleTexture };
                // 반투명 띠(빌드에서도 빠지지 않는 셰이더, 정점 알파 × 색 알파).
                _borderMaterial = WorldVisuals.Unlit("Desert Backdrop Border", borderColor);
            }

            var centers = new List<Vector2>();
            foreach (HexTile tile in _session.Map.Tiles)
            {
                HexPoint p = _session.Layout.TileCenter(tile.Coord);
                centers.Add(new Vector2((float)p.X, (float)p.Z));
            }
            _mapRadiusWorld = 0f;
            foreach (Vector2 c in centers) _mapRadiusWorld = Mathf.Max(_mapRadiusWorld, c.magnitude + Size);

            if (_duneMesh != null) Destroy(_duneMesh);
            if (_borderMesh != null) Destroy(_borderMesh);
            _duneMesh = DuneMesh(centers);
            _borderMesh = BorderMesh();
            Child("Dunes", _duneMesh, _sandMaterial);
            Child("Map Border", _borderMesh, _borderMaterial);
        }

        private void Child(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>바깥 사막 언덕의 높이(맵 평면 좌표, 맵 가장자리까지의 거리 <paramref name="edgeDistance"/>).</summary>
        private float DuneHeight(Vector2 p, float edgeDistance)
        {
            float t = Size;
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(flatTiles * t, riseTiles * t, edgeDistance));
            if (rise <= 0f) return baseHeight * t;
            float amplitude = Mathf.Lerp(nearHeight, farHeight, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(riseTiles * t, farTiles * t, edgeDistance))) * t;

            float a = windDegrees * Mathf.Deg2Rad;
            var wind = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
            var across = new Vector2(wind.y, -wind.x);
            float u = Vector2.Dot(p, wind) / t, v = Vector2.Dot(p, across) / t;
            float warp = (Mathf.PerlinNoise(seed + p.x * 0.04f / t, seed * 2 + p.y * 0.04f / t) - 0.5f) * duneSpacingTiles * 2f;
            float s = Mathf.Repeat((u + warp) / duneSpacingTiles, 1f);
            // 바람 맞는 앞면은 완만하게(75%), 뒷면은 가파르게(25%).
            float shaped = s < 0.75f ? s / 0.75f * 0.5f : 0.5f + (s - 0.75f) / 0.25f * 0.5f;
            float profile = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * shaped);
            float crest = Mathf.Lerp(0.4f, 1f, Mathf.PerlinNoise(seed * 3 + v * 0.06f, seed * 5 + u * 0.03f));
            float swell = Mathf.PerlinNoise(seed * 7 + p.x * 0.025f / t, seed * 11 + p.y * 0.025f / t);
            return rise * amplitude * (0.65f * profile * crest + 0.35f * swell) + baseHeight * t;
        }

        private Mesh DuneMesh(List<Vector2> centers)
        {
            float half = extentTiles * Size;
            int n = gridCells;
            float step = 2f * half / n;
            float rippleTile = rippleSpacing * Size * 8f; // 텍스처 한 장에 잔물결 8줄
            float a = windDegrees * Mathf.Deg2Rad;
            var wind = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
            var across = new Vector2(wind.y, -wind.x);

            var vertices = new Vector3[(n + 1) * (n + 1)];
            var uv = new Vector2[vertices.Length];
            for (int z = 0; z <= n; z++)
            {
                for (int x = 0; x <= n; x++)
                {
                    var p = new Vector2(-half + x * step, -half + z * step);
                    float nearest = float.MaxValue;
                    if (p.magnitude < _mapRadiusWorld + riseTiles * Size + 1f)
                    {
                        foreach (Vector2 c in centers) nearest = Mathf.Min(nearest, (p - c).sqrMagnitude);
                        nearest = Mathf.Sqrt(nearest) - Size;
                    }
                    else
                    {
                        nearest = p.magnitude - _mapRadiusWorld;
                    }
                    int i = z * (n + 1) + x;
                    vertices[i] = new Vector3(p.x, DuneHeight(p, nearest), p.y);
                    // 잔물결은 바람에 수직(바람 방향으로 반복).
                    uv[i] = new Vector2(Vector2.Dot(p, across) / rippleTile, Vector2.Dot(p, wind) / rippleTile);
                }
            }

            var triangles = new int[n * n * 6];
            int k = 0;
            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    int i = z * (n + 1) + x;
                    triangles[k++] = i;
                    triangles[k++] = i + n + 1;
                    triangles[k++] = i + 1;
                    triangles[k++] = i + 1;
                    triangles[k++] = i + n + 1;
                    triangles[k++] = i + n + 2;
                }
            }
            var mesh = new Mesh { name = "Desert Dunes", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>이웃이 맵 밖인 타일 변마다 얇은 띠를 이어 맵 가장자리 윤곽을 그린다.</summary>
        private Mesh BorderMesh()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var colors = new List<Color>();
            float width = borderWidth * Size;
            float apothem = Size * Mathf.Sqrt(3f) * 0.5f;
            foreach (HexTile tile in _session.Map.Tiles)
            {
                HexPoint c = _session.Layout.TileCenter(tile.Coord);
                var center = new Vector2((float)c.X, (float)c.Z);
                for (int i = 0; i < 6; i++)
                {
                    // 꼭짓점 i(북쪽에서 시계 방향 60°씩, WorldVisuals.HexMesh와 같음)와 i+1 사이 변, 바깥 방향은 60i + 30°.
                    float mid = Mathf.PI / 3f * i + Mathf.PI / 6f;
                    var outward = new Vector2(Mathf.Sin(mid), Mathf.Cos(mid));
                    Vector2 neighbor = center + outward * apothem * 2f;
                    if (_session.Map.Contains(_session.Layout.PointToHex(new HexPoint(neighbor.x, neighbor.y)))) continue;
                    Vector2 a = center + Corner(i) * Size, b = center + Corner(i + 1) * Size;
                    // 변 위가 가장 진하고 안쪽·바깥쪽으로 투명해지는 부드러운 띠(정점 알파). 양끝은 꼭짓점 방향으로 넓혀
                    // 이웃 변의 띠와 겹치지 않고 맞물린다(겹치면 모서리만 진해진다).
                    float radial = 1f / Mathf.Cos(Mathf.PI / 6f);
                    Vector2 da = Corner(i) * width * radial, db = Corner(i + 1) * width * radial;
                    int v0 = vertices.Count;
                    foreach ((Vector2 q, float alpha) in new[]
                             {
                                 (a - da * 0.4f, 0f), (a, 1f), (a + da * 0.6f, 0f),
                                 (b - db * 0.4f, 0f), (b, 1f), (b + db * 0.6f, 0f),
                             })
                    {
                        vertices.Add(new Vector3(q.x, 0.006f * Size, q.y));
                        colors.Add(new Color(1f, 1f, 1f, alpha));
                    }
                    triangles.AddRange(new[] { v0, v0 + 1, v0 + 4, v0, v0 + 4, v0 + 3, v0 + 1, v0 + 2, v0 + 5, v0 + 1, v0 + 5, v0 + 4 });
                }
            }
            var mesh = new Mesh { name = "Map Border", vertices = vertices.ToArray(), colors = colors.ToArray(), triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            // 감기 방향과 상관없이 위를 보게.
            Vector3[] normals = mesh.normals;
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            mesh.normals = normals;
            int[] tris = mesh.triangles;
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 n = Vector3.Cross(vertices[tris[i + 1]] - vertices[tris[i]], vertices[tris[i + 2]] - vertices[tris[i]]);
                if (n.y < 0) (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
            }
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector2 Corner(int i)
        {
            float angle = Mathf.PI / 3f * (i % 6);
            return new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
        }

        /// <summary>바람에 수직인 잔물결 무늬(가로로 반복, 세로로 8줄). 밝기 1 ± strength.</summary>
        private static Texture2D RippleTexture(float strength)
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Sand Ripples", wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = x / (float)size, fy = y / (float)size;
                    float wave = Mathf.Sin(2f * Mathf.PI * (fy * 8f + 0.18f * Mathf.Sin(2f * Mathf.PI * fx * 2f) + 0.06f * Mathf.Sin(2f * Mathf.PI * fx * 5f)));
                    // 잔물결 마루는 밝고 좁게, 골은 넓게.
                    float shade = 1f + strength * (wave > 0 ? wave * wave : -0.5f * wave * wave) - strength * 0.25f;
                    pixels[y * size + x] = new Color(shade, shade, shade, 1f);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(true);
            return tex;
        }

        private void LateUpdate()
        {
            if (!_session.IsReady) return;
            Camera cam = Camera.main;
            if (cam == null) return;

            // 낮·밤에 따라 안개(먼 곳 흐림)와 카메라 배경색을 바꾼다. 등불과 같은 시각 곡선(해 지기 1.5시간 전부터 어두워짐).
            var time = _session.TimeSettings;
            float night = DayNightLighting.LanternFactor(_session.Clock.HourOfDay, time.DayStartHour, time.NightStartHour, 1.5f, 0.5f);
            Color haze = Color.Lerp(dayHaze, nightHaze, night);
            float cameraToMap = Vector3.Distance(cam.transform.position, transform.position);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = haze;
            RenderSettings.fogStartDistance = cameraToMap + _mapRadiusWorld + fogStartMarginTiles * Size;
            RenderSettings.fogEndDistance = RenderSettings.fogStartDistance + fogRangeTiles * Size;
            // 경계선은 조명을 받지 않는 셰이더라 밤에는 직접 어둡게 한다(안 그러면 어두운 땅 위에 밝게 뜬다).
            if (_borderMaterial != null)
            {
                Color border = borderColor;
                float dim = 1f - 0.8f * night;
                _borderMaterial.color = new Color(border.r * dim, border.g * dim, border.b * dim, border.a);
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = haze;
        }
    }
}
