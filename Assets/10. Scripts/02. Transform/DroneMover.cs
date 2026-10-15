using System;
using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Study3D.Transforms
{
    // 키보드로 드론을 이동, 회전 하고 기준 공간을 바꿔서 움직일 수 있는 컴포넌트
    // - Trnaslate
    // - W/S : 앞, 뒤
    // - A/D : 좌, 우
    // - Q/E : 아래, 위
    // - 좌우 화살표 : 회전
    
    public class DroneMover : MonoBehaviour
    {
        [Header("Settings")] 
        public float moveSpeed = 3;
        public float verticalSpeed = 2;
        public float yawSpeed = 90f;
        
        public Space space = Space.Self;

        private void Update()
        {
            Keyboard current = Keyboard.current;
            // 이동 처리
            float x = GetAxis(current.aKey, current.dKey);
            float z = GetAxis(current.sKey, current.wKey);
            float y = GetAxis(current.qKey, current.eKey);

            Vector3 moveVector;
            moveVector.x = x * moveSpeed;
            moveVector.y = y * verticalSpeed;
            moveVector.z = z * moveSpeed;
            
            transform.Translate(moveVector * Time.deltaTime, space);
            // 회전 처리
            float yaw = GetAxis(current.leftArrowKey, current.rightArrowKey);
            transform.Rotate(0f, yaw * yawSpeed * Time.deltaTime, 0f);
        }

        // 두가지의 입력 값을 받아서 하나로 통합하는 함수
        private float GetAxis(KeyControl negative, KeyControl positive)
        {
            // A랑 D가 동시에 눌리면? 입력값 : 0
            // A가 눌리면? 입력값 : -1
            // D가 눌리면? 입력값 : 1
            return (positive.isPressed ? 1f : 0f) - (negative.isPressed ? 1f : 0f);
        }
    }
}

