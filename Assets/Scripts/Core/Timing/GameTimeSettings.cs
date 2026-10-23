using System;

namespace DesertSettlement.Core.Timing
{
    /// <summary>
    /// 시간 시스템에서 사람이 조정할 값. Unity 인스펙터에서 바로 편집된다(GameSession 컴포넌트).
    /// 하루 길이(1배속 180초, 2026-10-23 W1)는 여기 없고 설정 JSON의 confirmedRules에서 읽는다.
    /// </summary>
    [Serializable]
    public sealed class GameTimeSettings
    {
        /// <summary>정착지를 시작할 때의 시각(0~23). 확정: 정오 12시.</summary>
        public int StartHourOfDay = 12;

        /// <summary>낮이 시작되는 시각(0~23). 2026-10-23 확정: 5시(낮 15시간).</summary>
        public int DayStartHour = 5;

        /// <summary>밤이 시작되는 시각(0~23). 2026-10-23 확정: 20시.</summary>
        public int NightStartHour = 20;

        /// <summary>선택 가능한 배속 목록(1/2/4). 멈춤은 배속이 아니라 일시정지로만 한다(2026-10-23 플레이테스트 메모: 0배속 제거).</summary>
        public float[] SpeedSteps = { 1f, 2f, 4f };

        /// <summary>시작 시 선택되는 배속의 SpeedSteps 인덱스.</summary>
        public int DefaultSpeedIndex = 0;

        /// <summary>값이 잘못됐으면 설명과 함께 예외를 던진다.</summary>
        public void Validate()
        {
            CheckHour(StartHourOfDay, nameof(StartHourOfDay));
            CheckHour(DayStartHour, nameof(DayStartHour));
            CheckHour(NightStartHour, nameof(NightStartHour));
            if (DayStartHour == NightStartHour)
            {
                throw new ArgumentException("낮 시작 시각과 밤 시작 시각이 같을 수 없습니다.");
            }
            if (SpeedSteps == null || SpeedSteps.Length == 0)
            {
                throw new ArgumentException("배속 목록(SpeedSteps)이 비어 있습니다.");
            }
            foreach (float speed in SpeedSteps)
            {
                if (speed <= 0f) throw new ArgumentException($"배속은 0보다 커야 합니다(멈춤은 일시정지로): {speed}");
            }
            if (DefaultSpeedIndex < 0 || DefaultSpeedIndex >= SpeedSteps.Length)
            {
                throw new ArgumentException($"DefaultSpeedIndex가 배속 목록 범위를 벗어났습니다: {DefaultSpeedIndex}");
            }
        }

        private static void CheckHour(int hour, string name)
        {
            if (hour < 0 || hour > 23) throw new ArgumentException($"{name}는 0~23이어야 합니다: {hour}");
        }
    }
}
