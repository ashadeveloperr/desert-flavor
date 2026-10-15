using System;
using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study3D.SkinnedMeshes
{
    public class BoneFlap : MonoBehaviour
    {
        [Header("Settings")] 
        public float anglePerSecond = 20f;
        public Transform modelRoot;
        
        [Header("LeftWing")] // y 40 ~ -40
        public Transform leftWing;
        public Vector3 leftWingAxis;
        public float leftWingMaxAngle = 22f;
        
        [Header("RightWing")] // y -40 ~ -40
        public Transform rightWing;
        public Vector3 rightWingAxis;
        public float rightWingMaxAngle = 22f;

        private void Update()
        {
            // Q를 누르면 양쪽 날개가 올라가고
            // A를 누르면 양쪽 날개가 내려가는 애니메이션을 만들어 봅시다.

            bool pressedQ = SimpleInput.GetKey(Key.Q);
            bool pressedA = SimpleInput.GetKey(Key.A);
            
            if(pressedQ == false && pressedA == false) return;
            bool isUp = pressedQ; // 올라가는 방향이냐
            float angleDelta = anglePerSecond * Time.deltaTime; // 날개 각도의 초당 변화량
            
            //UsingRotate(isUp, angleDelta);
            UsingQuaternion(isUp, angleDelta);
        }

        private float sumAngle = 0;
        
        private void UsingRotate(bool isUp, float angleDelta)
        {
            // transform의 함수를 이용해서 회전시키는 법
            angleDelta *= isUp ? 1 : -1f;
            sumAngle += angleDelta;
            
            if (sumAngle < -leftWingMaxAngle || leftWingMaxAngle < sumAngle)
            {
                sumAngle =Mathf.Clamp(sumAngle, -leftWingMaxAngle, leftWingMaxAngle);
                angleDelta = 0.0f;
            }
            
            leftWing.Rotate(leftWingAxis * angleDelta, Space.Self);
            rightWing.Rotate(rightWingAxis * angleDelta, Space.Self);
        }

        private void UsingQuaternion(bool isUp, float angleDelta)
        {
            // 이번에는 회전량을 생성해서 적용하는법
            angleDelta *= isUp ? 1 : -1f;

            Quaternion leftRotation = Quaternion.AngleAxis(angleDelta, leftWingAxis);
            leftWing.localRotation *= leftRotation;
            
            Quaternion rightRotation = Quaternion.AngleAxis(angleDelta, rightWingAxis);
            rightWing.localRotation *= rightRotation;
        }
    }
}
