using System;
using System.Collections.Generic;
using DesertSettlement.Core.Config;
using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>부품의 쓰임: 몸체(레벨·공사 진행률로 높이가 바뀜), 작물(자람·시듦), 비계(공사·개량 중에만 보임), 등불(상태 색을 입히지 않음, 2026-10-27 이슈 #13).</summary>
    public enum FacilityPartRole { Body, Crop, Scaffold, Lantern }

    /// <summary>
    /// 시설 하나의 표시 오브젝트(여러 부품). 부품마다 원래 색을 기억해 두고, 상태 색(공사·폐쇄·개량·철거)을 모든 부품에 함께 입힌다.
    /// 루트는 타일 크기로만 늘리고, 높이 변화는 <see cref="Body"/> 아래 부품에만 건다(비계는 높이가 그대로).
    /// 레벨 부품은 <see cref="ApplyLevel"/>로 그 레벨부터 보인다(2026-10-23 로드맵 3-②, SG1~SG5).
    /// </summary>
    public sealed class FacilityModel : MonoBehaviour
    {
        private sealed class PartInfo
        {
            public MeshRenderer Renderer;
            public Color Color;
            public FacilityPartRole Role;
            public int MinLevel;
            public Vector3 BaseScale, BasePosition;
        }

        private readonly List<PartInfo> _parts = new List<PartInfo>();

        /// <summary>몸체 부품의 부모. SettlementView가 localScale.y로 높이를 바꾼다.</summary>
        public Transform Body { get; private set; }

        public int PartCount => _parts.Count;
        public int VisiblePartCount { get { int n = 0; foreach (PartInfo p in _parts) if (p.Renderer.gameObject.activeSelf) n++; return n; } }
        public bool ScaffoldVisible { get; private set; }

        private void EnsureBody()
        {
            if (Body != null) return;
            Body = new GameObject("Body").transform;
            Body.SetParent(transform, false);
        }

        public Transform ParentFor(FacilityPartRole role)
        {
            EnsureBody();
            return role == FacilityPartRole.Scaffold || role == FacilityPartRole.Lantern ? transform : Body;
        }

        public void Add(GameObject part, Color color, FacilityPartRole role = FacilityPartRole.Body, int minLevel = 1)
        {
            _parts.Add(new PartInfo
            {
                Renderer = part.GetComponent<MeshRenderer>(), Color = color, Role = role, MinLevel = minLevel,
                BaseScale = part.transform.localScale, BasePosition = part.transform.localPosition,
            });
            if (role == FacilityPartRole.Scaffold || minLevel > 1) part.SetActive(false);
        }

        /// <summary>그 레벨까지의 부품만 보이게 한다(비계는 따로).</summary>
        public void ApplyLevel(int level)
        {
            foreach (PartInfo p in _parts)
            {
                if (p.Role != FacilityPartRole.Scaffold && p.Role != FacilityPartRole.Lantern) p.Renderer.gameObject.SetActive(p.MinLevel <= level);
            }
        }

        public void ShowScaffold(bool show)
        {
            if (show == ScaffoldVisible) return;
            ScaffoldVisible = show;
            foreach (PartInfo p in _parts)
            {
                if (p.Role == FacilityPartRole.Scaffold) p.Renderer.gameObject.SetActive(show);
            }
        }

        private MeshRenderer _glow;
        private Light _light;
        private Material _glowLit;

        private void OnDestroy()
        {
            if (_glowLit != null) Destroy(_glowLit);
        }

        /// <summary>등불 불빛 부품과 바닥을 비추는 점광원(<see cref="FacilityModels"/>가 만든다).</summary>
        public void SetLanternParts(MeshRenderer glow, Light light)
        {
            _glow = glow;
            _light = light;
        }

        /// <summary>
        /// 등불(2026-10-27 LT1~LT3, 이슈 #13): <paramref name="lit"/>이면 이 시설 몫의 불빛 머티리얼(<paramref name="litTemplate"/> 사본)을
        /// <paramref name="litColor"/>로 칠하고 점광원 <paramref name="lightIntensity"/>, 아니면(폐쇄) 꺼진 유리 머티리얼과 광원 끔.
        /// 시설마다 숨쉬는 밝기가 달라서(LT5, 이슈 #16) 불빛 머티리얼은 시설마다 따로 둔다.
        /// </summary>
        public void SetLantern(bool lit, Color litColor, Material litTemplate, Material offMaterial, float lightIntensity, Color lightColor, float lightRange)
        {
            if (lit && _glowLit == null && litTemplate != null) _glowLit = new Material(litTemplate);
            if (_glowLit != null) _glowLit.color = litColor;
            if (_glow != null) _glow.sharedMaterial = lit ? _glowLit : offMaterial;
            if (_light == null) return;
            bool on = lit && lightIntensity > 0.01f;
            if (_light.enabled != on) _light.enabled = on;
            if (!on) return;
            _light.intensity = lightIntensity;
            _light.color = lightColor;
            _light.range = lightRange;
        }

        public bool LanternLightOn => _light != null && _light.enabled;

        /// <summary>작물 높이(0~1 배). 바닥은 그대로 두고 위로 자란다.</summary>
        public void SetGrowth(float growth)
        {
            foreach (PartInfo p in _parts)
            {
                if (p.Role != FacilityPartRole.Crop) continue;
                Transform t = p.Renderer.transform;
                float bottom = p.BasePosition.y - p.BaseScale.y * 0.5f;
                t.localScale = new Vector3(p.BaseScale.x, p.BaseScale.y * growth, p.BaseScale.z);
                t.localPosition = new Vector3(p.BasePosition.x, bottom + p.BaseScale.y * growth * 0.5f, p.BasePosition.z);
            }
        }

        /// <summary>부품마다 <paramref name="tint"/>(원래 색 → 표시 색)를 적용한다. 작물은 먼저 <paramref name="crop"/>(시듦 색)을 거친다.</summary>
        public void Paint(Func<Color, Color> tint, Func<Color, Color> crop = null)
        {
            foreach (PartInfo p in _parts)
            {
                if (!p.Renderer.gameObject.activeSelf || p.Role == FacilityPartRole.Lantern) continue;
                Color c = p.Role == FacilityPartRole.Crop && crop != null ? crop(p.Color) : p.Color;
                p.Renderer.sharedMaterial = WorldVisuals.Solid(p.Role == FacilityPartRole.Scaffold ? c : tint(c));
            }
        }
    }

    /// <summary>
    /// 시설 외형(2026-10-23 로드맵 3-①·②, Docs/decision_log_auto.md M1~M5·SG1~SG5). 외부 에셋 없이 기본 도형을 조합한다.
    /// 좌표는 타일 반지름 1 기준(바닥 y = 0)이고, <see cref="SettlementView"/>가 루트를 타일 크기로, 몸체를 레벨·공사 진행률로 늘린다.
    /// 입구·문은 −z(화면 아래쪽) 방향이다. 시설 고유색(<paramref name="body"/>)은 인스펙터 색을 그대로 쓰고, 나머지는 공통 재료색.
    /// 레벨 부품(L2·L3)은 개량할수록 시설에 붙는 것들(도르래 두레박·물길 도랑·기중기·광차 선로 등).
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
        private static readonly Color Water = new Color(0.25f, 0.55f, 0.9f);
        private static readonly Color Scaffold = new Color(0.78f, 0.62f, 0.38f);

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
            Scaffolding(m, kind == BuildingKinds.Farm ? 0.25f : 0.75f);
            Lantern(m, kind == BuildingKinds.Farm ? new Vector3(-0.66f, 0, -0.56f) : new Vector3(-0.52f, 0, -0.52f));
            return m;
        }

        /// <summary>
        /// 부품 하나. 원기둥·구의 scale.y는 Unity 기본 도형 기준(원기둥 높이 = 2 × scale.y).
        /// <paramref name="level"/>이 2 이상이면 그 레벨부터 보인다.
        /// </summary>
        private static void Part(FacilityModel m, PrimitiveType type, Color color, Vector3 position, Vector3 scale,
            Vector3 euler = default, int level = 1, FacilityPartRole role = FacilityPartRole.Body)
        {
            GameObject go = WorldVisuals.Primitive(type, type.ToString(), m.ParentFor(role), color);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler);
            m.Add(go, color, role, level);
        }

        // 등불(2026-10-27 LT1, 이슈 #13): 앞 왼쪽 귀퉁이의 나무 기둥 + 가로대 + 매달린 불빛 + 바닥을 비추는 약한 점광원.
        // 불빛은 조명을 받지 않는 머티리얼이라 밤에도 밝게 보이고, 밝기·색은 SettlementView가 시각에 따라 바꾼다. 몸체 높이(공사 진행률)와 상관없이 그대로.
        private static void Lantern(FacilityModel m, Vector3 at)
        {
            Part(m, PrimitiveType.Cube, DarkWood, at + new Vector3(0, 0.26f, 0), new Vector3(0.035f, 0.52f, 0.035f), role: FacilityPartRole.Lantern);
            Part(m, PrimitiveType.Cube, DarkWood, at + new Vector3(0.05f, 0.51f, 0), new Vector3(0.12f, 0.025f, 0.025f), role: FacilityPartRole.Lantern);
            GameObject glow = WorldVisuals.Primitive(PrimitiveType.Sphere, "Lantern Glow", m.transform, Cream);
            glow.GetComponent<MeshFilter>().sharedMesh = WorldVisuals.WithWhiteVertexColors(glow.GetComponent<MeshFilter>().sharedMesh);
            MeshRenderer glowRenderer = glow.GetComponent<MeshRenderer>();
            glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glow.transform.localPosition = at + new Vector3(0.1f, 0.44f, 0);
            glow.transform.localScale = new Vector3(0.08f, 0.1f, 0.08f);

            var lightObject = new GameObject("Lantern Light");
            lightObject.transform.SetParent(m.transform, false);
            lightObject.transform.localPosition = at + new Vector3(0.1f, 0.5f, 0);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.enabled = false;
            m.SetLanternParts(glowRenderer, light);
        }

        // 공사장 비계: 네 귀퉁이 기둥 + 위·가운데 가로대. 공사·개량 중에만 보인다(SG1).
        private static void Scaffolding(FacilityModel m, float height)
        {
            const float r = 0.42f;
            foreach (float x in new[] { -r, r })
                foreach (float z in new[] { -r, r })
                    Part(m, PrimitiveType.Cube, Scaffold, new Vector3(x, height * 0.5f, z), new Vector3(0.035f, height, 0.035f), role: FacilityPartRole.Scaffold);
            foreach (float y in new[] { height * 0.5f, height })
            {
                Part(m, PrimitiveType.Cube, Scaffold, new Vector3(0, y, -r), new Vector3(2 * r, 0.025f, 0.025f), role: FacilityPartRole.Scaffold);
                Part(m, PrimitiveType.Cube, Scaffold, new Vector3(0, y, r), new Vector3(2 * r, 0.025f, 0.025f), role: FacilityPartRole.Scaffold);
                Part(m, PrimitiveType.Cube, Scaffold, new Vector3(-r, y, 0), new Vector3(0.025f, 0.025f, 2 * r), role: FacilityPartRole.Scaffold);
                Part(m, PrimitiveType.Cube, Scaffold, new Vector3(r, y, 0), new Vector3(0.025f, 0.025f, 2 * r), role: FacilityPartRole.Scaffold);
            }
        }

        // 우물: 돌 테두리 + 물 + 기둥 두 개와 도르래 지붕. L2 두레박, L3 물받이 돌 수조.
        private static void Well(FacilityModel m, Color body)
        {
            Part(m, PrimitiveType.Cylinder, Stone, new Vector3(0, 0.12f, 0), new Vector3(0.6f, 0.12f, 0.6f));
            Part(m, PrimitiveType.Cylinder, body, new Vector3(0, 0.245f, 0), new Vector3(0.46f, 0.01f, 0.46f));
            Part(m, PrimitiveType.Cube, Wood, new Vector3(-0.27f, 0.42f, 0), new Vector3(0.05f, 0.6f, 0.05f));
            Part(m, PrimitiveType.Cube, Wood, new Vector3(0.27f, 0.42f, 0), new Vector3(0.05f, 0.6f, 0.05f));
            Part(m, PrimitiveType.Cube, Wood, new Vector3(0, 0.66f, 0), new Vector3(0.6f, 0.05f, 0.05f));
            Part(m, PrimitiveType.Cube, DarkWood, new Vector3(0, 0.76f, 0), new Vector3(0.7f, 0.04f, 0.42f));
            Part(m, PrimitiveType.Cube, Dark, new Vector3(0, 0.52f, 0), new Vector3(0.01f, 0.28f, 0.01f), level: 2);
            Part(m, PrimitiveType.Cylinder, Wood, new Vector3(0, 0.36f, 0), new Vector3(0.1f, 0.05f, 0.1f), level: 2);
            Part(m, PrimitiveType.Cube, Stone, new Vector3(0.48f, 0.07f, -0.2f), new Vector3(0.18f, 0.14f, 0.4f), level: 3);
            Part(m, PrimitiveType.Cube, Water, new Vector3(0.48f, 0.141f, -0.2f), new Vector3(0.12f, 0.01f, 0.34f), level: 3);
        }

        // 농장: 흙 바닥 + 작물 이랑 네 줄(자라고 시든다, SG3). L2 앞쪽 물 도랑, L3 구석 헛간.
        private static void Farm(FacilityModel m, Color body)
        {
            Part(m, PrimitiveType.Cube, Soil, new Vector3(0, 0.03f, 0), new Vector3(1.2f, 0.06f, 1.0f));
            for (int i = 0; i < 4; i++)
            {
                float x = -0.42f + 0.28f * i;
                Part(m, PrimitiveType.Cube, body, new Vector3(x, 0.12f, 0), new Vector3(0.14f, 0.12f, 0.88f), role: FacilityPartRole.Crop);
            }
            Part(m, PrimitiveType.Cube, Water, new Vector3(0, 0.062f, -0.47f), new Vector3(1.15f, 0.01f, 0.05f), level: 2);
            Part(m, PrimitiveType.Cube, Wood, new Vector3(0.5f, 0.12f, 0.38f), new Vector3(0.2f, 0.18f, 0.2f), level: 3);
            Part(m, PrimitiveType.Cube, DarkWood, new Vector3(0.5f, 0.23f, 0.38f), new Vector3(0.24f, 0.04f, 0.24f), level: 3);
        }

        // 채석장: 계단식으로 깎인 돌 + 떨어진 돌덩이. L2 나무 기중기, L3 돌 실은 수레.
        private static void Quarry(FacilityModel m, Color body)
        {
            Color deep = Color.Lerp(body, Dark, 0.25f);
            Part(m, PrimitiveType.Cube, body, new Vector3(0, 0.08f, 0), new Vector3(0.85f, 0.16f, 0.85f));
            Part(m, PrimitiveType.Cube, deep, new Vector3(0.1f, 0.24f, 0.1f), new Vector3(0.6f, 0.16f, 0.6f));
            Part(m, PrimitiveType.Cube, body, new Vector3(0.18f, 0.38f, 0.18f), new Vector3(0.34f, 0.12f, 0.34f));
            Part(m, PrimitiveType.Cube, deep, new Vector3(-0.32f, 0.24f, -0.3f), new Vector3(0.16f, 0.16f, 0.16f), new Vector3(0, 25, 0));
            Part(m, PrimitiveType.Cube, body, new Vector3(-0.15f, 0.22f, -0.38f), new Vector3(0.1f, 0.1f, 0.1f), new Vector3(0, -15, 0));
            Part(m, PrimitiveType.Cube, Wood, new Vector3(-0.38f, 0.4f, 0.3f), new Vector3(0.04f, 0.8f, 0.04f), level: 2);
            Part(m, PrimitiveType.Cube, Wood, new Vector3(-0.2f, 0.78f, 0.22f), new Vector3(0.4f, 0.03f, 0.03f), new Vector3(0, 25, -12), level: 2);
            Part(m, PrimitiveType.Cube, Dark, new Vector3(-0.03f, 0.66f, 0.15f), new Vector3(0.008f, 0.22f, 0.008f), level: 2);
            Part(m, PrimitiveType.Cube, DarkWood, new Vector3(0.42f, 0.09f, -0.36f), new Vector3(0.22f, 0.1f, 0.14f), level: 3);
            Part(m, PrimitiveType.Cube, deep, new Vector3(0.42f, 0.16f, -0.36f), new Vector3(0.16f, 0.06f, 0.1f), level: 3);
        }

        // 광산: 언덕 + 갱도 입구(나무 틀) + 권양탑과 바퀴. L2 광차 선로와 광차, L3 두 번째 권양탑.
        private static void Mine(FacilityModel m, Color body)
        {
            Part(m, PrimitiveType.Sphere, body, new Vector3(0, 0, 0.05f), new Vector3(0.95f, 0.75f, 0.85f));
            Part(m, PrimitiveType.Cube, Dark, new Vector3(0, 0.12f, -0.34f), new Vector3(0.22f, 0.24f, 0.14f));
            Part(m, PrimitiveType.Cube, Wood, new Vector3(-0.13f, 0.13f, -0.4f), new Vector3(0.04f, 0.26f, 0.04f));
            Part(m, PrimitiveType.Cube, Wood, new Vector3(0.13f, 0.13f, -0.4f), new Vector3(0.04f, 0.26f, 0.04f));
            Part(m, PrimitiveType.Cube, Wood, new Vector3(0, 0.27f, -0.4f), new Vector3(0.32f, 0.05f, 0.05f));
            Part(m, PrimitiveType.Cube, Wood, new Vector3(0.28f, 0.48f, 0.18f), new Vector3(0.05f, 0.62f, 0.05f));
            Part(m, PrimitiveType.Cylinder, DarkWood, new Vector3(0.28f, 0.8f, 0.18f), new Vector3(0.22f, 0.02f, 0.22f), new Vector3(90, 0, 0));
            Part(m, PrimitiveType.Cube, Dark, new Vector3(-0.05f, 0.01f, -0.6f), new Vector3(0.02f, 0.02f, 0.4f), level: 2);
            Part(m, PrimitiveType.Cube, Dark, new Vector3(0.05f, 0.01f, -0.6f), new Vector3(0.02f, 0.02f, 0.4f), level: 2);
            Part(m, PrimitiveType.Cube, DarkWood, new Vector3(0, 0.07f, -0.66f), new Vector3(0.14f, 0.1f, 0.12f), level: 2);
            Part(m, PrimitiveType.Cube, Wood, new Vector3(-0.3f, 0.42f, 0.25f), new Vector3(0.05f, 0.5f, 0.05f), level: 3);
            Part(m, PrimitiveType.Cylinder, DarkWood, new Vector3(-0.3f, 0.68f, 0.25f), new Vector3(0.18f, 0.02f, 0.18f), new Vector3(90, 0, 0), level: 3);
        }

        // 경매장: 기단 + 금빛 박공지붕 + 앞 기둥 셋 + 깃발. L2 옆 별관.
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
            Part(m, PrimitiveType.Cube, Cream, new Vector3(-0.52f, 0.13f, 0.12f), new Vector3(0.26f, 0.26f, 0.4f), level: 2);
        }

        // 병원: 흰 건물 + 지붕의 붉은 십자 + 문. L2 옆 병동, L3 위층.
        private static void Hospital(FacilityModel m, Color body)
        {
            Part(m, PrimitiveType.Cube, body, new Vector3(0, 0.2f, 0), new Vector3(0.72f, 0.4f, 0.6f));
            Part(m, PrimitiveType.Cube, Red, new Vector3(0, 0.41f, 0), new Vector3(0.38f, 0.02f, 0.11f));
            Part(m, PrimitiveType.Cube, Red, new Vector3(0, 0.41f, 0), new Vector3(0.11f, 0.02f, 0.38f));
            Part(m, PrimitiveType.Cube, Dark, new Vector3(0, 0.11f, -0.305f), new Vector3(0.15f, 0.22f, 0.02f));
            Part(m, PrimitiveType.Cube, body, new Vector3(0.5f, 0.14f, 0.1f), new Vector3(0.3f, 0.28f, 0.42f), level: 2);
            Part(m, PrimitiveType.Cube, body, new Vector3(-0.15f, 0.5f, 0.12f), new Vector3(0.36f, 0.2f, 0.3f), level: 3);
        }

        // 물탱크: 나무 다리 넷 위의 둥근 통. L2 옆의 작은 통, L3 위 사다리.
        private static void WaterTank(FacilityModel m, Color body)
        {
            foreach (float x in new[] { -0.22f, 0.22f })
                foreach (float z in new[] { -0.22f, 0.22f })
                    Part(m, PrimitiveType.Cube, Wood, new Vector3(x, 0.16f, z), new Vector3(0.05f, 0.32f, 0.05f));
            Part(m, PrimitiveType.Cylinder, body, new Vector3(0, 0.5f, 0), new Vector3(0.62f, 0.18f, 0.62f));
            Part(m, PrimitiveType.Cylinder, Color.Lerp(body, Dark, 0.3f), new Vector3(0, 0.69f, 0), new Vector3(0.42f, 0.01f, 0.42f));
            Part(m, PrimitiveType.Cylinder, body, new Vector3(0.48f, 0.15f, -0.3f), new Vector3(0.24f, 0.15f, 0.24f), level: 2);
            Part(m, PrimitiveType.Cube, DarkWood, new Vector3(0, 0.4f, -0.33f), new Vector3(0.12f, 0.8f, 0.02f), new Vector3(-8, 0, 0), level: 3);
        }

        // 창고: 긴 건물 + 어두운 박공지붕 + 큰 문. L2 문 앞 나무 상자, L3 옆 차양.
        private static void Warehouse(FacilityModel m, Color body)
        {
            Part(m, PrimitiveType.Cube, body, new Vector3(0, 0.18f, 0), new Vector3(0.92f, 0.36f, 0.56f));
            Part(m, PrimitiveType.Cube, DarkWood, new Vector3(0, 0.36f, 0), new Vector3(0.98f, 0.4f, 0.4f), new Vector3(45, 0, 0));
            Part(m, PrimitiveType.Cube, Dark, new Vector3(0, 0.12f, -0.285f), new Vector3(0.26f, 0.24f, 0.02f));
            Part(m, PrimitiveType.Cube, Wood, new Vector3(0.32f, 0.06f, -0.42f), new Vector3(0.12f, 0.12f, 0.12f), level: 2);
            Part(m, PrimitiveType.Cube, Wood, new Vector3(0.44f, 0.06f, -0.4f), new Vector3(0.1f, 0.1f, 0.1f), new Vector3(0, 20, 0), level: 2);
            Part(m, PrimitiveType.Cube, Wood, new Vector3(0.36f, 0.17f, -0.41f), new Vector3(0.1f, 0.1f, 0.1f), new Vector3(0, -10, 0), level: 2);
            Part(m, PrimitiveType.Cube, DarkWood, new Vector3(-0.56f, 0.22f, 0), new Vector3(0.22f, 0.02f, 0.5f), new Vector3(0, 0, -15), level: 3);
            Part(m, PrimitiveType.Cube, Wood, new Vector3(-0.64f, 0.1f, -0.22f), new Vector3(0.03f, 0.2f, 0.03f), level: 3);
            Part(m, PrimitiveType.Cube, Wood, new Vector3(-0.64f, 0.1f, 0.22f), new Vector3(0.03f, 0.2f, 0.03f), level: 3);
        }
    }
}
