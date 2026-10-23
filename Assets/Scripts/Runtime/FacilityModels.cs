using System;
using System.Collections.Generic;
using DesertSettlement.Core.Config;
using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 시설 하나의 표시 오브젝트(여러 부품). 부품마다 원래 색을 기억해 두고, 상태 색(공사·폐쇄·개량·철거)을 모든 부품에 함께 입힌다.
    /// </summary>
    public sealed class FacilityModel : MonoBehaviour
    {
        private readonly List<(MeshRenderer renderer, Color color)> _parts = new List<(MeshRenderer, Color)>();

        public int PartCount => _parts.Count;

        public void Add(GameObject part, Color color) => _parts.Add((part.GetComponent<MeshRenderer>(), color));

        /// <summary>부품마다 <paramref name="tint"/>(원래 색 → 표시 색)를 적용한다.</summary>
        public void Paint(Func<Color, Color> tint)
        {
            foreach ((MeshRenderer renderer, Color color) in _parts) renderer.sharedMaterial = WorldVisuals.Solid(tint(color));
        }
    }

    /// <summary>
    /// 시설 외형(2026-10-23 로드맵 3-①, Docs/decision_log_auto.md M1~M5). 외부 에셋 없이 기본 도형을 조합한다.
    /// 좌표는 타일 반지름 1 기준(바닥 y = 0)이고, <see cref="SettlementView"/>가 루트를 타일 크기·레벨·공사 진행률로 늘린다.
    /// 입구·문은 −z(화면 아래쪽) 방향이다. 시설 고유색(<paramref name="body"/>)은 인스펙터 색을 그대로 쓰고, 나머지는 공통 재료색.
    /// </summary>
    public static class FacilityModels
    {
        private static readonly Color Stone = new Color(0.66f, 0.62f, 0.56f);
        private static readonly Color Wood = new Color(0.48f, 0.33f, 0.2f);
        private static readonly Color DarkWood = new Color(0.32f, 0.22f, 0.14f);
        private static readonly Color Soil = new Color(0.55f, 0.4f, 0.25f);
        private static readonly Color Dark = new Color(0.12f, 0.1f, 0.09f);
        private static readonly Color Cream = new Color(0.94f, 0.9f, 0.8f);
        private static readonly Color Red = new Color(0.85f, 0.15f, 0.15f);

        public static FacilityModel Build(string kind, Transform parent, Color body)
        {
            var root = new GameObject(kind);
            root.transform.SetParent(parent, false);
            FacilityModel m = root.AddComponent<FacilityModel>();
            switch (kind)
            {
                case BuildingKinds.Well: Well(m, body); break;
                case BuildingKinds.Farm: Farm(m, body); break;
                case BuildingKinds.Quarry: Quarry(m, body); break;
                case BuildingKinds.Mine: Mine(m, body); break;
                case BuildingKinds.AuctionHall: AuctionHall(m, body); break;
                case BuildingKinds.Hospital: Hospital(m, body); break;
                case BuildingKinds.WaterTank: WaterTank(m, body); break;
                case BuildingKinds.Warehouse: Warehouse(m, body); break;
                default: Part(m, PrimitiveType.Cube, body, new Vector3(0, 0.2f, 0), new Vector3(0.6f, 0.4f, 0.6f)); break;
            }
            return m;
        }

        /// <summary>부품 하나. 원기둥·구의 scale.y는 Unity 기본 도형 기준(원기둥 높이 = 2 × scale.y).</summary>
        private static void Part(FacilityModel m, PrimitiveType type, Color color, Vector3 position, Vector3 scale, Vector3 euler = default)
        {
            GameObject go = WorldVisuals.Primitive(type, type.ToString(), m.transform, color);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler);
            m.Add(go, color);
        }

        // 우물: 돌 테두리 + 물 + 기둥 두 개와 도르래 지붕.
        private static void Well(FacilityModel m, Color body)
        {
            Part(m, PrimitiveType.Cylinder, Stone, new Vector3(0, 0.12f, 0), new Vector3(0.6f, 0.12f, 0.6f));
            Part(m, PrimitiveType.Cylinder, body, new Vector3(0, 0.245f, 0), new Vector3(0.46f, 0.01f, 0.46f));
            Part(m, PrimitiveType.Cube, Wood, new Vector3(-0.27f, 0.42f, 0), new Vector3(0.05f, 0.6f, 0.05f));
            Part(m, PrimitiveType.Cube, Wood, new Vector3(0.27f, 0.42f, 0), new Vector3(0.05f, 0.6f, 0.05f));
            Part(m, PrimitiveType.Cube, Wood, new Vector3(0, 0.66f, 0), new Vector3(0.6f, 0.05f, 0.05f));
            Part(m, PrimitiveType.Cube, DarkWood, new Vector3(0, 0.76f, 0), new Vector3(0.7f, 0.04f, 0.42f));
        }

        // 농장: 흙 바닥 + 작물 이랑 네 줄.
        private static void Farm(FacilityModel m, Color body)
        {
            Part(m, PrimitiveType.Cube, Soil, new Vector3(0, 0.03f, 0), new Vector3(1.2f, 0.06f, 1.0f));
            for (int i = 0; i < 4; i++)
            {
                float x = -0.42f + 0.28f * i;
                Part(m, PrimitiveType.Cube, body, new Vector3(x, 0.12f, 0), new Vector3(0.14f, 0.12f, 0.88f));
            }
        }

        // 채석장: 계단식으로 깎인 돌 + 떨어진 돌덩이.
        private static void Quarry(FacilityModel m, Color body)
        {
            Color deep = Color.Lerp(body, Dark, 0.25f);
            Part(m, PrimitiveType.Cube, body, new Vector3(0, 0.08f, 0), new Vector3(0.85f, 0.16f, 0.85f));
            Part(m, PrimitiveType.Cube, deep, new Vector3(0.1f, 0.24f, 0.1f), new Vector3(0.6f, 0.16f, 0.6f));
            Part(m, PrimitiveType.Cube, body, new Vector3(0.18f, 0.38f, 0.18f), new Vector3(0.34f, 0.12f, 0.34f));
            Part(m, PrimitiveType.Cube, deep, new Vector3(-0.32f, 0.24f, -0.3f), new Vector3(0.16f, 0.16f, 0.16f), new Vector3(0, 25, 0));
            Part(m, PrimitiveType.Cube, body, new Vector3(-0.15f, 0.22f, -0.38f), new Vector3(0.1f, 0.1f, 0.1f), new Vector3(0, -15, 0));
        }

        // 광산: 언덕 + 갱도 입구(나무 틀) + 권양탑과 바퀴.
        private static void Mine(FacilityModel m, Color body)
        {
            Part(m, PrimitiveType.Sphere, body, new Vector3(0, 0, 0.05f), new Vector3(0.95f, 0.75f, 0.85f));
            Part(m, PrimitiveType.Cube, Dark, new Vector3(0, 0.12f, -0.34f), new Vector3(0.22f, 0.24f, 0.14f));
            Part(m, PrimitiveType.Cube, Wood, new Vector3(-0.13f, 0.13f, -0.4f), new Vector3(0.04f, 0.26f, 0.04f));
            Part(m, PrimitiveType.Cube, Wood, new Vector3(0.13f, 0.13f, -0.4f), new Vector3(0.04f, 0.26f, 0.04f));
            Part(m, PrimitiveType.Cube, Wood, new Vector3(0, 0.27f, -0.4f), new Vector3(0.32f, 0.05f, 0.05f));
            Part(m, PrimitiveType.Cube, Wood, new Vector3(0.28f, 0.48f, 0.18f), new Vector3(0.05f, 0.62f, 0.05f));
            Part(m, PrimitiveType.Cylinder, DarkWood, new Vector3(0.28f, 0.8f, 0.18f), new Vector3(0.22f, 0.02f, 0.22f), new Vector3(90, 0, 0));
        }

        // 경매장: 기단 + 금빛 박공지붕 + 앞 기둥 셋 + 깃발.
        private static void AuctionHall(FacilityModel m, Color body)
        {
            Part(m, PrimitiveType.Cube, Cream, new Vector3(0, 0.2f, 0.04f), new Vector3(0.8f, 0.4f, 0.56f));
            Part(m, PrimitiveType.Cube, body, new Vector3(0, 0.4f, 0), new Vector3(0.88f, 0.42f, 0.42f), new Vector3(45, 0, 0));
            for (int i = -1; i <= 1; i++)
            {
                Part(m, PrimitiveType.Cylinder, Cream, new Vector3(0.3f * i, 0.2f, -0.32f), new Vector3(0.07f, 0.2f, 0.07f));
            }
            Part(m, PrimitiveType.Cube, Wood, new Vector3(0.36f, 0.85f, 0.2f), new Vector3(0.025f, 0.5f, 0.025f));
            Part(m, PrimitiveType.Cube, Red, new Vector3(0.45f, 1.02f, 0.2f), new Vector3(0.17f, 0.11f, 0.01f));
        }

        // 병원: 흰 건물 + 지붕의 붉은 십자 + 문.
        private static void Hospital(FacilityModel m, Color body)
        {
            Part(m, PrimitiveType.Cube, body, new Vector3(0, 0.2f, 0), new Vector3(0.72f, 0.4f, 0.6f));
            Part(m, PrimitiveType.Cube, Red, new Vector3(0, 0.41f, 0), new Vector3(0.38f, 0.02f, 0.11f));
            Part(m, PrimitiveType.Cube, Red, new Vector3(0, 0.41f, 0), new Vector3(0.11f, 0.02f, 0.38f));
            Part(m, PrimitiveType.Cube, Dark, new Vector3(0, 0.11f, -0.305f), new Vector3(0.15f, 0.22f, 0.02f));
        }

        // 물탱크: 나무 다리 넷 위의 둥근 통.
        private static void WaterTank(FacilityModel m, Color body)
        {
            foreach (float x in new[] { -0.22f, 0.22f })
                foreach (float z in new[] { -0.22f, 0.22f })
                    Part(m, PrimitiveType.Cube, Wood, new Vector3(x, 0.16f, z), new Vector3(0.05f, 0.32f, 0.05f));
            Part(m, PrimitiveType.Cylinder, body, new Vector3(0, 0.5f, 0), new Vector3(0.62f, 0.18f, 0.62f));
            Part(m, PrimitiveType.Cylinder, Color.Lerp(body, Dark, 0.3f), new Vector3(0, 0.69f, 0), new Vector3(0.42f, 0.01f, 0.42f));
        }

        // 창고: 긴 건물 + 어두운 박공지붕 + 큰 문.
        private static void Warehouse(FacilityModel m, Color body)
        {
            Part(m, PrimitiveType.Cube, body, new Vector3(0, 0.18f, 0), new Vector3(0.92f, 0.36f, 0.56f));
            Part(m, PrimitiveType.Cube, DarkWood, new Vector3(0, 0.36f, 0), new Vector3(0.98f, 0.4f, 0.4f), new Vector3(45, 0, 0));
            Part(m, PrimitiveType.Cube, Dark, new Vector3(0, 0.12f, -0.285f), new Vector3(0.26f, 0.24f, 0.02f));
        }
    }
}
