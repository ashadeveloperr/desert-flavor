using System;
using UnityEngine;

namespace Study3D.Rendering
{
    // SunCycle
    // : Directional Light를 회전시켜서 하루의 해를 구현해 봅시다.
    // - Directional Light는 오직 회전만 의미가 있습니다. 위치는 반영되지 않습니다.
    // - X축의 각도가 곧 해의 높이 입니다. 
    // - Gradient로 시간대 별 빛 색을, 해의 높이로 밝기를 정합니다.
    
    public class SunCycle : MonoBehaviour
    {
        private static Gradient CreateDefaultGradient()
        {
            var gradient = new Gradient();

            var colorKeys = new[]
            {
                new GradientColorKey(new Color(1f, 0.55f, 0.25f), 0.00f),
                new GradientColorKey(new Color(1f, 0.97f, 0.90f), 0.25f),
                new GradientColorKey(new Color(1f, 0.50f, 0.25f), 0.50f),
                new GradientColorKey(new Color(0.15f, 0.15f, 0.35f), 0.75f),
                new GradientColorKey(new Color(1f, 0.55f, 0.25f), 1.00f),
            };

            var alphaKeys = new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f),
            };

            gradient.SetKeys(colorKeys, alphaKeys);
            return gradient;
        }
        
        [Header("Day Settings")] 
        public float dayLengthSeconds = 20;
        public float startAngle = 50.0f;

        [Header("Sun Settings")] 
        public Gradient colorOverDay = CreateDefaultGradient();
        public float maxIntensity = 1.2f;

        private Light sun;
        private float angle;
        private float yaw; // 나중에 다룰테지만 현재는 y angle값이라고 생각하시면 됩니다.

        private void Awake()
        {
            sun = GetComponent<Light>();
            yaw = transform.eulerAngles.y;
            angle = startAngle;
        }

        private void Update()
        {
            float deltaAngle = (360 / dayLengthSeconds) * Time.deltaTime;
            // 현재 angle을 계산해줍니다.
            angle = Mathf.Repeat(angle + deltaAngle, 360f);
            transform.rotation = Quaternion.Euler(angle, yaw, 0f);
            // sun의 color를 가져옵니다 ( 현재 각도 / 360 => 백분율(0f ~ 1f 값으로 바꿔준거))
            sun.color = colorOverDay.Evaluate(angle / 360);

            // 해가 지평선 아래로 내려가면 밝기를 0으로 낮춥니다
            // 밑의 Dot은 내적이라는 Vector의 곱연산중 하나로, 아래에서는
            // transform.forward와 Vector.Down(월드의 Down벡터가) 얼마나 같은가를 체크합니다
            // 같은 1이 나오고 서로 정 반대이면 0이 나옵니다
            float height = Vector3.Dot(transform.forward, Vector3.down);
            sun.intensity = maxIntensity * Mathf.Clamp01(height);
        }
    }
}