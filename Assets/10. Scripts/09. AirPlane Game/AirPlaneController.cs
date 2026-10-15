using System;
using Study.Utilities;
using UnityEngine;
using UnityEngine.Serialization;

namespace Study3D.AirPlaneGame
{
    // 종이 비행기의 입력 처리와 SkinnedMeshRenderer, 리깅, 회전 등을 이용해서
    // 비주얼적인 애니메이션 처리를 담당하는 클래스
    public class AirPlaneController : MonoBehaviour
    {
        public static AirPlaneController LocalAirPlane { get; private set; }
        private Rigidbody rBody;

        # region Unity Event Methods
        
        private void Awake()
        {
            rBody = GetComponent<Rigidbody>();

            // 약식 싱글톤
            if (LocalAirPlane == null) LocalAirPlane = this;
            else Destroy(gameObject);
        }

        private void Update()
        {
            UpdateInput();
            UpdateModelVisual();
        }

        private void FixedUpdate()
        {
            if (rBody.useGravity) return;
            UpdateRotation();
            UpdateVelocity();
        }
        
        #endregion
        
        # region Main Study

        [Header("RotationAndMove Settings")] 
        [SerializeField] private float pitchAngle = 1.0f;
        [SerializeField] private float yawAngle = 1.0f;
        
        [SerializeField] private float pitchSpeed = 1.0f;
        [SerializeField] private float yawSpeed = 1.0f;
        [SerializeField] private float moveSpeed = 1.0f;
        
        // yawSpeed가 있는 이유?
        // airplane이 보기에는 pitch, roll 회전으로 구현되어 보이지만
        // 실제 게임에 적용되는 회전 축은 pitch, yaw로 구현해야 자연스러워짐

        private Vector2 inputAxis;
        
        private void UpdateInput()
        {
            inputAxis = SimpleInput.GetMoveAxisRaw();
        }

        // 비행기 자체의 회전을 계산해줍니다. (pitch, yaw)
        private void UpdateRotation()
        {
            // pitch와 yaw 각각의 로테이션을 생성해줍니다.
            float pAngle = inputAxis.y * pitchAngle * pitchSpeed * Time.deltaTime;
            float yAngle = inputAxis.x * yawAngle * yawSpeed * Time.deltaTime;
            Quaternion pitchRotation = Quaternion.AngleAxis(pAngle, transform.right);
            Quaternion yawRotation = Quaternion.AngleAxis(yAngle, Vector3.up);
            
            // 아래처럼 Rotate를 사용해도 될것 같습니다.
            // Vector3 pAngle = new Vector3(inputAxis.y, 0, 0) * (pitchAngle * pitchSpeed * Time.deltaTime);
            // Vector3 yAngle = new Vector3(0, inputAxis.x, 0) * (yawAngle * yawSpeed * Time.deltaTime);
            //transform.Rotate(transform.right * pAngle, Space.Self);
            //transform.Rotate(Vector3.up * yAngle, Space.World);

            // 로테이션은 계산 순서에 따라 다르게 적용 됩니다.
            transform.rotation = yawRotation * pitchRotation * transform.rotation;
            // 로테이션은 우측에서 좌측으로 계산되어 적용된다고 생각하시면 됩니다.
            // yawRotation : 월드 축(Vector3.up)을 기반으로 한 회전량
            // pitchRotation : 내 로컬축(transform.right)을 기반으로 한 회전량
            // transform.rotation : 내 현재 회전량
            
            // 오른쪽부터 나의 회전이 모두 끝나고 그 결과로 yawRotation을 적용해야 합니다.
            // 모든것은 누적으로 반영되기 때문에 여러 곳에서 특정물체의 회전요청을 하면
            // 이상한 회전이 나올 수도 있습니다.
        }

        // 선속도를 기반으로 비행기의 이동을 결정합니다. 
        private void UpdateVelocity()
        {
            // UpdateRotation에서 수정된 transform.forward를 사용해서 선속도 벡터를 만들어 줍니다.
            Vector3 applyVelocity = transform.forward * (moveSpeed * Time.fixedDeltaTime);
            // rigidBody의 선속도에 대입해줍니다.
            //rBody.AddForce(applyVelocity, ForceMode.VelocityChange);
            rBody.linearVelocity = applyVelocity;
            
            // 플레이어가 조종하는(?) 녀석은 AddForce 함수를 이용하는것보다
            // 그냥 대입하는게 자연스러움.
            // ( 왜냐면 player && isKinematic(true) 인 개체는 물리엔진의 처리에서 벗어나 있는게...
            // 쉬울것 같은데?)
        }

        //---------------------
        
        [Header("Model Settings")]
        [SerializeField] private Transform modelTransform;
        [SerializeField] private float maxRollAngle = 60f;
        [SerializeField] [Range(0f, 1f)] private float rollSpeedDamping = 1.0f;

        // 날개 처리는 BoneFlap.cs 에 더 쉬운버전으로 있습니다.
        [Header("Wing Settings")] 
        [SerializeField] private Transform leftWingTransform;
        [SerializeField] private Transform rightWingTransform;
        [SerializeField] private float maxWingAngle = 60.0f;
        [SerializeField] [Range(0f, 1f)] private float wingRotationDamping = 0.1f;
        
        // 비행기 모델의 비주얼적인 처리를 한곳에 모아서 연산해 줍니다.
        private void UpdateModelVisual()
        {
            // 비행기의 Roll처리와 날개 애니메이션을 만들어 봅시다.
            // 1. 몸체의 Roll 회전 적용
            // - 왼쪽입력을 할경우 : maxRollAngle,
            // - 오른쪽 입력의 경우 : -maxRollAngle
            // - 입력 없을 경우 : 0.0f;
            
            // 입력을 판정해서 이번 실행동안 목표 maxRollAngle을 구해줍니다.
            float targetAngle = 0.0f;
            if (inputAxis.x < 0) targetAngle = maxRollAngle;
            else if (inputAxis.x > 0) targetAngle = -maxRollAngle;
            
            // targetAngle을 적용한 목표 roll 회전량을 생성해준다
            Quaternion targetRollRotation = Quaternion.Euler(0f,0f, targetAngle);
            
            Quaternion applyRoll = Quaternion.Lerp(
                    modelTransform.localRotation, 
                    targetRollRotation, 
                    rollSpeedDamping);

            modelTransform.localRotation = applyRoll;
            
            // 2. 날개 애니메이션 처리
            if (inputAxis.x <= 0 || inputAxis.y < 0)
            {
                leftWingTransform.localRotation =
                    CalculateWingRotation(leftWingTransform, true);
            }

            if (inputAxis.x >= 0 || inputAxis.y < 0)
            {
                rightWingTransform.localRotation =
                    CalculateWingRotation(rightWingTransform, false);
            }

            return;
            
            // 왼쪽 날개나 오른쪽 날개나 처리는 동일하기 때문에
            // 함수를 선언하여 처리해줍니다.
            Quaternion CalculateWingRotation(Transform wingTransform, bool isLeft)
            {
                // 입력이 있으면 wingTargetAngle에 maxWingAngle을 대입해준다.
                float wingTargetAngle =
                    (Mathf.Abs(inputAxis.x) > 0 || Mathf.Abs(inputAxis.y) > 0) ? maxWingAngle : 0.0f;
                
                Quaternion currentRotation = wingTransform.localRotation;

                Quaternion targetRotation = Quaternion.Euler(
                    currentRotation.eulerAngles.x,
                    isLeft ? wingTargetAngle : -wingTargetAngle,
                    currentRotation.eulerAngles.z);

                currentRotation = Quaternion.Lerp(
                    currentRotation, // 현재 날개의 회전량(로테이션)에
                    targetRotation,  // 목표 회전량(로테이션) 까지
                    wingRotationDamping // damping값의 속도로 보간
                );
                return currentRotation;
            }

        }
        
        #endregion


        public void Respawn(Vector3 position, Quaternion rotation)
        {
            transform.position = position;
            transform.rotation = rotation;

            rBody.linearVelocity = Vector3.zero;
            rBody.angularVelocity = Vector3.zero;
        }

        public void Dead()
        {
            rBody.useGravity = true;
        }
    }

}

