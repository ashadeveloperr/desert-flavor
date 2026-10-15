using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study3D.FpsGame.Player
{
    // PlayerController
    // : 1인칭 이동, 시점, 사격, 재장전 입력과 애니메이션 처리를 담당하는 클래스 입니다;.
    // - Unity의 CharacterController 컴포넌트를 이용
    // - 카메라를 기반으로 모든 처리들을 담당합니다.
    // - WASD => 이동
    // - 마우스 움직임 => 카메라 회전
    // - 마우스 왼쪽 클릭 => 사격
    // - LeftShift : 달리기
    // - Space : 점프
    // - R : 재장전
    
    public partial class PlayerController : MonoBehaviour
    {
        private static readonly int Fire = Animator.StringToHash("Fire");

        [Header("이동")] 
        [SerializeField] private float walkSpeed = 4f;
        [SerializeField] private float runSpeed = 7f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -9.81f;

        [Header("카메라")] 
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float mouseSensitivity = 2f; // 마우스 감도 입니다.
        [SerializeField] private float maxLookAngle = 80f;

        [Header("애니메이터")] 
        [SerializeField] private Animator animator;

        [Header("바닥 확인용")] 
        [SerializeField] private float groundCheckDistance = 0.1f;
        [SerializeField] private LayerMask groundMask;
        
        // 아래는 내부변수들
        private CharacterController controller;
        private Vector3 velocity;
        private float verticalLookAngle;
        private Quaternion initialCameraRotation;
        private float moveX, moveZ; // 키를 따라 -1 ~ 1 사이를 부드럽게 움직이는 로직에 사용되는 축 값

        private void UpdateMovement()
        {
            // 첫번째로는 이동 속도를 결정해줍니다. LeftShift가 true이면 runSpeed 입니다.
            float speed = SimpleInput.GetKey(Key.LeftShift) ? runSpeed : walkSpeed;

            // 이동 연산에 사용할 방향의 축값을 가져옵니다. 가져온 데이터는 한축은
            // -1, 0, 1 의 3가지중 하나가 반환됩니다
            Vector2 inputRaw = SimpleInput.GetMoveAxisRaw();
            moveX = SmoothAxis(moveX, inputRaw.x);
            moveZ = SmoothAxis(moveZ, inputRaw.y);

            // 현재 플레이어의 방향에 맞춰서 moveVector를 계산합니다.
            Vector3 moveVector = (transform.right * moveX) + (transform.forward * moveZ);
            // moveVector의 길이만큼 현재 프레임의 이동량ㄹ이 조정이 됩니다.
            controller.Move(moveVector * (speed * Time.deltaTime));
            
            // CharacterController는 쉬운만큼 까다로워서
            // 여러군데에서 나눠서 호출할 경우 버그를 찾기가 어려울수도 있습니다. 
            // 특히 CharacterController의 Move()함수를 Update, FixedUpdate등을 이용해서
            // 서로 다른 갱신 타이밍에 호출하면 생각지도 못한 에러를 발생시킬수도 있습니다.
            // 보통 바닥 판정이 잘 안된다거나 하는 등의 이슈가 있습니다.
            
            // 아래부터는 중력과 점프 구현
            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);

            // controller의 isGrounded는 마지막 Move 적용 기준이라
            // 마지막 수직이동 바로 뒤에 읽어야 합니다. 위의
            // "서로 다른 갱신 타이밍에 호출하면 생각지도 못한 에러를 발생시킬수도 있습니다."
            // 요 내용입니다.
            bool isGrounded = controller.isGrounded;

            // 땅에 서있는 동안 낙하속도가 계속 쌓이지 않도록 작은 값으로 되돌립니다.
            // 아래 줄이 없으면 점프 하자마 바로 땅으로 떨어질 수 있습니다.
            // -2f 등을 이용해서 점프 느낌을 조절 합니다.
            // 아니면 따로 변수를 주세요
            if (isGrounded && velocity.y < 0f) velocity.y = -2f;

            if (isGrounded && SimpleInput.GetKeyDown(Key.Space))
            {
                // 점프의 최대 높이를 지정해주는게 개발에 쉬움.
                // 힘으로 점프를 설정하면 플레이어가 얼마 뛸 지를 모르니까
                // 등가속 운동 공식을 사용해서 바꿔준거임
                // 아래처럼 점프를 만들면 힘을 써서 최대 높이를 설정해주지 않아도
                // jumpHeight를 입력하는 것만으로 최대 높이의 점프를 설저애줄 수 있음
                // 위치 에너지 = 1/2 * m * v^2
                // m = 계산 안함 = 1
                // v^2 = 2 * 위치에너지
                // v = Sqrt(위치에너지 * 2)
                // 운동에너지 = 등가속운동 = velocity
                velocity.y = Mathf.Sqrt((jumpHeight * -2f * gravity));
            }
        }
        
        // 현재 값을
        private static float SmoothAxis(float current, float target)
        {
            // 아래값은 보간 속도(?)를 의미한다고 생각하시면 됩니다. 댐핑값
            const float AXIS_SPEED = 3f;
            // target이 0이면 정지를 의미하고
            // current * target 음수일 경우에는 반대 방향으로 이동하는 케이스
            // 둘다 current를 0으로 두고 계산합니다.
            // (왼쪽으로 이동하고 있는데 오른쪽으로 이동하라고 명령이 들어오면 0일 기준으로
            // 이동처리를 하라고 처리합니다.)
            if (target != 0f && current * target < 0f) current = 0f;
            // Toward : ~ 향해 이런뜻 
            // MoveTowards(current, target, maxDelta)
            // : current에서 target 목표 지점으로 maxDelta를 초과하지 않게 움직인 양을 반환합니다.
            // ex) current = -0.2f, target : -1, maxDelta = 0.1f
            // -0.3이 반환 됩니다.
            return Mathf.MoveTowards(current, target, AXIS_SPEED * Time.deltaTime);
        }
        
        private void UpdateCameraRotation()
        {
            const float MOUSE_AXIS_SCALE = 0.1f;
            // 현재 프레임의 마우스 이동량을 가져옵니다.
            // MOUSE_AXIS_SCALE을 곱해서 픽셀로 돌려받은 값을 줄여줍니다.
            Vector2 delta = SimpleInput.GetMouseDelta() * MOUSE_AXIS_SCALE;
            float mouseX = delta.x * mouseSensitivity;
            float mouseY = delta.y * mouseSensitivity;
            
            Quaternion yawRotation = Quaternion.Euler(0, mouseX, 0);
            // 이것도 되겠죠?
            //Quaternion yawRotation = Quaternion.AngleAxis(mouseX, Vector3.up);
            transform.rotation *= yawRotation; // y축은 내가 돌아갑니다. 나는 월드 입니다 

            // Pitch값은 누적. verticalLookRotation => verticalLookAngle로 수정
            verticalLookAngle -= mouseY;
            verticalLookAngle = Mathf.Clamp(verticalLookAngle, -maxLookAngle, maxLookAngle);
            cameraTransform.localRotation = Quaternion.Euler(verticalLookAngle, 0f , 0f);
            
            // 아래와 동일합니다.
            // Quaternion pitchRotation = Quaternion.AngleAxis(verticalLookAngle, Vector3.right);
            // cameraTransform.localRotation = initialCameraRotation * pitchRotation;
        }

        // 총발사와 재장전을 담당하는 함수 입니다.
        private void UpdatePlayerInput()
        {
            // 발사 하는 버튼
            if(SimpleInput.GetMouseButton(SimpleInput.MouseButton.Left))
            {
                bool successFire = LocalPlayer.localPlayer.Fire();
                
                // 발사 성공일 경우
                if (successFire)
                {
                    // 해당 animator의 발사동작은 Any State에 들어가므로,
                    // 남은 트리거를 지우고 다시 겁니다.
                    animator.ResetTrigger(Fire);
                    animator.SetTrigger(Fire);
                }
            }

            // 재장전
            if (SimpleInput.GetKeyDown(Key.R)) LocalPlayer.localPlayer.RequestReload();
        }
    }
}