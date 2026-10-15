using System;
using UnityEngine;

namespace Study3D.MiniGolf
{
    public class GolfPlainCamera : MonoBehaviour
    {
        [Header("Target")]
        public Transform target;

        [Header("Settings")] 
        [SerializeField] private float radius; // 반지름 : taget과의 거리가 됩니다. 
        [SerializeField] private float yaw; // y축 회전
        
        [SerializeField] private float pitch; // x축 회전
        [SerializeField] private float minPitch; // x축 회전 최소값
        [SerializeField] private float maxPitch; // x축 회전 최대값
        [SerializeField] private float followSpeed; // target을 따라가는 속도
        [SerializeField] private Vector3 offset;

        private Vector3 trackedPoint;

        public float Yaw 
        { 
            get => yaw;
            set => yaw = Mathf.Repeat(value + 180, 360) - 180;
            // 유니티의 각도 범위(-180 ~ 180)를 맞추기 위해 Repeat 처리 해줍니다.
        }

        public float Pitch
        {
            get => pitch; 
            set => pitch = Mathf.Clamp(value, minPitch, maxPitch);
            // 각도 제한을 위해 Clamp 처리 합니다
        }

        public float Radius
        {
            get => radius;
            set => radius = value;
        }

        private void Start()
        {
            if (target == null)
            {
                Debug.LogError("No target assigned!");
                return;
            }
            trackedPoint = target.position;
        }

        // 모든 Update의 로직이나 값 설정들이 갱신 된 후에
        // 마지막에 카메라의 위치를 갱신하도록 LateUpdate를 사용합니다.
        private void LateUpdate()
        {
            if (target == null) return;
            Vector3 currentTargetPosition = target.position;
            
            trackedPoint = 
                Vector3.Lerp(trackedPoint, currentTargetPosition, followSpeed * Time.deltaTime);
            
            // 자신의 프로퍼티들을 이용해서 회전량(쿼터니언)을 생성하고 3차원 좌표 위치(벡터)를 계산합니다.
            // 결과는 원점에서부터 Quaternion.Euler(pitch, yaw, 0) 만큼 회전하여
            // Vector3(0f, 0f, - radius)만큼 떨어진
            // 위치좌표가 생성(offset)됩니다.
            
            Quaternion camRotation = Quaternion.Euler(pitch, yaw, 0); // 나(카메라)의 현재 회전량
            Vector3 distance = new Vector3(0f, 0f, -radius) + offset; // 궤도의 반지름 + offset
            // 나의 회전량으로부터 궤도의 반지름 만큼 떨어진 궤도 위치좌표
            Vector3 orbitPoint = camRotation * distance ; 
            
            // trackedPoint = ball의 좌표
            // trackedPoint와 offset을 더하면 ball로부터 회전하여 떨어져 있는 위치 좌표가 결과로 나옵니다
            transform.position = trackedPoint + orbitPoint;
            
            // 아래 내용과 LookAt 함수는 동일한 내용이 나옵니다.
            //Vector3 toTarget = (target.position - transform.position).normalized;
            //transform.rotation = Quaternion.LookRotation(toTarget, Vector3.up);
            transform.LookAt(target);
        }
    }
}