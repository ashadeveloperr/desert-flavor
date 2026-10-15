using System;
using UnityEngine;

namespace Study3D.Terrains
{
    // 터레인에 붙혀서 사용합니다.
    // SnapToTerrain이 붙어 있는 오브젝트의 자식 오브젝트들의 y값을
    // 현재 Terrain의 Height값으로 변경합니다.
    public class SnapToTerrain : MonoBehaviour
    {
        private Terrain terrain;

        private void Awake()
        {
            // 터레인 정보를 가져오는법
            // terrain = Terrain.activeTerrain; 
            // Terrain.activeTerrain : 활성화된 Terrain 객체를 가져옵니다. 씬이 실행되어 있어야함
            // Terrain.activeTerrains : 터레인이 여러개일 경우에 사용합니다
            
            terrain = GetComponentInParent<Terrain>();
        }

        [ContextMenu("Snap to terrain")]
        public void SnapChildren()
        {
            // 자식 오브젝트들의 y값을 터레인의 height로 조절하려고 합니다.
            // - 자식 오브젝트들
            // - 터레인과 터레인의 특정 위치의 y값

            terrain = Terrain.activeTerrain;
            
            // GetPosition()
            // : 현재 터레인이 붙어있는 transform 컴포넌트의 위치와
            // 본인의 Height값을 고려하여 터레인의 기본 위치를 반환합니다.
            // (터레인의 월드 위치)
            float baseY = terrain.GetPosition().y;

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                Vector3 childPosition = child.position;
                childPosition.y = baseY + terrain.SampleHeight(childPosition);
                // SampleHeight(world 위치 좌표)
                // : 매개변수로 전달된 위치좌표로
                // 터레인의 해당 위치 height값을 추정하여 반환합니다.
                // (get이 아닌 이유는 보간해서 추정하기 때문)
                child.position = childPosition;
            }
        }
    }

}


