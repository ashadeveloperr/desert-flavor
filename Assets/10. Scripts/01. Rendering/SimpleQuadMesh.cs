using System;
using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study3D.Rendering
{
    public class SimpleQuadMesh : MonoBehaviour
    {
        [Header("크기")] 
        public float width = 2f;
        public float height = 2f;

        [Header("Settings")] public bool flipWinding = false;
        
        private Mesh mesh;
        private bool builtFlip;

        private void Awake()
        {
            // 새로운 Mesh 객체를 생성합니다.
            mesh = new Mesh { name = "SimpleQuad" };
            // 생성한 Mesh 객체의 참조정보(주소)를 MeshFilter에 연결해줍니다.
            // (연결된 Renderer에서 자동으로 mesh정보를 가지고 렌더링을 합니다)
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            Build();
        }

        private void Update()
        {
            if (SimpleInput.GetKeyDown(Key.Space)) flipWinding = !flipWinding;
            if (flipWinding != builtFlip) Build();
        }

        private void Build()
        {
            mesh.Clear();
            
            // 첫번째로 정점(버텍스)을 생성해줍니다.
            // 왼쪽 밑, 오른쪽 밑, 왼쪽 상단, 오른쪽 상단
            Vector3[] vertices =
            {
                new Vector3(-width / 2f, 0, 0f), // Left Bottom
                new Vector3(width / 2f, 0, 0f), // Right Bottom
                new Vector3(-width / 2f, height, 0f), // Left Top
                new Vector3(width / 2f, height, 0f), // Right Top
            };
            
            // 정점생성이 끝났으면 삼각형 정보를 넣어 줍니다.
            int[] triangles = flipWinding  
                ? new[] {0,1,2,2,1,3}  // 뒷면 (앞면은 컬링됨) 
                : new[] {0,2,1,2,3,1}; // 앞면
            
            // 삼각형이 뒤집히면 보이는 면도 반대가 되므로 노말도 함께 뒤집어 줍니다
            Vector3 normalDir = flipWinding ? Vector3.forward : Vector3.back;
            Vector3[] normals = { normalDir, normalDir, normalDir, normalDir};
            
            // Left Bottom
            // Right Bottom
            // Left Top
            // Right Top
            
            Vector2[] uv = new[]
            {
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 0f), // 빨간색이 있는 곳
                new Vector2(0f, 1f),
            };
            
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.RecalculateNormals();

            // builtFlip을 갱신해서 토글이 동작하도록 합니다.
            builtFlip = flipWinding;
        }
        
        
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Gizmos와 Handles 모두 객체의 로컬 좌표계를 기준으로 그리도록 행렬 적용
            Gizmos.matrix = transform.localToWorldMatrix;
            UnityEditor.Handles.matrix = transform.localToWorldMatrix;

            Vector3[] previewVertices =
            {
                new Vector3(-width / 2f, 0, 0f),
                new Vector3(width / 2f, 0, 0f),
                new Vector3(-width / 2f, height, 0f),
                new Vector3(width / 2f, height, 0f),
            };

            Vector3 previewNormal = flipWinding ? Vector3.forward : Vector3.back;

            // 씬 뷰에 표시될 텍스트의 스타일 설정
            GUIStyle labelStyle = new GUIStyle();
            labelStyle.normal.textColor = Color.white;
            labelStyle.fontSize = 14;
            labelStyle.fontStyle = FontStyle.Bold;

            for (int i = 0; i < previewVertices.Length; i++)
            {
                Vector3 vertex = previewVertices[i];

                // 1. 정점(Vertex) 렌더링
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(vertex, 0.05f);

                // 2. 노말(Normal) 렌더링
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(vertex, previewNormal * 0.5f);

                // 3. 인덱스(Index) 텍스트 렌더링
                // 구체와 텍스트가 겹치지 않도록 위치를 살짝 오프셋(Offset) 처리
                Vector3 labelOffset = new Vector3(0.1f, 0.1f, 0f);
                UnityEditor.Handles.Label(vertex + labelOffset, $"[{i}]", labelStyle);
            }
        }
#endif
    }
}