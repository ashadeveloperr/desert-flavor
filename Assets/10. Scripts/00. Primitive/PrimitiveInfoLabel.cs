using System;
using TMPro;
using UnityEngine;

namespace Study3D.Primitives
{
    public class PrimitiveInfoLabel : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;

        private void Start()
        {
            label = transform.parent.GetComponentInChildren<TMP_Text>();
            
            // 각 기본 도형에 정점과 삼각형이 얼마나 있는지 체크해 봅시다
            // MeshFilter는 Mesh(vertex, edge, face 등등)의 정보를 가지고 있는 클래스 입니다.
            // sharedMesh : 유니티에서 같은 mesh를 반복해서 재사용하게끔 만든 공유 mesh 클래스.
            // (Def랑 비슷한 재사용성을 가지고 있음. 같은 정보를 갖는 객체를 하위 객체가 공유를 했었죠?)
            // mesh : 해당 MeshFilter만 가지고 있는 고유한 mesh 정보라고 생각하시면 됩니다.
            
            Mesh mesh = GetComponent<MeshFilter>().sharedMesh;
            
            // 해당 mesh의 정점 갯수 정보를 가지고 옵니다
            int vertexCount = mesh.vertexCount;
            // 해당 mesh의 삼각형 갯수 정보를 가지고 옵니다
            int triangleCount = mesh.triangles.Length / 3;
            // 해당 mesh의 bounds 크기(aabb로 표현한 영역)
            Vector3 size = mesh.bounds.size;

            string infoText = $"{name}\n" +
                              $"정점 : {vertexCount} | 삼각형 : {triangleCount}\n" +
                              $"메시 크기 : {size.x:0##}x{size.y:0##}x{size.z:0##}";
            // ":0##" 은 float 소숫점 자릿수 보간
            
            label.SetText(infoText);
        }
    }

}

