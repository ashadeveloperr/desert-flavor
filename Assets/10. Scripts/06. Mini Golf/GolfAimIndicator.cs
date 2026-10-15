using System;
using UnityEngine;

namespace Study3D.MiniGolf
{
    public class GolfAimIndicator : MonoBehaviour
    {
        [Header("Settings")] 
        public float minLength;
        public float maxLength;

        private LineRenderer line;

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.useWorldSpace = true; // 월드 공간 좌표계를 사용한다고 설정합니다.
            line.positionCount = 2; // 점 두개의 위치로 선을 그린다고 설정합니다.
            Hide();
        }

        public void Show(Vector3 origin, Vector3 direction, float power01)
        {
            // 0~1 사이의 비율로 들어온 power 값을
            // 내가 가진 최소 길이(minLength : 0)와 최대 길이(maxLength :0)으로
            // 치환합니다(바꿔줍니다)
            float length = Mathf.Lerp(minLength, maxLength, power01);
            line.SetPosition(0, origin);
            line.SetPosition(1, origin + direction * length);
            line.enabled = true;
        }

        public void Hide()
        {
            line.enabled = false;
        }
    }
}