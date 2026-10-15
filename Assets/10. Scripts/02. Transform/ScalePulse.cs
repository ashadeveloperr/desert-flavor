using System;
using UnityEngine;

namespace Study3D.Transforms
{
    // ScalePulse
    // : 사인(Sin) 곡선을 이용해서 크기를 키웠다가 줄였다가 하는 로직을 짜봅시다
    // - localScale : 부모 기준의 배율. 부모가 커지면 자식도 함께 커집니다.
    // - lossyScale : 배모 배율까지 곱한 월드 기준 크기이고 읽기 전용입니다.
    
    
    public class ScalePulse : MonoBehaviour
    {
        public float amplitude = 0.2f;
        public float pulsePerSecond = 0.5f;

        private Vector3 startScale;

        private void Awake()
        {
            startScale = transform.localScale;
        }

        private void Update()
        {
            float wave = Mathf.Sin(Time.time * pulsePerSecond * Mathf.PI * 2f);
            Debug.Log($"wave : {wave}");
            transform.localScale = startScale * (1f + wave * amplitude);
        }
    }
}