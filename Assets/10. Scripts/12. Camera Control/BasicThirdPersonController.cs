using System;
using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study3D.CameraControl
{
    public class BasicThirdPersonController : MonoBehaviour
    {
        [Header("Settings")]   
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float runSpeed = 7f;
        [SerializeField] private float horizontalSensitivity = 1.0f;
        [SerializeField] private float verticalSensitivity = 1.0f;
        
        [SerializeField] private float pitchAngleMin;
        [SerializeField] private float pitchAngleMax;
        
        [Header("Ref")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float distance = 4f; // radius. 공전 궤도의 크기
        
        // 입력 회전량들을 모아둘겁니다
        private float angleX = 0.0f;
        private float angleY = 0.0f;

        private void Start()
        {
            LockCursor();
        }

        private void Update()
        {
            UpdatePosition();
            UpdateAngle();
        }

        private void LateUpdate()
        {
            UpdateCameraRotation();
        }

        private void UpdateAngle()
        {
            Vector2 delta = SimpleInput.GetMouseDelta();
            angleX -= delta.y * verticalSensitivity;
            angleY += delta.x * horizontalSensitivity;
            angleX = Mathf.Clamp(angleX, pitchAngleMin, pitchAngleMax);
        }
        
        private void UpdatePosition()
        {
            Vector2 inputAxis = SimpleInput.GetMoveAxisRaw();
            if (inputAxis.sqrMagnitude < 1) return;
            
            // inputAxis를 3차원 입력 벡터로 바꿔줍니다.
            Vector3 inputVector = new Vector3(inputAxis.x, 0f, inputAxis.y);
            
            // angleY의 값을 기준으로 회전량을 생성해 줍니다.
            // pivot의 로컬 기준이 됩니다. 카메라의 좌우 각도가 담겨있음
            Quaternion yaw = Quaternion.Euler(0f, angleY, 0f);
            
            // inputVector를 yaw만큼 회전시킨 이동 벡터를 생성합니다.            
            Vector3 moveVector = (yaw * inputVector).normalized;
            // 크기(속도)를 결정해 줍니다.
            float speed = SimpleInput.GetKey(Key.LeftShift) ? runSpeed : moveSpeed;
            // 내가 이번 프레임에 이동해야할 월드 벡터가 나옵니다.
            transform.position += moveVector * (speed * Time.deltaTime);

            // 몸과 루트의 회전적용합니다
            Quaternion targetRotation = Quaternion.LookRotation(moveVector);
            transform.rotation = targetRotation;
        }

        private void UpdateCameraRotation()
        {
            cameraPivot.rotation = Quaternion.Euler(angleX, angleY, 0f);
            cameraTransform.localPosition = new Vector3(0f, 0f, -distance);
        }
        
        private void LockCursor()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }
}