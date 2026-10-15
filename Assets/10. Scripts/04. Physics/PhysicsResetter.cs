using System;
using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study3D.ThreeDPhysics
{
    public class PhysicsResetter : MonoBehaviour
    {
        private Rigidbody[] bodies;
        private Vector3[] positions;
        private Quaternion[] rotations;

        private bool resetRequested;

        private void Awake()
        {
            bodies = GetComponentsInChildren<Rigidbody>();
            positions = new Vector3[bodies.Length];
            rotations = new Quaternion[bodies.Length];

            for (int i = 0; i < bodies.Length; i++)
            {
                // 각 리지드바디 물체의 위치와 회전량의 초기 값을 저장해줍니다.
                positions[i] = bodies[i].position;
                rotations[i] = bodies[i].rotation;
            }
        }

        private void Update()
        {
            // 입력은 Update에서 담당해야 Pass 되는 경우가 없습니다.
            if (SimpleInput.GetKeyDown(Key.R)) resetRequested = true;
        }

        private void FixedUpdate()
        {
            if(resetRequested)
            {
                ResetAll();
                resetRequested = false;
            }
        }

        public void ResetAll()
        {
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody body = bodies[i];
                
                // isKinematic이 true로 되어있으면
                // 물리엔진의 작용/반작용 법칙에서 벗어난 특수한 물리처리를 하는
                // 개체가 됩니다. 그래서 isKinematic이 true개체는 속도를 초기화 해주지 않습니다.
                if (body.isKinematic == false)
                {
                    // linearVelocity : 선 속도(직선 움직임)
                    body.linearVelocity = Vector3.zero;
                    // angularVelocity : 각 속도(회전 움직임)
                    body.angularVelocity = Vector3.zero;
                }
                
                body.position = positions[i];
                body.rotation = rotations[i];
                // 화면에서 해당 개체가 튀지 않도록 transform도 함께 변경해 줍니다.
                // (transform이 갱신되는 타이밍과 물리개체의 위치나 회전이 바뀌는 타이밍이 다르기 때문)
                body.transform.SetPositionAndRotation(positions[i], rotations[i]);
            }
        }
    }
}