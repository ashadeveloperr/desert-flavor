// ─────────────────────────────────────────────
// SunCycle — 해를 하루 궤도로 돌리고, 시간 조작 키를 받고, 패널 여섯 장의 발전량을 HUD에 찍는다
// 학습 포인트
//  - 해의 방향은 동쪽(+x)과 정오 방향을 코사인·사인으로 섞은 선형결합입니다(02 §3.4). 해가 뜨고 지는 큰 원이 됩니다.
//  - 1초 = 게임 속 1시간입니다. 발전량(kWh)은 발전 세기(kW)에 흐른 시간(시간)을 곱해 쌓습니다.
// 조작
//  - Space : 시간 멈추기 / 다시 흐르기
//  - Q : 시간 되감기 (누르는 동안)
//  - E : 시간 빨리 감기 (누르는 동안)
//  - R : 새벽 6시로, 발전량 0으로
// ─────────────────────────────────────────────
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study3D.VectorMath
{
    public class SunCycle : MonoBehaviour
    {
        [Header("해")]
        [SerializeField] private Light sunLight;
        [SerializeField] private Transform sunMarker;
        [SerializeField] private float markerDistance = 10f;
        [SerializeField] [Range(10f, 90f)] private float noonElevation = 60f;

        [Header("시간")]
        [SerializeField] [Range(0f, 24f)] private float hour = 6f;
        [SerializeField] private float hoursPerSecond = 1f;
        [SerializeField] private float fastMultiplier = 4f;

        [Header("표시")]
        [SerializeField] private SolarPanel[] panels;
        [SerializeField] private SunTracker tracker;
        [SerializeField] private TMP_Text readoutText;

        private bool paused;
        private Vector3 sunDirection;

        public Vector3 LightForward => sunLight != null ? sunLight.transform.forward : Vector3.down;
        public bool IsDaytime => sunDirection.y > 0f;
        public float DeltaHours { get; private set; }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.spaceKey.wasPressedThisFrame) paused = !paused;

            if (keyboard.rKey.wasPressedThisFrame)
            {
                hour = 6f;
                foreach (SolarPanel p in panels)
                {
                    if (p != null) p.ResetEnergy();
                }
            }

            float rate = paused ? 0f : 1f;          // Q/E 는 멈춤 중에도 동작합니다
            if (keyboard.qKey.isPressed) rate = -fastMultiplier;
            else if (keyboard.eKey.isPressed) rate = fastMultiplier;

            float step = rate * hoursPerSecond * Time.deltaTime;
            hour = Mathf.Repeat(hour + step, 24f);

            // 되감을 때는 0 — 시간을 거꾸로 돌려도 이미 모은 발전량은 줄지 않습니다
            DeltaHours = Mathf.Max(0f, step);

            ApplySun();
            WriteReadout();
        }

        private void OnValidate()
        {
            hoursPerSecond = Mathf.Max(0f, hoursPerSecond);
            fastMultiplier = Mathf.Max(0f, fastMultiplier);
            markerDistance = Mathf.Max(0f, markerDistance);

            if (sunLight != null || sunMarker != null)
            {
                ApplySun();
            }
        }

        private void ApplySun()
        {
            float angle = (hour - 6f) / 24f * 2f * Mathf.PI;      // 6시 = 0, 12시 = 90도, 18시 = 180도
            float elevation = noonElevation * Mathf.Deg2Rad;
            Vector3 noonDirection = new Vector3(0f, Mathf.Sin(elevation), -Mathf.Cos(elevation));   // 정오의 해 = 남쪽 하늘
            sunDirection = Vector3.right * Mathf.Cos(angle) + noonDirection * Mathf.Sin(angle);     // 동쪽과 정오 방향의 선형결합

            if (sunLight != null)
            {
                sunLight.transform.rotation = Quaternion.LookRotation(-sunDirection);
                sunLight.intensity = Mathf.Clamp01(sunDirection.y * 4f);
            }

            if (sunMarker != null)
            {
                sunMarker.position = sunDirection * markerDistance;
            }
        }

        private void WriteReadout()
        {
            if (readoutText == null) return;

            int h = (int)hour;
            int m = (int)((hour - h) * 60f);
            float elevationDegrees = Mathf.Asin(Mathf.Clamp(sunDirection.y, -1f, 1f)) * Mathf.Rad2Deg;

            string line1 = $"시각 : {h:00}시 {m:00}분 (해 높이 {elevationDegrees:F0}도)";
            if (!IsDaytime) line1 += " · 밤";
            if (paused) line1 += " · 멈춤";

            string best = "없음";
            float bestEnergy = 0f;

            var text = new System.Text.StringBuilder();
            text.AppendLine(line1);

            for (int i = 0; i < panels.Length; i++)
            {
                SolarPanel p = panels[i];
                if (p == null) continue;

                text.AppendLine($"{p.DisplayName} : {p.PowerKw:F2} kW · 오늘 {p.EnergyKwh:F1} kWh");

                if (p.EnergyKwh > bestEnergy)
                {
                    bestEnergy = p.EnergyKwh;
                    best = p.DisplayName;
                }
            }

            if (tracker != null)
            {
                text.AppendLine($"추적 패널이 고른 후보 : {tracker.CurrentIndex}번 ({tracker.CurrentName})");
            }

            text.Append($"가장 많이 모은 패널 : {best}");

            readoutText.text = text.ToString();
        }
    }
}
