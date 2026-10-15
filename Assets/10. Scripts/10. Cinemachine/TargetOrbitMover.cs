using Study.Utilities;
using Study3D.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study3D.Cinematic
{
    // 대상을 원 궤도로 날립니다.
    // - 10.시네머신의 01.씬에서 카메라가 추적하는 대상입니다.
    // - Space : 멈추거나 다시 움직이기
    
    public class TargetOrbitMover : MonoBehaviour
    {
        [Header("Ref")] 
        public Spinner spinner;
        
        [Header("Settings")] 
        public float waveAmp = 0.5f; // 진폭
        public float wavePeriod = 0.5f; // 주기

        private float wavePhase;
        private bool moving = true; 
        
        private void Start()
        {
            wavePhase = 0f;
            moving = true;
            Calculate();
        }

        // Update is called once per frame
        private void Update()
        {
            if(SimpleInput.GetKeyDown(Key.Space))
            {
                moving = !moving;
                spinner.enabled = moving;
            }
            if (moving == false) return;

            wavePhase += Time.deltaTime / wavePeriod;
            Calculate();
        }

        // 위치와 회전을 매 프레임 새로 계산해서 transform.position에 대입합니다.
        private void Calculate()
        {
            float currentY = Mathf.Sin(wavePhase * Mathf.PI * 2f) * waveAmp;
            Vector3 currentPos = transform.localPosition;
            currentPos.y = currentY;
            transform.localPosition = currentPos;
        }
    }

}

