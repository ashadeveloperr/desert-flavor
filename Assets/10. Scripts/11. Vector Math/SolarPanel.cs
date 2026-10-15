// ─────────────────────────────────────────────
// SolarPanel — 패널 한 장이 지금 받는 햇빛으로 발전 세기와 오늘 발전량을 계산하고 막대로 보여 준다
// 학습 포인트
//  - 패널 법선은 transform.up입니다. 패널을 기울이면 법선도 같이 기웁니다(03 §3.4 비탈 그림과 같은 원리).
//  - 해가 지평선 아래면 무대가 발전을 0으로 둡니다. 땅이 해를 가리기 때문입니다 — 이것은 문제가 아니라 무대 규칙입니다.
// 조작
//  - 없음
// ─────────────────────────────────────────────
using UnityEngine;

namespace Study3D.VectorMath
{
    public class SolarPanel : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private SolarPanelLab lab;
        [SerializeField] private SunCycle sunCycle;

        [Header("표시")]
        [SerializeField] private string displayName = "패널";

        [Header("발전")]
        [SerializeField] private float ratedKw = 1f;

        [Header("표시")]
        [SerializeField] private Transform powerBar;
        [SerializeField] private float barMaxHeight = 2f;

        private Vector3 powerBarBaseScale;
        private Vector3 powerBarLocalPosition;

        public string DisplayName => displayName;
        public float PowerKw { get; private set; }
        public float EnergyKwh { get; private set; }

        public void ResetEnergy()
        {
            EnergyKwh = 0f;
        }

        private void Awake()
        {
            if (powerBar != null)
            {
                powerBarBaseScale = powerBar.localScale;
                powerBarLocalPosition = powerBar.localPosition;
            }
        }

        private void LateUpdate()
        {
            if (lab == null || sunCycle == null) return;

            Vector3 toSun = lab.DirectionToSun(sunCycle.LightForward);
            // 해가 지평선 아래면 땅이 해를 가립니다. 기울인 패널은 지평선 아래 해와도 내적이 양수일 수 있어 무대가 따로 막습니다.
            float light = sunCycle.IsDaytime ? lab.Sunlight(transform.up, toSun) : 0f;

            // NaN·무한대만 0 으로 바꿉니다(화면이 깨지지 않게). 음수나 1 초과처럼 틀린 값은 그대로 둡니다 — 학생이 버그를 봐야 합니다.
            if (float.IsNaN(light) || float.IsInfinity(light)) light = 0f;

            PowerKw = ratedKw * light;
            EnergyKwh += PowerKw * sunCycle.DeltaHours;

            if (powerBar != null)
            {
                // 막대만 0 ~ 1 로 자릅니다. 음수 막대는 그릴 수 없으니, 틀린 숫자는 HUD 읽기값으로 보여 줍니다.
                float h = Mathf.Max(0.02f, barMaxHeight * Mathf.Clamp01(light));
                powerBar.localScale = new Vector3(powerBarBaseScale.x, h, powerBarBaseScale.z);
                powerBar.localPosition = new Vector3(powerBarLocalPosition.x, h * 0.5f, powerBarLocalPosition.z);
            }
        }
    }
}
