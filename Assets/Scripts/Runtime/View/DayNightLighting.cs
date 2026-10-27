using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 게임 시계의 현재 시각에 맞춰 태양(Directional Light)을 돌리고 색·밝기를 바꾼다.
    /// 시각 표현 전용이며 생산 등 게임 규칙에는 영향을 주지 않는다(주야간 생산 보정은 도입하지 않는 방향, 잠정).
    /// 일출·일몰은 기본적으로 GameSession 시계 설정(낮 시작·밤 시작, 2026-10-23: 5시·20시)을 따른다.
    /// 색과 밝기는 인스펙터의 Gradient / Curve로 조정한다. 곡선의 가로축은 '표준 시각'(일출 6시·일몰 18시 기준 0~24)이라,
    /// 실제 일출·일몰이 바뀌어도 곡선을 고칠 필요 없이 낮·밤 길이에 맞춰 늘어나고 줄어든다.
    /// </summary>
    public sealed class DayNightLighting : MonoBehaviour
    {
        [SerializeField] private GameSession session;
        [SerializeField] private Light sun;

        [Tooltip("켜면 일출·일몰을 GameSession 시계 설정(DayStartHour·NightStartHour)에서 읽는다. 끄면 아래 값을 쓴다")]
        [SerializeField] private bool useClockDayNight = true;
        [Tooltip("태양이 뜨는 시각(시계 설정을 따르지 않을 때). 이 시각에 태양 고도 0°")]
        [Range(0f, 24f)]
        [SerializeField] private float sunriseHour = 6f;
        [Tooltip("태양이 지는 시각(시계 설정을 따르지 않을 때). 이 시각에 태양 고도 180°(반대편 지평선)")]
        [Range(0f, 24f)]
        [SerializeField] private float sunsetHour = 18f;
        [Tooltip("태양이 지나가는 방위각(Y축 회전)")]
        [SerializeField] private float sunAzimuth = -30f;

        [Tooltip("표준 시각별 햇빛 색(일출 6·일몰 18 기준). 0 = 0시, 1 = 24시")]
        [SerializeField] private Gradient sunColor = DefaultSunColor();
        [Tooltip("표준 시각별 햇빛 밝기(일출 6·일몰 18 기준). 가로축 0~24")]
        [SerializeField] private AnimationCurve sunIntensity = DefaultSunIntensity();
        [Tooltip("표준 시각별 환경광 배율(일출 6·일몰 18 기준). 가로축 0~24")]
        [SerializeField] private AnimationCurve ambientIntensity = DefaultAmbient();

        // 밤 조명(2026-10-27 MN1~MN2, 이슈 #13: "밤이 너무 어둡다, 기본 월드 조명이 약하게라도"). 해가 진 동안 같은 방향광을 달빛으로 다시 띄운다.
        [Header("밤")]
        [Tooltip("달빛 색")]
        [SerializeField] private Color moonColor = new Color(0.55f, 0.66f, 1f);
        [Tooltip("달빛 최대 밝기(한밤, 낮 햇빛 최대는 약 1.3)")]
        [SerializeField] private float moonIntensity = 0.35f;
        [Tooltip("달 고도(도)")]
        [Range(10f, 89f)]
        [SerializeField] private float moonElevation = 50f;
        [Tooltip("일몰 뒤·일출 전 달빛이 서서히 켜지고 꺼지는 시간(밤 길이 대비 비율)")]
        [Range(0.01f, 0.5f)]
        [SerializeField] private float moonFade = 0.12f;
        [Tooltip("환경광 최소 배율(밤에도 이 아래로 내려가지 않는다, 낮 최대 1)")]
        [Range(0f, 1f)]
        [SerializeField] private float nightAmbientMin = 0.5f;

        /// <summary>코드로 연결할 때 쓴다.</summary>
        public void Bind(GameSession gameSession, Light sunLight)
        {
            session = gameSession;
            sun = sunLight;
        }

        private void Reset()
        {
            session = FindAnyObjectByType<GameSession>();
            if (RenderSettings.sun != null) sun = RenderSettings.sun;
        }

        private void LateUpdate()
        {
            if (session == null || sun == null || !session.IsReady) return;

            if (useClockDayNight)
            {
                sunriseHour = session.TimeSettings.DayStartHour;
                sunsetHour = session.TimeSettings.NightStartHour;
            }
            float hour = (float)session.Clock.HourOfDay;
            Apply(hour);
        }

        /// <summary>주어진 시각(0~24)의 조명을 적용한다. 에디터에서 미리보기할 때도 쓸 수 있다.</summary>
        public void Apply(float hour)
        {
            float dayLength = Mathf.Repeat(sunsetHour - sunriseHour, 24f);
            if (dayLength <= 0f) dayLength = 12f;
            float sinceSunrise = Mathf.Repeat(hour - sunriseHour, 24f);
            // 낮 동안 0→180°, 밤 동안 180→360°(지평선 아래)로 돈다.
            float elevation = sinceSunrise <= dayLength
                ? sinceSunrise / dayLength * 180f
                : 180f + (sinceSunrise - dayLength) / (24f - dayLength) * 180f;

            // 곡선은 표준 시각(일출 6·일몰 18)으로 그려져 있으므로, 실제 낮/밤 진행률을 표준 시각으로 바꿔 읽는다.
            float standard = sinceSunrise <= dayLength
                ? 6f + sinceSunrise / dayLength * 12f
                : Mathf.Repeat(18f + (sinceSunrise - dayLength) / (24f - dayLength) * 12f, 24f);

            RenderSettings.ambientIntensity = Mathf.Max(nightAmbientMin, ambientIntensity.Evaluate(standard));
            if (sinceSunrise <= dayLength)
            {
                sun.transform.rotation = Quaternion.Euler(elevation, sunAzimuth, 0f);
                sun.color = sunColor.Evaluate(standard / 24f);
                sun.intensity = sunIntensity.Evaluate(standard);
                return;
            }
            // 밤: 해는 지평선 아래라 비추지 않으므로 같은 광원을 반대편 하늘의 달로 쓴다. 일몰·일출 무렵엔 햇빛이 거의 0이라 방향이 바뀌어도 티가 나지 않는다.
            float night = (sinceSunrise - dayLength) / (24f - dayLength);
            float fade = Mathf.Clamp01(Mathf.Min(night, 1f - night) / moonFade);
            sun.transform.rotation = Quaternion.Euler(moonElevation, sunAzimuth + 180f, 0f);
            sun.color = moonColor;
            sun.intensity = Mathf.Max(sunIntensity.Evaluate(standard), moonIntensity * Mathf.SmoothStep(0f, 1f, fade));
        }

        /// <summary>
        /// 시설 등불 밝기 0~1(2026-10-27 LT3, 이슈 #13): 밤 시작 <paramref name="fadeHours"/>시간 전부터 서서히 켜져 밤 내내 1,
        /// 낮 시작 <paramref name="fadeHours"/>시간 전부터 서서히 꺼진다(밤 20시·낮 5시, 1시간이면 19~20시 켜짐·4~5시 꺼짐).
        /// </summary>
        public static float LanternFactor(double hour, double dayStartHour, double nightStartHour, float fadeHours)
        {
            float fade = Mathf.Max(0.01f, fadeHours);
            float nightLength = Mathf.Repeat((float)(dayStartHour - nightStartHour), 24f);
            float x = Mathf.Repeat((float)(hour - nightStartHour) + fade, 24f);
            if (x < fade) return x / fade;
            if (x < nightLength) return 1f;
            if (x < nightLength + fade) return 1f - (x - nightLength) / fade;
            return 0f;
        }

        private static Gradient DefaultSunColor()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.25f, 0.3f, 0.5f), 0f),     // 자정
                    new GradientColorKey(new Color(1f, 0.55f, 0.3f), 0.25f),    // 6시 일출
                    new GradientColorKey(new Color(1f, 0.96f, 0.88f), 0.5f),    // 정오
                    new GradientColorKey(new Color(1f, 0.5f, 0.25f), 0.75f),    // 18시 일몰
                    new GradientColorKey(new Color(0.25f, 0.3f, 0.5f), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }

        private static AnimationCurve DefaultSunIntensity() => new AnimationCurve(
            new Keyframe(0f, 0.05f), new Keyframe(5f, 0.05f), new Keyframe(7f, 0.8f),
            new Keyframe(12f, 1.3f), new Keyframe(17f, 0.8f), new Keyframe(19f, 0.05f), new Keyframe(24f, 0.05f));

        private static AnimationCurve DefaultAmbient() => new AnimationCurve(
            new Keyframe(0f, 0.25f), new Keyframe(6f, 0.6f), new Keyframe(12f, 1f),
            new Keyframe(18f, 0.6f), new Keyframe(24f, 0.25f));
    }
}
