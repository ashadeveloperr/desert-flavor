using System;
using DesertSettlement.Core.Economy;
using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>시설 등불(결정 MN1·MN2·LT1~LT7): 밤에 켜지는 불빛·점광원과 숨쉬기. 값은 인스펙터 '밤 등불'.</summary>
    public sealed partial class SettlementView
    {
        [Header("밤 등불")]
        [Tooltip("밤에 빛나는 불빛 색(1보다 크면 블룸으로 번져 보인다)")]
        [ColorUsage(false, true)]
        [SerializeField] private Color lanternGlow = new Color(2.6f, 1.7f, 0.7f);
        [Tooltip("낮이거나 폐쇄된 시설의 꺼진 등불 유리 색")]
        [SerializeField] private Color lanternOff = new Color(0.45f, 0.38f, 0.28f);
        [Tooltip("바닥을 비추는 점광원 색·최대 밝기·범위(타일 크기 대비)")]
        [SerializeField] private Color lanternLightColor = new Color(1f, 0.72f, 0.4f);
        [SerializeField] private float lanternLightIntensity = 1.6f;
        [SerializeField] private float lanternLightRange = 1.3f;
        [Tooltip("밤 시작 전·꺼지기 전 등불이 서서히 켜지고 꺼지는 시간(게임 시간)")]
        [SerializeField] private float lanternFadeHours = 1f;
        [Tooltip("낮이 시작된 뒤에도 켜져 있는 시간(게임 시간). 1이면 낮 5시 기준 5~6시에 서서히 꺼진다(이슈 #16)")]
        [SerializeField] private float lanternLingerHours = 1f;
        [Tooltip("숨쉬기 주기 범위(1배속 기준 초). 시설마다 이 범위에서 다른 주기·다른 시작점이고 배속을 따른다(이슈 #16)")]
        [SerializeField] private Vector2 lanternBreathSeconds = new Vector2(5f, 8f);
        [Tooltip("숨쉬기 중 가장 어두울 때의 밝기 비율(1 = 숨쉬기 없음)")]
        [Range(0f, 1f)] [SerializeField] private float lanternBreathMin = 0.7f;
        [Tooltip("촛불 같은 빠른 잔떨림의 폭(0.03 = ±3%, 0 = 없음)")]
        [Range(0f, 0.2f)] [SerializeField] private float lanternFlicker = 0.03f;

        private Material _lanternLit, _lanternOffMaterial;
        private float _lanternFactor;

        /// <summary>
        /// 시설 하나의 등불을 맞춘다. 폐쇄된 시설만 꺼지고 운영·건설·개량·철거 중에는 켜진다(결정 LT2, 사람이 일하는 중).
        /// 숨쉬기(결정 LT5): 시설마다 다른 주기·시작점으로 오르내리고 그 위에 약한 잔떨림. 게임 시간 기준이라 배속을 따르고 일시정지면 멈춘다.
        /// </summary>
        private void ApplyLantern(Facility f, FacilityModel model)
        {
            float glow = _lanternFactor * LanternBreath(f.Id, _session.Clock.ElapsedHours * _session.Clock.RealSecondsPerGameDay / 24.0,
                lanternBreathSeconds.x, lanternBreathSeconds.y, lanternBreathMin, lanternFlicker);
            model.SetLantern(f.State != FacilityState.Closed, Color.Lerp(lanternOff, lanternGlow, glow), _lanternLit, _lanternOffMaterial,
                lanternLightIntensity * glow, lanternLightColor, Size * lanternLightRange);
        }

        /// <summary>지금 시각의 등불 밝기(LT3)와 공용 불빛 머티리얼 색을 맞춘다.</summary>
        private void UpdateLanternMaterials()
        {
            if (_lanternLit == null)
            {
                _lanternLit = WorldVisuals.Unlit("Lantern Lit", lanternOff);
                _lanternOffMaterial = WorldVisuals.Unlit("Lantern Off", lanternOff);
            }
            var time = _session.TimeSettings;
            _lanternFactor = DayNightLighting.LanternFactor(_session.Clock.HourOfDay, time.DayStartHour, time.NightStartHour, lanternFadeHours, lanternLingerHours);
            _lanternOffMaterial.color = lanternOff;
        }

        /// <summary>
        /// 등불 숨쉬기 배율(결정 LT5·LT6). <paramref name="seconds"/>는 1배속 기준으로 환산한 게임 시간(초)이라 배속을 따른다.
        /// 시설 번호로 주기(<paramref name="minPeriod"/>~<paramref name="maxPeriod"/>초)와 시작점을 정해 <paramref name="breathMin"/>~1 사이를 천천히 오가고,
        /// 그 위에 ±<paramref name="flicker"/>의 빠른 잔떨림을 얹는다.
        /// </summary>
        public static float LanternBreath(int facilityId, double seconds, float minPeriod, float maxPeriod, float breathMin, float flicker)
        {
            uint h = (uint)facilityId * 2654435761u;
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            double u1 = (h & 0xFFFF) / 65535.0, u2 = (h >> 16) / 65535.0;
            double period = Math.Max(0.1, minPeriod + (maxPeriod - minPeriod) * u1);
            double phase = seconds / period + u2;
            double wave = 0.5 - 0.5 * Math.Cos(2 * Math.PI * (phase - Math.Floor(phase)));
            double breath = breathMin + (1 - breathMin) * wave;
            double jitter = 0.6 * Math.Sin(2 * Math.PI * (seconds / 0.37 + u2)) + 0.4 * Math.Sin(2 * Math.PI * (seconds / 0.23 + u1));
            return (float)Math.Max(0, breath * (1 + flicker * jitter));
        }
    }
}
