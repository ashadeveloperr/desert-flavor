using System;
using UnityEngine;

namespace Study3D.Transforms
{
    public class LookAtTarget : MonoBehaviour
    {
        public Transform target;

        public void LateUpdate()
        {
            transform.LookAt(target);
        }

        // 게임 화면에 디버깅용 출력을 하는 용도의 이벤트 함수. 
        private void OnDrawGizmos()
        {
            // 출력의 색상을 결정하고
            Gizmos.color = Color.yellow;
            // Line을 그려줍니다
            Gizmos.DrawLine(transform.position, target.position);
        }
    }
}