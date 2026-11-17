using System.Collections.Generic;
using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>자연물 모양 중 돌: 각진 바위(크기만 바꿔 여러 곳에 씀), 층 바위 한 켜(암반), 광맥 수정.</summary>
    public static partial class NatureModels
    {
        // 돌 색은 구운 건물 텍스처의 돌 부분(채도 낮은 곳) 색에서 골랐다: 시설이 들어서도 어색하지 않게(결정 GR6).
        /// <summary>암반 층 바위 = 채석장 모델의 돌(중간·밝은 쪽·어두운 쪽).</summary>
        public static readonly Color[] BedrockColors = { new Color(0.66f, 0.57f, 0.49f), new Color(0.74f, 0.65f, 0.56f), new Color(0.57f, 0.49f, 0.42f) };
        /// <summary>샘 둘레 돌 = 우물 모델의 돌.</summary>
        public static readonly Color[] StoneColors = { new Color(0.6f, 0.55f, 0.49f), new Color(0.54f, 0.5f, 0.45f), new Color(0.68f, 0.62f, 0.55f) };
        /// <summary>장애물 바위 = 광산 모델의 바위(회갈색).</summary>
        public static readonly Color[] BoulderColors = { new Color(0.54f, 0.48f, 0.42f), new Color(0.46f, 0.4f, 0.35f), new Color(0.58f, 0.47f, 0.36f) };
        /// <summary>광맥 바위 = 광산 모델의 어두운 바위.</summary>
        public static readonly Color OreRockColor = new Color(0.4f, 0.34f, 0.3f);
        public static readonly Color CrystalColor = new Color(0.68f, 0.38f, 0.95f);
        private static Material _crystal;

        /// <summary>수정 머티리얼(매끈하게 해서 빛을 받으면 반짝인다).</summary>
        public static Material Crystal
        {
            get
            {
                if (_crystal != null) return _crystal;
                _crystal = new Material(WorldVisuals.Solid(CrystalColor)) { name = "Ore Crystal" };
                _crystal.SetFloat("_Smoothness", 0.8f);
                return _crystal;
            }
        }

        /// <summary>
        /// 각진 바위: 정이십면체를 한 번 쪼갠 뒤 꼭짓점을 제멋대로 밀고 당긴 덩어리(반지름 약 1, 각진 면 그대로).
        /// 바닥은 잘라 평평하다(y ≥ −0.35). 크기·납작함은 쓰는 쪽이 scale로.
        /// </summary>
        public static Mesh Rock(int variant) => Cached("Rock " + variant, () =>
        {
            var random = new System.Random(300 + variant);
            (List<Vector3> points, List<int> faces) = Icosphere();
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 p = points[i] * (0.78f + 0.4f * (float)random.NextDouble());
                p.y = Mathf.Max(p.y, -0.35f);
                points[i] = p;
            }
            return Faceted("Rock " + variant, points, faces);
        });

        /// <summary>
        /// 층 바위 한 켜: 둘레가 고르지 않은 두꺼운 판(바닥 y = 0, 높이 1, 반지름 약 1). 옆면은 가운데가 살짝 불룩하고
        /// 윗면 가장자리는 둥글게 깎여(바닥 0.92 → 허리 1 → 윗면 0.8) 풍화된 사암 층처럼 보인다. 몇 켜를 어긋나게 쌓아 쓴다.
        /// </summary>
        public static Mesh Slab(int variant) => Cached("Slab " + variant, () =>
        {
            var random = new System.Random(400 + variant);
            const int n = 14;
            float[] radius = new float[n];
            for (int i = 0; i < n; i++) radius[i] = 0.72f + 0.38f * (float)random.NextDouble();
            (float y, float scale)[] rings = { (0f, 0.92f), (0.5f, 1f), (0.85f, 0.95f), (1f, 0.78f) };
            var points = new List<Vector3>();
            foreach ((float y, float scale) in rings)
                for (int i = 0; i < n; i++)
                {
                    float a = i / (float)n * Mathf.PI * 2f;
                    float wobble = 1f + 0.06f * ((float)random.NextDouble() - 0.5f); // 켜마다 둘레가 조금씩 달라 깎인 듯
                    points.Add(new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius[i] * scale * wobble + Vector3.up * y);
                }
            int top = points.Count;
            points.Add(Vector3.up * 1.04f); // 윗면 가운데가 살짝 솟음
            var faces = new List<int>();
            for (int r = 0; r < rings.Length - 1; r++)
                for (int i = 0; i < n; i++)
                {
                    int a = r * n + i, b = r * n + (i + 1) % n, c = a + n, d = b + n;
                    faces.AddRange(new[] { a, c, b, b, c, d });
                }
            int last = (rings.Length - 1) * n;
            for (int i = 0; i < n; i++) faces.AddRange(new[] { last + i, top, last + (i + 1) % n });
            return Faceted("Slab " + variant, points, faces);
        });

        /// <summary>수정 하나: 육각기둥 + 뾰족한 끝(바닥 y = 0, 높이 약 1, 반지름 0.22).</summary>
        public static Mesh CrystalShard() => Cached("Crystal", () =>
        {
            var points = new List<Vector3>();
            var faces = new List<int>();
            const int sides = 6;
            for (int k = 0; k < sides; k++)
            {
                float a = k / (float)sides * Mathf.PI * 2f;
                var ring = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.22f;
                points.Add(ring);
                points.Add(ring + Vector3.up * 0.75f);
            }
            int tip = points.Count;
            points.Add(Vector3.up);
            for (int k = 0; k < sides; k++)
            {
                int b0 = 2 * k, t0 = b0 + 1, b1 = 2 * ((k + 1) % sides), t1 = b1 + 1;
                faces.AddRange(new[] { b0, t0, b1, b1, t0, t1, t0, tip, t1 });
            }
            return Faceted("Crystal", points, faces);
        });

        private static (List<Vector3>, List<int>) Icosphere()
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var points = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < points.Count; i++) points[i] = points[i].normalized;
            int[] ico =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var middle = new Dictionary<(int, int), int>();
            int Mid(int a, int b)
            {
                var key = a < b ? (a, b) : (b, a);
                if (middle.TryGetValue(key, out int m)) return m;
                points.Add(((points[a] + points[b]) * 0.5f).normalized);
                return middle[key] = points.Count - 1;
            }
            var faces = new List<int>();
            for (int i = 0; i < ico.Length; i += 3)
            {
                int a = ico[i], b = ico[i + 1], c = ico[i + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                faces.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            return (points, faces);
        }

        /// <summary>면마다 꼭짓점을 따로 둬 각진 그림자가 지게 한다. 바깥(가운데 축에서 먼 쪽)을 보게 감기를 맞춘다.</summary>
        private static Mesh Faceted(string name, List<Vector3> points, List<int> faces)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            Vector3 center = Vector3.zero;
            foreach (Vector3 p in points) center += p;
            center /= points.Count;
            for (int i = 0; i < faces.Count; i += 3)
            {
                Vector3 a = points[faces[i]], b = points[faces[i + 1]], c = points[faces[i + 2]];
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f - center) < 0) (b, c) = (c, b);
                triangles.Add(vertices.Count); vertices.Add(a);
                triangles.Add(vertices.Count); vertices.Add(b);
                triangles.Add(vertices.Count); vertices.Add(c);
            }
            return Finish(name, vertices, triangles);
        }
    }
}
