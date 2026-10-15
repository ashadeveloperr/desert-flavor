using System;
using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study3D.Transforms
{
    // 키를 눌러서 대상의 부모를 두 개체 사이에서 바꾸는 클래스
    public class ParentSwitcher : MonoBehaviour
    {
        public Transform target;
        public Transform parentA;
        public Transform parentB;

        private Vector3 startLocalPosition;
        private Quaternion startLocalRotation; // Quaternion은 회전을 표현하는 단위 입니다.

        private void Awake()
        {
            startLocalPosition = target.localPosition;
            startLocalRotation = target.localRotation;
        }

        private void Update()
        {
            if(SimpleInput.GetKeyDown(Key.Digit1)) Switch(true);
            if(SimpleInput.GetKeyDown(Key.Digit2)) Switch(false);
        }

        private void Switch(bool worldPositionStays)
        {
            // target.parent와 parentA가 동일하니? 동일하다면 parentB이고, 아니라면 parentA야
            Transform nextParent = (target.parent == parentA) ? parentB : parentA;
            target.SetParent(nextParent, worldPositionStays);

            // 월드 위치 좌표를 유지할 경우 종료.
            if (worldPositionStays) return;
            
            // 월드 위치 좌표를 유지하지 않는다면 첫 위치로 리셋
            target.localPosition = startLocalPosition;
            target.localRotation = startLocalRotation;
        }
    }
}