using System;
using UnityEngine;

namespace Study3D.SolarSystem
{
    // 공전하는 거리를 반지름으로 원 궤도를 그리는 컴포넌트 입니다.
    [RequireComponent(typeof(LineRenderer))]
    public class OrbitPathDrawer : MonoBehaviour
    {
        [Header("Settings")] 
        public Transform orbitingChild; // 궤도를 돌고 있는 행성의 Transform
        public int segment = 96; // segment갯수만큼 등분
        public float width = 0.05f; // lineRenderer의 선 두께

        private void Start()
        {
            Draw();
        }

        [ContextMenu("OrbitPathDraw")]
        public void Draw()
        {
            if (orbitingChild == null) return;
            LineRenderer lineRenderer = GetComponent<LineRenderer>();
            // 행성의 위치를 가져 온다.
            Vector3 local = orbitingChild.localPosition;
            // Distance를 써도 되긴 하지만, 쫌 더 평면을 기반으로 반지름
            // 길이를 재기 위해서 x랑 z만 사용합니다
            float radius = new Vector2(local.x, local.z).magnitude;
            int segmentCount = Mathf.Max(4, segment); // 최소 4개 이상으로 설정

            // 원의 위치 좌표들을 구하기 전에 LineRenderer를 초기화.
            lineRenderer.useWorldSpace = false; // 로컬 좌표로 전환
            lineRenderer.loop = true; // 끝과 끝을 이어줍니다
            lineRenderer.widthMultiplier = width; // 선의 너비를 지정해 줍니다.
            lineRenderer.positionCount = segmentCount;
            
            for (int i = 0; i < segmentCount; ++i)
            {
                // 점이 배치될 하나의 각도를 구해줍니다.
                float angle = i * Mathf.PI * 2f / segmentCount;
                Vector3 point = new Vector3(
                    Mathf.Cos(angle) * radius,
                    local.y,
                    Mathf.Sin(angle) * radius);
                // LineRenderer의 SetPosition 함수를 이용해서 i번째 점의 위치를 기입해 줍니다.
                lineRenderer.SetPosition(i , point);
            }
        }
    }

}
