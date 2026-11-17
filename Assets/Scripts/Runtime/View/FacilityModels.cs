using System;
using System.Collections.Generic;
using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>부품의 쓰임: 몸체(레벨로 커지고 공사 진행률로 솟음), 비계(공사·개량 중에만 보임), 등불(상태 색을 입히지 않음).</summary>
    public enum FacilityPartRole { Body, Scaffold, Lantern }

    /// <summary>
    /// 시설 하나의 표시 오브젝트(여러 부품). 부품마다 원래 색(텍스처 부품은 흰색 = 텍스처 그대로)을 기억해 두고,
    /// 상태 색(공사·폐쇄·개량·철거·시듦)을 모든 부품에 함께 입힌다. 텍스처 부품은 그 색을 텍스처에 곱하고, 폐쇄면 채도 낮춘 텍스처로 바꾼다.
    /// 루트는 타일 크기로만 늘리고, 레벨·공사 진행률에 따른 크기 변화는 <see cref="Body"/> 아래 부품에만 건다(비계·등불은 그대로).
    /// </summary>
    public sealed class FacilityModel : MonoBehaviour
    {
        private sealed class PartInfo
        {
            public MeshRenderer Renderer;
            public Color Color;
            public FacilityPartRole Role;
            public Material Texture, Muted; // 텍스처 부품이면 원본·채도 낮춘 머티리얼, 단색 부품이면 null
        }

        private readonly List<PartInfo> _parts = new List<PartInfo>();

        /// <summary>몸체 부품의 부모. SettlementView가 localScale로 레벨 크기와 공사 높이를 바꾼다.</summary>
        public Transform Body { get; private set; }

        public int PartCount => _parts.Count;
        public int VisiblePartCount { get { int n = 0; foreach (PartInfo p in _parts) if (p.Renderer.gameObject.activeSelf) n++; return n; } }
        public bool ScaffoldVisible { get; private set; }

        /// <summary>구운 건물 메시(텍스처)로 그린 시설인지. 아니면 메시가 없는 종류의 상자.</summary>
        public bool Textured { get; private set; }

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

        /// <summary>단색 부품을 등록한다. 비계는 처음에 숨긴다.</summary>
        public void Add(GameObject part, Color color, FacilityPartRole role = FacilityPartRole.Body)
        {
            _parts.Add(new PartInfo { Renderer = part.GetComponent<MeshRenderer>(), Color = color, Role = role });
            if (role == FacilityPartRole.Scaffold) part.SetActive(false);
        }

        /// <summary>텍스처 부품(몸체)을 등록한다. 상태 색은 <paramref name="material"/>(채도를 낮출 때는 <paramref name="muted"/>)에 곱해진다.</summary>
        public void AddTextured(GameObject part, Material material, Material muted)
        {
            _parts.Add(new PartInfo { Renderer = part.GetComponent<MeshRenderer>(), Color = Color.white, Role = FacilityPartRole.Body, Texture = material, Muted = muted ?? material });
            Textured = true;
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
        /// 등불(결정 LT1~LT3): <paramref name="lit"/>이면 이 시설 몫의 불빛 머티리얼(<paramref name="litTemplate"/> 사본)을
        /// <paramref name="litColor"/>로 칠하고 점광원 <paramref name="lightIntensity"/>, 아니면(폐쇄) 꺼진 유리 머티리얼과 광원 끔.
        /// 시설마다 숨쉬는 밝기가 달라서(결정 LT5) 불빛 머티리얼은 시설마다 따로 둔다.
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

        /// <summary>
        /// 부품마다 <paramref name="tint"/>(원래 색 → 표시 색)를 적용한다. 비계는 상태 색 없이, 등불은 건드리지 않는다.
        /// <paramref name="muted"/>이면(폐쇄) 채도를 <see cref="FacilityModels.MutedSaturation"/>로 낮춘다: 텍스처 부품은 채도 낮춘 텍스처로 바꾸고, 단색 부품은 색을 회색 쪽으로.
        /// </summary>
        public void Paint(Func<Color, Color> tint, bool muted = false)
        {
            foreach (PartInfo p in _parts)
            {
                if (!p.Renderer.gameObject.activeSelf || p.Role == FacilityPartRole.Lantern) continue;
                if (p.Role == FacilityPartRole.Scaffold) { p.Renderer.sharedMaterial = WorldVisuals.Solid(p.Color); continue; }
                if (p.Texture != null) { p.Renderer.sharedMaterial = WorldVisuals.Tinted(muted ? p.Muted : p.Texture, tint(p.Color)); continue; }
                Color c = tint(p.Color);
                if (muted) c = Color.Lerp(new Color(c.grayscale, c.grayscale, c.grayscale), c, FacilityModels.MutedSaturation);
                p.Renderer.sharedMaterial = WorldVisuals.Solid(c);
            }
        }
    }

    /// <summary>
    /// 시설 외형. 몸체는 <c>Resources/Buildings/&lt;시설 id&gt;</c>의 구운 메시 + <c>BuildingAtlas</c> 텍스처 머티리얼
    /// (원본 FBX를 자르는 법은 에디터의 <c>BuildingModelBaker</c>). 메시가 없는 종류는 몸체 색 상자.
    /// 비계·등불은 모든 시설 공통의 기본 도형이다.
    /// 좌표는 타일 반지름 1 기준(바닥 y = 0)이고, <see cref="SettlementView"/>가 루트를 타일 크기로, 몸체를 레벨·공사 진행률로 늘린다.
    /// 입구는 −z(카메라 기본 방향에서 화면 아래쪽)를 본다.
    /// </summary>
    public static class FacilityModels
    {
        public const string MeshFolder = "Buildings/";
        public const string AtlasPath = "Buildings/BuildingAtlas";
        public const string MutedAtlasPath = "Buildings/BuildingAtlasMuted";

        /// <summary>폐쇄 시설에 남기는 채도(0 = 회색, 1 = 원래 색). 텍스처 사본은 굽을 때 이 값으로 만든다(바꾸면 다시 굽기).</summary>
        public const float MutedSaturation = 0.35f;

        private static readonly Color DarkWood = new Color(0.32f, 0.22f, 0.14f);
        private static readonly Color Cream = new Color(0.94f, 0.9f, 0.8f);
        private static readonly Color Scaffold = new Color(0.78f, 0.62f, 0.38f);

        private static Material _atlas, _mutedAtlas;
        private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();

        /// <summary>그 종류의 구운 메시(없으면 null). 한 번 찾은 결과를 기억한다.</summary>
        public static Mesh MeshOf(string kind)
        {
            if (!Meshes.TryGetValue(kind, out Mesh mesh))
            {
                mesh = Resources.Load<Mesh>(MeshFolder + kind);
                Meshes[kind] = mesh;
            }
            return mesh;
        }

        public static FacilityModel Build(string kind, Transform parent, Color body)
        {
            var root = new GameObject(kind);
            root.transform.SetParent(parent, false);
            FacilityModel m = root.AddComponent<FacilityModel>();
            Mesh mesh = MeshOf(kind);
            if (_atlas == null) _atlas = Resources.Load<Material>(AtlasPath);
            if (_mutedAtlas == null) _mutedAtlas = Resources.Load<Material>(MutedAtlasPath);
            if (mesh != null && _atlas != null)
            {
                var go = new GameObject("Model");
                go.transform.SetParent(m.ParentFor(FacilityPartRole.Body), false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = _atlas;
                m.AddTextured(go, _atlas, _mutedAtlas);
            }
            else
            {
                Part(m, PrimitiveType.Cube, body, new Vector3(0, 0.2f, 0), new Vector3(0.6f, 0.4f, 0.6f));
            }
            Scaffolding(m, 0.62f, 0.75f);
            Lantern(m, new Vector3(-0.6f, 0, -0.6f));
            return m;
        }

        /// <summary>단색 부품 하나. 원기둥·구의 scale.y는 Unity 기본 도형 기준(원기둥 높이 = 2 × scale.y).</summary>
        private static void Part(FacilityModel m, PrimitiveType type, Color color, Vector3 position, Vector3 scale,
            FacilityPartRole role = FacilityPartRole.Body)
        {
            GameObject go = WorldVisuals.Primitive(type, type.ToString(), m.ParentFor(role), color);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            m.Add(go, color, role);
        }

        // 등불(결정 LT1): 앞 왼쪽 귀퉁이의 나무 기둥 + 가로대 + 매달린 불빛 + 바닥을 비추는 약한 점광원.
        // 불빛은 조명을 받지 않는 머티리얼이라 밤에도 밝게 보이고, 밝기·색은 SettlementView가 시각에 따라 바꾼다. 몸체 크기와 상관없이 그대로.
        private static void Lantern(FacilityModel m, Vector3 at)
        {
            Part(m, PrimitiveType.Cube, DarkWood, at + new Vector3(0, 0.26f, 0), new Vector3(0.035f, 0.52f, 0.035f), FacilityPartRole.Lantern);
            Part(m, PrimitiveType.Cube, DarkWood, at + new Vector3(0.05f, 0.51f, 0), new Vector3(0.12f, 0.025f, 0.025f), FacilityPartRole.Lantern);
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

        // 공사장 비계: 네 귀퉁이 기둥 + 위·가운데 가로대. 공사·개량 중에만 보인다(결정 SG1).
        private static void Scaffolding(FacilityModel m, float r, float height)
        {
            foreach (float x in new[] { -r, r })
                foreach (float z in new[] { -r, r })
                    Part(m, PrimitiveType.Cube, Scaffold, new Vector3(x, height * 0.5f, z), new Vector3(0.035f, height, 0.035f), FacilityPartRole.Scaffold);
            foreach (float y in new[] { height * 0.5f, height })
            {
                Part(m, PrimitiveType.Cube, Scaffold, new Vector3(0, y, -r), new Vector3(2 * r, 0.025f, 0.025f), FacilityPartRole.Scaffold);
                Part(m, PrimitiveType.Cube, Scaffold, new Vector3(0, y, r), new Vector3(2 * r, 0.025f, 0.025f), FacilityPartRole.Scaffold);
                Part(m, PrimitiveType.Cube, Scaffold, new Vector3(-r, y, 0), new Vector3(0.025f, 0.025f, 2 * r), FacilityPartRole.Scaffold);
                Part(m, PrimitiveType.Cube, Scaffold, new Vector3(r, y, 0), new Vector3(0.025f, 0.025f, 2 * r), FacilityPartRole.Scaffold);
            }
        }
    }
}
