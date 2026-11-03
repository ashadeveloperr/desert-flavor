using System;
using System.Collections.Generic;
using UnityEngine;

namespace DesertSettlement.Runtime.UI
{
    /// <summary>
    /// 코드로 그리는 작은 아이콘(결정 UV2). 그림 파일·이모지 없이 도형을 겹쳐 64×64 스프라이트를 한 번 만들고 다시 쓴다.
    /// 이모지는 Unity 기본 글꼴에서 네모로 깨질 수 있어 쓰지 않는다. 새 아이콘은 <see cref="Draw"/>에 도형 목록 하나를 더한다.
    /// </summary>
    public static class Icons
    {
        public enum Kind { Cash, Food, Stone, People, Ore, Water, Upkeep }

        private const int N = 64;
        private static readonly Dictionary<Kind, Sprite> Cache = new Dictionary<Kind, Sprite>();

        /// <summary>아이콘 스프라이트(처음 부를 때 그린다).</summary>
        public static Sprite Get(Kind kind)
        {
            if (Cache.TryGetValue(kind, out Sprite s) && s != null) return s;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[N * N];
            foreach ((Func<float, float, float> sdf, Color color) in Draw(kind))
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        // sdf < 0 = 안쪽(픽셀 단위 거리). 경계 1픽셀을 부드럽게.
                        float a = Mathf.Clamp01(0.5f - sdf(x + 0.5f, y + 0.5f)) * color.a;
                        if (a <= 0) continue;
                        Color under = px[y * N + x];
                        float outA = a + under.a * (1 - a);
                        Color c = outA > 0 ? (color * a + under * under.a * (1 - a)) / outA : Color.clear;
                        c.a = outA;
                        px[y * N + x] = c;
                    }
            tex.SetPixels(px);
            tex.Apply();
            return Cache[kind] = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        }

        // ── 도형(좌표는 0~64, y는 위로) ──
        private static Func<float, float, float> Circle(float cx, float cy, float r) =>
            (x, y) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;

        private static Func<float, float, float> Ellipse(float cx, float cy, float rx, float ry, float degrees = 0)
        {
            float c = Mathf.Cos(degrees * Mathf.Deg2Rad), s = Mathf.Sin(degrees * Mathf.Deg2Rad);
            return (x, y) =>
            {
                float dx = x - cx, dy = y - cy;
                float u = dx * c + dy * s, v = -dx * s + dy * c;
                float k = Mathf.Sqrt(u * u / (rx * rx) + v * v / (ry * ry));
                return (k - 1) * Mathf.Min(rx, ry);
            };
        }

        private static Func<float, float, float> Box(float cx, float cy, float hw, float hh, float round = 0)
        {
            return (x, y) =>
            {
                float qx = Mathf.Abs(x - cx) - hw + round, qy = Mathf.Abs(y - cy) - hh + round;
                float outside = Mathf.Sqrt(Mathf.Max(qx, 0) * Mathf.Max(qx, 0) + Mathf.Max(qy, 0) * Mathf.Max(qy, 0));
                return outside + Mathf.Min(Mathf.Max(qx, qy), 0) - round;
            };
        }

        /// <summary>볼록 다각형(꼭짓점은 시계 반대 방향).</summary>
        private static Func<float, float, float> Poly(params Vector2[] p)
        {
            return (x, y) =>
            {
                float d = float.MinValue;
                for (int i = 0; i < p.Length; i++)
                {
                    Vector2 a = p[i], b = p[(i + 1) % p.Length];
                    Vector2 n = new Vector2(b.y - a.y, a.x - b.x).normalized; // 바깥쪽 법선
                    d = Mathf.Max(d, (x - a.x) * n.x + (y - a.y) * n.y);
                }
                return d;
            };
        }

        private static Func<float, float, float> Minus(Func<float, float, float> a, Func<float, float, float> b) => (x, y) => Mathf.Max(a(x, y), -b(x, y));
        private static Func<float, float, float> Grow(Func<float, float, float> a, float by) => (x, y) => a(x, y) - by;

        private static Color C(float r, float g, float b, float a = 1) => new Color(r, g, b, a);
        private static readonly Color Outline = C(0.10f, 0.08f, 0.06f, 0.95f);

        /// <summary>아이콘마다 아래에서 위로 그릴 도형과 색. 첫 줄은 대개 어두운 외곽선(밝은 바탕에서도 보이게).</summary>
        private static IEnumerable<(Func<float, float, float>, Color)> Draw(Kind kind)
        {
            switch (kind)
            {
                case Kind.Cash: // 가운데 네모 구멍이 있는 금화
                {
                    var coin = Circle(32, 32, 25);
                    var hole = Box(32, 32, 6, 6, 1);
                    yield return (Grow(coin, 3), Outline);
                    yield return (Minus(coin, hole), C(0.78f, 0.55f, 0.16f));
                    yield return (Minus(Circle(32, 32, 20), Grow(hole, 3)), C(0.98f, 0.80f, 0.32f));
                    yield return (Minus(Ellipse(26, 40, 8, 4, 30), Grow(hole, 3)), C(1f, 0.93f, 0.65f, 0.8f));
                    break;
                }
                case Kind.Food: // 밀 이삭
                {
                    var stalk = Box(32, 18, 2.2f, 14);
                    var grains = new List<Func<float, float, float>> { Ellipse(32, 52, 5, 8) };
                    for (int i = 0; i < 3; i++)
                    {
                        float y = 24 + i * 9;
                        grains.Add(Ellipse(24, y + 2, 5, 8.5f, 35));
                        grains.Add(Ellipse(40, y + 2, 5, 8.5f, -35));
                    }
                    yield return (Grow(stalk, 2.5f), Outline);
                    foreach (var g in grains) yield return (Grow(g, 2.5f), Outline);
                    yield return (stalk, C(0.72f, 0.55f, 0.22f));
                    foreach (var g in grains) yield return (g, C(0.96f, 0.78f, 0.34f));
                    break;
                }
                case Kind.Stone: // 쌓은 돌 세 개
                {
                    var blocks = new[] { Box(20, 18, 13, 10, 4), Box(44, 18, 13, 10, 4), Box(32, 39, 14, 10, 4) };
                    foreach (var b in blocks) yield return (Grow(b, 2.5f), Outline);
                    yield return (blocks[0], C(0.62f, 0.60f, 0.56f));
                    yield return (blocks[1], C(0.70f, 0.68f, 0.63f));
                    yield return (blocks[2], C(0.80f, 0.78f, 0.72f));
                    yield return (Box(28, 43, 6, 2, 1), C(0.92f, 0.90f, 0.85f, 0.8f));
                    break;
                }
                case Kind.People: // 머리와 어깨
                {
                    var head = Circle(32, 44, 10);
                    var body = Minus(Ellipse(32, 14, 20, 16), Box(32, -4, 40, 6));
                    yield return (Grow(head, 2.5f), Outline);
                    yield return (Grow(body, 2.5f), Outline);
                    yield return (body, C(0.55f, 0.70f, 0.82f));
                    yield return (head, C(0.93f, 0.80f, 0.62f));
                    break;
                }
                case Kind.Ore: // 보석 모양 광물
                {
                    var gem = Poly(new Vector2(32, 6), new Vector2(56, 34), new Vector2(46, 52), new Vector2(18, 52), new Vector2(8, 34));
                    yield return (Grow(gem, 2.5f), Outline);
                    yield return (gem, C(0.48f, 0.42f, 0.86f));
                    yield return (Poly(new Vector2(32, 6), new Vector2(46, 34), new Vector2(18, 34)), C(0.64f, 0.58f, 0.96f));
                    yield return (Poly(new Vector2(18, 52), new Vector2(18, 34), new Vector2(46, 34), new Vector2(46, 52)), C(0.75f, 0.71f, 1f, 0.9f));
                    break;
                }
                case Kind.Water: // 물방울
                {
                    var drop = (Func<float, float, float>)((x, y) => Mathf.Min(Circle(32, 24, 16)(x, y),
                        Poly(new Vector2(32, 60), new Vector2(17.5f, 30), new Vector2(46.5f, 30))(x, y)));
                    yield return (Grow(drop, 2.5f), Outline);
                    yield return (drop, C(0.36f, 0.70f, 0.92f));
                    yield return (Ellipse(25, 24, 4, 7, -20), C(0.85f, 0.95f, 1f, 0.8f));
                    break;
                }
                case Kind.Upkeep: // 아래로 향한 화살표(지출)
                {
                    var arrow = (Func<float, float, float>)((x, y) => Mathf.Min(Box(32, 40, 8, 16)(x, y),
                        Poly(new Vector2(32, 6), new Vector2(54, 28), new Vector2(10, 28))(x, y)));
                    yield return (Grow(arrow, 2.5f), Outline);
                    yield return (arrow, C(0.92f, 0.42f, 0.34f));
                    break;
                }
            }
        }
    }
}
