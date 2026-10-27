using System.Collections.Generic;
using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 임시 그래픽용 도우미: 색별 머티리얼 캐시와 납작한 육각형 메시.
    /// 정식 모델·머티리얼이 생기면 이 파일을 쓰는 곳(SettlementView, BuildInputController)만 바꾸면 된다.
    /// </summary>
    public static class WorldVisuals
    {
        // 키는 8비트 색: 개량 깜빡임·수로 물결처럼 매 프레임 바뀌는 색도 머티리얼 수가 일정 이상 늘지 않는다.
        private static readonly Dictionary<Color32, Material> Materials = new Dictionary<Color32, Material>();
        private static Shader _shader;
        private static Mesh _hexMesh;

        /// <summary>단색 머티리얼(URP Lit). 같은 색은 하나를 공유한다.</summary>
        public static Material Solid(Color color)
        {
            Color32 key = color;
            if (Materials.TryGetValue(key, out Material m) && m != null) return m;
            color = key;
            if (_shader == null)
            {
                _shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            }
            m = new Material(_shader) { color = color, name = "Solid " + ColorUtility.ToHtmlStringRGB(color) };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
            Materials[key] = m;
            return m;
        }

        /// <summary>반지름 1(중심~꼭짓점), pointy-top, 높이 0의 육각형. 크기는 transform.localScale로 맞춘다.</summary>
        public static Mesh HexMesh
        {
            get
            {
                if (_hexMesh != null) return _hexMesh;
                var vertices = new Vector3[7];
                var triangles = new int[18];
                vertices[0] = Vector3.zero;
                for (int i = 0; i < 6; i++)
                {
                    float angle = Mathf.PI / 3f * i; // 북쪽에서 시계 방향 (HexLayout과 같음)
                    vertices[i + 1] = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                    triangles[i * 3] = 0;
                    triangles[i * 3 + 1] = i + 1;
                    triangles[i * 3 + 2] = (i + 1) % 6 + 1;
                }
                // 에디터(맵 스케치 Gizmo)에서도 쓰므로 씬에 저장되지 않게 한다.
                _hexMesh = new Mesh { name = "Hex", vertices = vertices, triangles = triangles, hideFlags = HideFlags.HideAndDontSave };
                _hexMesh.RecalculateNormals();
                _hexMesh.RecalculateBounds();
                return _hexMesh;
            }
        }

        /// <summary>충돌체 없는 기본 도형.</summary>
        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = Solid(color);
            return go;
        }

        public static GameObject HexTile(string name, Transform parent, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = HexMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Solid(color);
            return go;
        }

        private static readonly Dictionary<Mesh, Mesh> WhiteMeshes = new Dictionary<Mesh, Mesh>();

        /// <summary>
        /// 조명을 받지 않는 반투명 가능 머티리얼(2026-10-27 이슈 #7·#13). `Sprites/Default`는 그래픽 설정의 '항상 포함' 셰이더라 빌드에서 빠지지 않는다.
        /// 색이 1보다 크면(HDR) 블룸으로 빛나 보인다. 매번 새 머티리얼이므로 색을 바꿔 쓰는 쪽이 가지고 있는다.
        /// </summary>
        public static Material Unlit(string name, Color color) =>
            new Material(Shader.Find("Sprites/Default")) { name = name, color = color };

        /// <summary>`Sprites/Default`는 정점 색 × 재질 색이라, 정점 색(흰색)을 넣은 메시 사본을 쓴다(원본별로 하나).</summary>
        public static Mesh WithWhiteVertexColors(Mesh source)
        {
            if (WhiteMeshes.TryGetValue(source, out Mesh m) && m != null) return m;
            m = Object.Instantiate(source);
            m.name = source.name + " (white)";
            var white = new Color[m.vertexCount];
            for (int i = 0; i < white.Length; i++) white[i] = Color.white;
            m.colors = white;
            WhiteMeshes[source] = m;
            return m;
        }

        public static void SetColor(GameObject go, Color color)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = Solid(color);
        }
    }
}
