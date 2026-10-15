using System;
using UnityEngine;

namespace Study3D.ThreeDPhysics
{
    // BumperFeedback
    // : 무언가 부딪히면 충격량과 상대 속도를 이용해서 피드백(팽창하는 표현)을 표현
    // - OnCollisionEnter : 닿는 순간 한번 호출되고, Collision에 충돌 정보가 담겨 옵니다.
    // - 리지드 바디가 없는 벽이나 범퍼 등도 부딪혀 오는 쪽에 리지드바디가 있으면 이벤트가 들어옵니다.
    
    public class BumperFeedback : MonoBehaviour
    {
        [Header("감지 설정")]
        public float minImpulse = 0.5f;
        [Tooltip("이 수치를 기준으로 팽창 비율을 계산합니다 (최대 충격량 기준치)")]
        public float maxExpectedImpulse = 20f; 
        
        [Header("팽창 설정")]
        [Tooltip("약한 충격일 때의 최소 팽창 배율")]
        public float minPulseScale = 1.5f;     
        [Tooltip("강한 충격일 때의 최대 팽창 배율")]
        public float maxPulseScale = 3.0f;     
        public float pulseDuration = 0.2f;

        private Vector3 startScale;
        private float pulseTimer;
        private float targetPulseScale; // 이번 충돌에서 도달할 목표 스케일 배율

        private void Awake()
        {
            startScale = transform.localScale;
            targetPulseScale = minPulseScale;
        }

        private void Update()
        {
            // 펄스(팽창) 애니메이션 처리
            if (pulseTimer <= 0f) return;
            
            pulseTimer -= Time.deltaTime;
            
            // pulseTimer의 값으로 전체 펄스 애니메이션 시간중 얼마나 진행되었는지를 계산
            float t = Mathf.Clamp01(pulseTimer / pulseDuration);
            
            // 동적으로 계산된 targetPulseScale을 향해 팽창
            transform.localScale = startScale * Mathf.Lerp(1f, targetPulseScale, t);
        }

        private void OnCollisionEnter(Collision collision)
        {
            // collision에 접근해서 충격(impulse, vector3)의 량(길이, float)를 가져옵니다.
            float impulse = collision.impulse.magnitude;
            
            // 최소 충격량보다 작다면 종료
            if (impulse < minImpulse) return;

            // 1. 충격량을 0.0 ~ 1.0 사이의 비율(intensity)로 변환
            float intensity = Mathf.Clamp01(impulse / maxExpectedImpulse);
            
            // 2. 충격량 비율에 맞춰 실제 팽창할 스케일 배율을 계산 (minPulseScale ~ maxPulseScale 사이)
            float newCalculatedScale = Mathf.Lerp(minPulseScale, maxPulseScale, intensity);
            
            // 3. 팽창 중 더 강한 충격이 들어오거나, 애니메이션이 끝난 상태라면 목표 스케일 갱신
            if (newCalculatedScale > targetPulseScale || pulseTimer <= 0f)
            {
                targetPulseScale = newCalculatedScale;
            }
            
            // 타이머 초기화로 애니메이션(재)시작
            pulseTimer = pulseDuration;
            
            float speed = collision.relativeVelocity.magnitude;
            Debug.Log($"[Bumper : {gameObject.name}] 발생 충격량 {impulse:F1} (비율: {intensity:P0}), 적용 스케일: {targetPulseScale:F2}배, 상대 속도 {speed:F1}");
        }
    }
}