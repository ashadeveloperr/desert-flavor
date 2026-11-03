using System;
using System.Collections.Generic;
using UnityEngine;

namespace DesertSettlement.Runtime.UI
{
    /// <summary>시설(도구 막대 버튼)과 관리 도구 아이콘(결정 HT3). 시설과 아이콘의 짝은 <see cref="HelpTexts"/>.</summary>
    public static partial class Icons
    {
        private static IEnumerable<(Func<float, float, float>, Color)> DrawFacility(Kind kind)
        {
            switch (kind)
            {
                case Kind.Well: // 지붕 달린 돌우물
                {
                    var roof = Poly(new Vector2(6, 46), new Vector2(58, 46), new Vector2(32, 62));
                    var posts = Union(Box(15, 36, 2.5f, 12), Box(49, 36, 2.5f, 12));
                    var body = Box(32, 18, 20, 13, 3);
                    yield return (Grow(Union(roof, posts, body), 2.5f), Outline);
                    yield return (posts, C(0.55f, 0.40f, 0.25f));
                    yield return (roof, C(0.75f, 0.35f, 0.25f));
                    yield return (body, C(0.66f, 0.64f, 0.60f));
                    yield return (Ellipse(32, 30, 18, 4.5f), C(0.30f, 0.62f, 0.90f));
                    yield return (Box(32, 14, 20, 1.2f), C(0.48f, 0.46f, 0.43f));
                    break;
                }
                case Kind.Canal: // 둑 사이로 흐르는 물길
                {
                    var banks = Union(Box(32, 50, 28, 6, 2), Box(32, 14, 28, 6, 2));
                    var water = Box(32, 32, 28, 11);
                    yield return (Grow(Union(banks, water), 2.5f), Outline);
                    yield return (water, C(0.30f, 0.62f, 0.90f));
                    yield return (banks, C(0.72f, 0.58f, 0.38f));
                    yield return (Capsule(12, 34, 24, 30, 1.8f), C(0.85f, 0.95f, 1f, 0.9f));
                    yield return (Capsule(30, 34, 42, 30, 1.8f), C(0.85f, 0.95f, 1f, 0.9f));
                    yield return (Capsule(46, 34, 54, 31, 1.8f), C(0.85f, 0.95f, 1f, 0.9f));
                    break;
                }
                case Kind.WaterTank: // 다리 달린 원통 탱크
                {
                    var legs = Union(Box(18, 10, 2.5f, 8), Box(46, 10, 2.5f, 8));
                    var tank = Union(Box(32, 34, 20, 16), Ellipse(32, 50, 20, 6), Ellipse(32, 18, 20, 6));
                    yield return (Grow(Union(legs, tank), 2.5f), Outline);
                    yield return (legs, C(0.45f, 0.42f, 0.40f));
                    yield return (tank, C(0.70f, 0.74f, 0.78f));
                    yield return (Union(Box(32, 26, 20, 8), Ellipse(32, 18, 20, 6)), C(0.30f, 0.62f, 0.90f));
                    yield return (Ellipse(32, 50, 16, 3.5f), C(0.85f, 0.88f, 0.92f));
                    break;
                }
                case Kind.Fruit: // 사과
                {
                    var apple = Union(Circle(24, 28, 17), Circle(40, 28, 17));
                    var stem = Capsule(32, 42, 35, 56, 2.2f);
                    var leaf = Ellipse(44, 52, 9, 4.5f, 25);
                    yield return (Grow(Union(apple, stem, leaf), 2.5f), Outline);
                    yield return (stem, C(0.45f, 0.30f, 0.18f));
                    yield return (leaf, C(0.42f, 0.72f, 0.30f));
                    yield return (apple, C(0.86f, 0.24f, 0.20f));
                    yield return (Ellipse(20, 34, 4, 7, -25), C(1f, 0.70f, 0.62f, 0.8f));
                    break;
                }
                case Kind.Meat: // 뼈 달린 고기
                {
                    var bone = Union(Capsule(14, 14, 28, 28, 4), Circle(10, 18, 5.5f), Circle(18, 10, 5.5f));
                    var meat = Ellipse(38, 38, 20, 15, 45);
                    yield return (Grow(Union(bone, meat), 2.5f), Outline);
                    yield return (bone, C(0.95f, 0.92f, 0.84f));
                    yield return (meat, C(0.70f, 0.30f, 0.22f));
                    yield return (Ellipse(41, 41, 13, 9, 45), C(0.82f, 0.42f, 0.30f));
                    yield return (Ellipse(44, 46, 5, 3, 45), C(0.95f, 0.70f, 0.60f, 0.8f));
                    break;
                }
                case Kind.Herb: // 잎 두 장
                {
                    var leafA = Ellipse(38, 38, 10, 22, -35);
                    var leafB = Ellipse(22, 30, 8, 16, 40);
                    var stem = Capsule(20, 6, 30, 26, 2);
                    yield return (Grow(Union(leafA, leafB, stem), 2.5f), Outline);
                    yield return (stem, C(0.35f, 0.50f, 0.25f));
                    yield return (leafB, C(0.38f, 0.62f, 0.32f));
                    yield return (leafA, C(0.48f, 0.76f, 0.38f));
                    yield return (Capsule(28, 22, 48, 54, 1.3f), C(0.30f, 0.50f, 0.24f));
                    break;
                }
                case Kind.Bread: // 칼집 낸 빵 덩어리
                {
                    var loaf = Union(Ellipse(32, 30, 27, 16), Box(32, 22, 27, 8, 4));
                    yield return (Grow(loaf, 2.5f), Outline);
                    yield return (loaf, C(0.78f, 0.52f, 0.24f));
                    yield return (Ellipse(32, 34, 22, 10), C(0.88f, 0.64f, 0.32f));
                    for (int i = -1; i <= 1; i++) yield return (Ellipse(32 + i * 13, 34, 3, 8, -35), C(0.98f, 0.86f, 0.60f));
                    break;
                }
                case Kind.Beer: // 거품 넘치는 맥주잔
                {
                    var mug = Box(28, 26, 15, 20, 3);
                    var handle = Minus(Circle(44, 28, 11), Circle(44, 28, 5.5f));
                    var foam = Union(Circle(18, 48, 7), Circle(28, 50, 8), Circle(38, 48, 7));
                    yield return (Grow(Union(mug, handle, foam), 2.5f), Outline);
                    yield return (handle, C(0.85f, 0.75f, 0.50f));
                    yield return (mug, C(0.92f, 0.62f, 0.16f));
                    yield return (Box(22, 26, 3, 15, 1.5f), C(1f, 0.82f, 0.40f, 0.8f));
                    yield return (foam, C(0.98f, 0.96f, 0.90f));
                    break;
                }
                case Kind.Wine: // 포도주 잔
                {
                    var bowl = Minus(Circle(32, 42, 16), Box(32, 64, 20, 9));
                    var stem = Box(32, 18, 2.5f, 10);
                    var foot = Ellipse(32, 8, 13, 3.5f);
                    yield return (Grow(Union(bowl, stem, foot), 2.5f), Outline);
                    yield return (Union(stem, foot), C(0.85f, 0.88f, 0.90f));
                    yield return (bowl, C(0.85f, 0.88f, 0.90f));
                    yield return (Minus(Circle(32, 42, 13.5f), Box(32, 60, 20, 12)), C(0.58f, 0.10f, 0.22f));
                    yield return (Ellipse(24, 46, 2.5f, 5, -15), C(1f, 1f, 1f, 0.6f));
                    break;
                }
                case Kind.House: // 지붕과 문
                {
                    var roof = Poly(new Vector2(4, 34), new Vector2(60, 34), new Vector2(32, 60));
                    var body = Box(32, 20, 21, 15);
                    yield return (Grow(Union(roof, body), 2.5f), Outline);
                    yield return (body, C(0.92f, 0.82f, 0.62f));
                    yield return (roof, C(0.78f, 0.38f, 0.26f));
                    yield return (Box(32, 13, 6, 8, 1), C(0.45f, 0.30f, 0.18f));
                    yield return (Box(19, 24, 4.5f, 4.5f), C(0.40f, 0.65f, 0.85f));
                    yield return (Box(45, 24, 4.5f, 4.5f), C(0.40f, 0.65f, 0.85f));
                    break;
                }
                case Kind.Warehouse: // 나무 상자
                {
                    var crate = Box(32, 30, 24, 22, 2);
                    yield return (Grow(crate, 2.5f), Outline);
                    yield return (crate, C(0.62f, 0.42f, 0.22f));
                    yield return (Box(32, 30, 18, 16), C(0.78f, 0.56f, 0.32f));
                    yield return (Capsule(16, 16, 48, 44, 3), C(0.62f, 0.42f, 0.22f));
                    yield return (Box(32, 30, 18, 2), C(0.62f, 0.42f, 0.22f));
                    break;
                }
                case Kind.Market: // 줄무늬 차양 노점
                {
                    var awning = Box(32, 46, 28, 9, 2);
                    var posts = Union(Box(9, 26, 2.5f, 14), Box(55, 26, 2.5f, 14));
                    var counter = Box(32, 16, 26, 8, 2);
                    yield return (Grow(Union(awning, posts, counter), 2.5f), Outline);
                    yield return (posts, C(0.55f, 0.40f, 0.25f));
                    yield return (counter, C(0.72f, 0.52f, 0.30f));
                    yield return (awning, C(0.96f, 0.92f, 0.84f));
                    for (int i = 0; i < 4; i++) yield return (Box(11 + i * 14, 46, 3.5f, 9), C(0.85f, 0.28f, 0.24f));
                    yield return (Union(Circle(22, 27, 4), Circle(32, 27, 4), Circle(42, 27, 4)), C(0.95f, 0.70f, 0.25f));
                    break;
                }
                case Kind.Hospital: // 흰 판에 붉은 십자
                {
                    var plate = Box(32, 32, 26, 26, 6);
                    var cross = Union(Box(32, 32, 7, 18, 1.5f), Box(32, 32, 18, 7, 1.5f));
                    yield return (Grow(plate, 2.5f), Outline);
                    yield return (plate, C(0.95f, 0.95f, 0.93f));
                    yield return (cross, C(0.88f, 0.22f, 0.20f));
                    break;
                }
                case Kind.Gavel: // 경매 망치와 받침
                {
                    var head = Capsule(18, 32, 34, 48, 8);
                    var handle = Capsule(28, 38, 54, 12, 3);
                    var block = Box(20, 8, 15, 4.5f, 2);
                    yield return (Grow(Union(head, handle, block), 2.5f), Outline);
                    yield return (block, C(0.55f, 0.38f, 0.22f));
                    yield return (handle, C(0.72f, 0.52f, 0.30f));
                    yield return (head, C(0.62f, 0.42f, 0.22f));
                    yield return (Capsule(20, 34, 32, 46, 3), C(0.95f, 0.75f, 0.30f));
                    break;
                }
                case Kind.Pointer: // 마우스 화살표
                {
                    var arrow = Union(Poly(new Vector2(16, 60), new Vector2(16, 16), new Vector2(48, 30)), Capsule(30, 28, 40, 8, 5));
                    yield return (Grow(arrow, 2.5f), Outline);
                    yield return (arrow, C(0.96f, 0.94f, 0.90f));
                    break;
                }
                case Kind.Upgrade: // 위로 향한 화살표
                {
                    var arrow = Union(Box(32, 22, 8, 16), Poly(new Vector2(10, 34), new Vector2(54, 34), new Vector2(32, 58)));
                    yield return (Grow(arrow, 2.5f), Outline);
                    yield return (arrow, C(0.48f, 0.80f, 0.44f));
                    break;
                }
                case Kind.Demolish: // 붉은 X
                {
                    var x = Union(Capsule(14, 14, 50, 50, 6), Capsule(14, 50, 50, 14, 6));
                    yield return (Grow(x, 2.5f), Outline);
                    yield return (x, C(0.92f, 0.38f, 0.32f));
                    break;
                }
                case Kind.Close: // 일시정지 막대(운영 멈춤·재개)
                {
                    var bars = Union(Box(22, 32, 7, 22, 2), Box(42, 32, 7, 22, 2));
                    yield return (Grow(bars, 2.5f), Outline);
                    yield return (bars, C(0.95f, 0.66f, 0.30f));
                    break;
                }
            }
        }
    }
}
