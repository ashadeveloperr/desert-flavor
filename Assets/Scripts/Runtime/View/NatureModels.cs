using System.Collections.Generic;
using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 자연물 모양(코드로 만드는 메시): 풀 덤불, 야자수(줄기 + 잎), 샘 물웅덩이. 돌·수정은 NatureModels.Rocks.cs.
    /// 좌표는 타일 반지름 1 단위, 바닥 y = 0. 같은 변형 번호의 메시는 하나를 공유한다.
    /// 쓰는 곳: 녹지(<c>SettlementView.Greenery.cs</c>), 지형 장식(<c>SettlementView.Terrain.cs</c>).
    /// </summary>
    public static partial class NatureModels
    {
        public const int Variants = 4;

        public static readonly Color[] GrassColors = { new Color(0.42f, 0.56f, 0.22f), new Color(0.55f, 0.6f, 0.28f), new Color(0.36f, 0.5f, 0.2f) };
        public static readonly Color TrunkColor = new Color(0.46f, 0.34f, 0.22f);
        public static readonly Color FrondColor = new Color(0.3f, 0.5f, 0.18f);
        private static readonly Color WaterColor = new Color(0.18f, 0.48f, 0.62f);

        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();
        private static Material _water;

        /// <summary>물 머티리얼(단색 URP Lit에 매끈함을 올려 하늘·해가 비친다).</summary>
        public static Material Water
        {
            get
            {
                if (_water != null) return _water;
                _water = new Material(WorldVisuals.Solid(WaterColor)) { name = "Spring Water" };
                _water.SetFloat("_Smoothness", 0.88f);
                return _water;
            }
        }

        /// <summary>풀 덤불: 바깥으로 기운 가는 잎 9개(양면). 높이 약 0.12.</summary>
        public static Mesh GrassTuft(int variant) => Cached("Grass " + variant, () =>
        {
            var random = new System.Random(100 + variant);
            var v = new List<Vector3>();
            for (int i = 0; i < 9; i++)
            {
                float angle = (i / 9f + (float)random.NextDouble() * 0.08f) * Mathf.PI * 2f;
                var outward = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                var side = new Vector3(outward.z, 0f, -outward.x);
                float h = 0.07f + 0.06f * (float)random.NextDouble();
                float lean = 0.02f + 0.05f * (float)random.NextDouble();
                Vector3 root = outward * 0.012f;
                v.Add(root - side * 0.011f);
                v.Add(root + side * 0.011f);
                v.Add(root + outward * lean + Vector3.up * h);
            }
            return DoubleSided("Grass " + variant, v, 0.6f);
        });

        /// <summary>야자수 줄기: 한쪽으로 휘며 가늘어지는 관. 높이 <see cref="PalmHeight"/>, 꼭대기 위치는 <see cref="PalmTop"/>.</summary>
        public static Mesh PalmTrunk(int variant) => Cached("Palm Trunk " + variant, () =>
        {
            const int sides = 7, segments = 8;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int s = 0; s <= segments; s++)
            {
                float t = s / (float)segments;
                Vector3 c = TrunkPoint(variant, t);
                // 마디마다 살짝 굵어져 줄기 무늬처럼 보이게.
                float r = Mathf.Lerp(0.032f, 0.018f, t) * (s % 2 == 0 ? 1.08f : 0.96f);
                for (int k = 0; k <= sides; k++)
                {
                    float a = k / (float)sides * Mathf.PI * 2f;
                    vertices.Add(c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                }
            }
            for (int s = 0; s < segments; s++)
            {
                for (int k = 0; k < sides; k++)
                {
                    int i = s * (sides + 1) + k, j = i + sides + 1;
                    triangles.AddRange(new[] { i, j, i + 1, i + 1, j, j + 1 });
                }
            }
            return Finish("Palm Trunk " + variant, vertices, triangles);
        });

        /// <summary>야자수 잎: 꼭대기에서 퍼져 아래로 처지는 잎 9장(가운데가 솟은 V자, 양면). 원점 = 줄기 꼭대기.</summary>
        public static Mesh PalmCrown(int variant) => Cached("Palm Crown " + variant, () =>
        {
            var random = new System.Random(200 + variant);
            var v = new List<Vector3>();
            const int steps = 8;
            for (int f = 0; f < 9; f++)
            {
                float yaw = (f / 9f + (float)random.NextDouble() * 0.05f) * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
                var side = new Vector3(dir.z, 0f, -dir.x);
                float length = 0.22f + 0.08f * (float)random.NextDouble();
                float lift = 0.3f + 0.25f * (float)random.NextDouble();
                Vector3 Ridge(float t) => dir * length * t + Vector3.up * length * (lift * t - 0.95f * t * t);
                for (int s = 0; s < steps; s++)
                {
                    float t0 = s / (float)steps, t1 = (s + 1) / (float)steps;
                    float w0 = 0.06f * Mathf.Sin(Mathf.PI * Mathf.Pow(t0, 0.7f)), w1 = 0.06f * Mathf.Sin(Mathf.PI * Mathf.Pow(t1, 0.7f));
                    Vector3 c0 = Ridge(t0), c1 = Ridge(t1);
                    Vector3 drop0 = Vector3.down * w0 * 0.35f, drop1 = Vector3.down * w1 * 0.35f;
                    foreach (float sign in new[] { -1f, 1f })
                    {
                        v.Add(c0); v.Add(c0 + side * sign * w0 + drop0); v.Add(c1);
                        v.Add(c1); v.Add(c0 + side * sign * w0 + drop0); v.Add(c1 + side * sign * w1 + drop1);
                    }
                }
            }
            return DoubleSided("Palm Crown " + variant, v, 0.3f);
        });

        public const float PalmHeight = 0.5f;

        /// <summary>줄기 꼭대기(잎을 붙일 곳).</summary>
        public static Vector3 PalmTop(int variant) => TrunkPoint(variant, 1f);

        private static Vector3 TrunkPoint(int variant, float t)
        {
            float lean = 0.05f + 0.025f * variant;
            return new Vector3(lean * t * t, PalmHeight * t, 0f);
        }

        /// <summary>샘 물웅덩이: 가장자리가 고르지 않은 납작한 원판(반지름 약 1, 높이 0). 크기는 쓰는 쪽이 맞춘다.</summary>
        public static Mesh SpringWater(int variant) => Cached("Spring " + variant, () =>
        {
            const int n = 28;
            var vertices = new List<Vector3> { Vector3.zero };
            var triangles = new List<int>();
            float phase = variant * 1.7f;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                float r = 1f + 0.09f * Mathf.Sin(3f * a + phase) + 0.05f * Mathf.Sin(5f * a + phase * 2f);
                vertices.Add(new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r));
                triangles.AddRange(new[] { 0, 1 + i, 1 + (i + 1) % n });
            }
            return Finish("Spring " + variant, vertices, triangles);
        });

        private static Mesh Cached(string key, System.Func<Mesh> build)
        {
            if (Cache.TryGetValue(key, out Mesh m) && m != null) return m;
            m = build();
            Cache[key] = m;
            return m;
        }

        private static Mesh Finish(string name, List<Vector3> vertices, List<int> triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>삼각형 목록(3개씩)을 앞뒤 양면으로. 노멀은 위쪽으로 <paramref name="upBlend"/>만큼 기울여 얇은 잎이 덜 번쩍이게.</summary>
        private static Mesh DoubleSided(string name, List<Vector3> triangleCorners, float upBlend)
        {
            var vertices = new List<Vector3>(triangleCorners);
            for (int i = 0; i < triangleCorners.Count; i += 3)
            {
                vertices.Add(triangleCorners[i]);
                vertices.Add(triangleCorners[i + 2]);
                vertices.Add(triangleCorners[i + 1]);
            }
            var triangles = new List<int>();
            for (int i = 0; i < vertices.Count; i++) triangles.Add(i);
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            Vector3[] normals = mesh.normals;
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.Lerp(normals[i], Vector3.up, upBlend).normalized;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
