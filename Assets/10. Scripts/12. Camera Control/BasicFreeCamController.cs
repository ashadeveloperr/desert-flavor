using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study3D.CameraControl
{
    // 1인칭 카메라와 조작은 같음.
    // 이동을 할때 y값의 변화가 있습니다.
    
    public class BasicFreeCamController : MonoBehaviour
    {
        [Header("Settings")]   
        [SerializeField] private float moveSpeed;
        [SerializeField] private float runSpeed;
        [SerializeField] private float horizontalSensitivity = 1.0f;
        [SerializeField] private float verticalSensitivity = 1.0f;
        
        [SerializeField] private float pitchAngleMin;
        [SerializeField] private float pitchAngleMax;
        
        private float angleX = 0.0f;
        private float angleY = 0.0f;
        
        private void Start()
        {
            LockCursor();
        }

        private void Update()
        {
            UpdateRotation();
            UpdatePosition();
        }
        
        private void UpdateRotation()
        {
            // 마우스의 이동량을 가지고와서 scale로 크기를 변경해 줍니다.
            Vector2 delta = SimpleInput.GetMouseDelta();

            angleY += delta.x * horizontalSensitivity;
            angleX -= delta.y * verticalSensitivity;
            angleX = Mathf.Clamp(angleX, pitchAngleMin,  pitchAngleMax);
            transform.localRotation = Quaternion.Euler(angleX,angleY,0);
        }
        
        private void UpdatePosition()
        {
            // inputAxis.x : 좌우
            // inputAxis.t : 전방 후방
            // 입력벡터를
            Vector2 inputAxis = SimpleInput.GetMoveAxisRaw();
            // 3차원 좌표계의 이동 방향 벡터로 바꿔주고
            Vector3 forward = transform.forward * inputAxis.y;
            Vector3 right = transform.right * inputAxis.x;
            Vector3 moveDir = (forward + right).normalized;
            // 크기(속도)를 결정해 줍니다.
            float speed = SimpleInput.GetKey(Key.LeftShift) ? runSpeed : moveSpeed;
            
            // 방향과 크기를 서로 곱하고 프레임당 시간을 다시 곱해주면
            // 현재 프레임에 이동할 이동량을 가지고 있는 벡터가 됩니다.
            Vector3 moveVector = moveDir * speed * Time.deltaTime;
            transform.position += moveVector;
        }

        private void LockCursor()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }
}