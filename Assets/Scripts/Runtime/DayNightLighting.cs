using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 게임 시계의 현재 시각에 맞춰 태양(Directional Light)을 돌리고 색·밝기를 바꾼다.
    /// 시각 표현 전용이며 생산 등 게임 규칙에는 영향을 주지 않는다(주야간 생산 보정은 도입하지 않는 방향, 잠정).
    /// 색과 밝기는 인스펙터의 Gradient / Curve로 조정한다(가로축 = 0~24시).
    /// </summary>
    public sealed class DayNightLighting : MonoBehaviour
    {
        [SerializeField] private GameSession session;
        [SerializeField] private Light sun;

        [Tooltip("태양이 뜨는 시각. 이 시각에 태양 고도 0°")]
        [Range(0f, 24f)]
        [SerializeField] private float sunriseHour = 6f;
        [Tooltip("태양이 지는 시각. 이 시각에 태양 고도 180°(반대편 지평선)")]
        [Range(0f, 24f)]
        [SerializeField] private float sunsetHour = 18f;
        [Tooltip("태양이 지나가는 방위각(Y축 회전)")]
        [SerializeField] private float sunAzimuth = -30f;

        [Tooltip("시각별 햇빛 색. 0 = 0시, 1 = 24시")]
        [SerializeField] private Gradient sunColor = DefaultSunColor();
        [Tooltip("시각별 햇빛 밝기. 가로축 0~24시")]
        [SerializeField] private AnimationCurve sunIntensity = DefaultSunIntensity();
        [Tooltip("시각별 환경광 배율. 가로축 0~24시")]
        [SerializeField] private AnimationCurve ambientIntensity = DefaultAmbient();

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

            sun.transform.rotation = Quaternion.Euler(elevation, sunAzimuth, 0f);
            sun.color = sunColor.Evaluate(hour / 24f);
            sun.intensity = sunIntensity.Evaluate(hour);
            RenderSettings.ambientIntensity = ambientIntensity.Evaluate(hour);
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
