using System;
using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study3D.MiniGolf
{
    // 오른쪽 마우스 버튼을 드래그 입력을 이용해서 볼을 중심으로 공전하고
    // 휠로 ZoonIn/ZoonOut 하는 역할의 카메라 컴포넌트 입니다.
    public class GolfOrbitCamera : MonoBehaviour
    {
        [Header("Settings")] 
        public float degreesPerPixel = 0.2f; // 1픽셀당 0.2도씩 움직이도록 디폴트값 설정
        public float zoomStep = 0.5f;
        public float minRadius = 1f;
        public float maxRadius = 10f;

        private GolfPlainCamera plainCamera;

        private void Awake()
        {
            plainCamera = GetComponent<GolfPlainCamera>();
        }

        private void Update()
        {
            // 마우스 휠의 이번 프레임 이동량을 가져옵니다.
            float scroll = SimpleInput.GetMouseScrollDeltaRaw();
            float zoomValue = plainCamera.Radius - scroll * zoomStep;
            plainCamera.Radius = Mathf.Clamp(zoomValue, minRadius, maxRadius) ;
            
            if (SimpleInput.GetMouseButton(SimpleInput.MouseButton.Right) == false) return;
            
            // 마우스의 이번 프레임 이동량을 가져 옵니다.
            Vector2 delta = SimpleInput.GetMouseDelta();
            plainCamera.Yaw += delta.x * degreesPerPixel;
            plainCamera.Pitch -= delta.y * degreesPerPixel;
        }
    }
}