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
        private static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();
        private static Shader _shader;
        private static Mesh _hexMesh;

        /// <summary>단색 머티리얼(URP Lit). 같은 색은 하나를 공유한다.</summary>
        public static Material Solid(Color color)
        {
            if (Materials.TryGetValue(color, out Material m) && m != null) return m;
            if (_shader == null)
            {
                _shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            }
            m = new Material(_shader) { color = color, name = "Solid " + ColorUtility.ToHtmlStringRGB(color) };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
            Materials[color] = m;
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
                _hexMesh = new Mesh { name = "Hex", vertices = vertices, triangles = triangles };
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

        public static void SetColor(GameObject go, Color color)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = Solid(color);
        }
    }
}
