using System;

namespace DesertSettlement.Core.Timing
{
    public enum DayPhase
    {
        Day,
        Night,
    }

    /// <summary>게임 시계의 한 시점. 정각 이벤트에 함께 전달된다.</summary>
    public readonly struct GameTimeStamp
    {
        /// <summary>정착지 시작 후 지난 정각 수(1부터). 경제 틱 번호로 쓴다.</summary>
        public readonly long ElapsedHours;
        /// <summary>정착지 시작일 = 1일차.</summary>
        public readonly int Day;
        /// <summary>0~23.</summary>
        public readonly int HourOfDay;
        public readonly DayPhase Phase;

        public GameTimeStamp(long elapsedHours, int day, int hourOfDay, DayPhase phase)
        {
            ElapsedHours = elapsedHours;
            Day = day;
            HourOfDay = hourOfDay;
            Phase = phase;
        }

        public override string ToString() => $"{Day}일차 {HourOfDay:00}:00 ({Phase})";
    }

    /// <summary>
    /// 게임 전용 시계. Unity Time.timeScale을 쓰지 않고, 매 프레임 현실 경과 시간을 받아 게임 시간을 누적한다.
    /// - 1배속 하루 = RealSecondsPerGameDay초(확정 90초), 하루 = 24게임시간
    /// - 정착지마다 <see cref="GameTimeSettings.StartHourOfDay"/>(12시)에서 시작
    /// - 정각마다 <see cref="HourPassed"/>, 자정마다 <see cref="DayStarted"/>, 낮/밤 전환 시 <see cref="PhaseChanged"/>
    /// 한 프레임에 여러 시간이 지나가도 정각 이벤트는 빠짐없이 순서대로 발생한다.
    /// 일시정지 중에는 시간이 흐르지 않는다(배속 설정과 별개).
    /// </summary>
    public sealed class GameClock
    {
        public const int HoursPerDay = 24;

        private readonly GameTimeSettings _settings;
        private double _elapsedHours;

        public double RealSecondsPerGameDay { get; }

        /// <summary>정착지 시작 후 흐른 게임 시간(소수 포함).</summary>
        public double ElapsedHours => _elapsedHours;

        /// <summary>시작 시각 기준 누적 시각(시 단위). 12시 시작이면 0시간 경과 시 12.</summary>
        private double AbsoluteHours => _settings.StartHourOfDay + _elapsedHours;

        /// <summary>현재 시각(0 이상 24 미만, 소수 포함). 낮·밤 조명 계산에 쓴다.</summary>
        public double HourOfDay => AbsoluteHours % HoursPerDay;

        /// <summary>현재 일차(시작일 = 1).</summary>
        public int Day => (int)Math.Floor(AbsoluteHours / HoursPerDay) + 1;

        public DayPhase Phase => PhaseAt((int)Math.Floor(HourOfDay));
        public bool IsDaytime => Phase == DayPhase.Day;

        public bool IsPaused { get; private set; }
        public int SpeedIndex { get; private set; }
        public float Speed => _settings.SpeedSteps[SpeedIndex];
        public int SpeedStepCount => _settings.SpeedSteps.Length;
        public float SpeedAt(int index) => _settings.SpeedSteps[index];

        /// <summary>시간이 실제로 흐르는 상태인지(일시정지 아님 + 배속 0 아님).</summary>
        public bool IsRunning => !IsPaused && Speed > 0f;

        public event Action<GameTimeStamp> HourPassed;
        public event Action<GameTimeStamp> DayStarted;
        public event Action<GameTimeStamp> PhaseChanged;
        public event Action SpeedOrPauseChanged;

        public GameClock(GameTimeSettings settings, double realSecondsPerGameDay)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _settings.Validate();
            if (realSecondsPerGameDay <= 0) throw new ArgumentOutOfRangeException(nameof(realSecondsPerGameDay));

            RealSecondsPerGameDay = realSecondsPerGameDay;
            SpeedIndex = _settings.DefaultSpeedIndex;
        }

        /// <summary>새 정착지 시작: 시계를 시작 시각으로 되돌린다(이벤트 없음).</summary>
        public void ResetForNewSettlement()
        {
            _elapsedHours = 0;
        }

        public void SetPaused(bool paused)
        {
            if (IsPaused == paused) return;
            IsPaused = paused;
            SpeedOrPauseChanged?.Invoke();
        }

        public void TogglePause() => SetPaused(!IsPaused);

        public void SetSpeedIndex(int index)
        {
            if (index < 0 || index >= _settings.SpeedSteps.Length) throw new ArgumentOutOfRangeException(nameof(index));
            if (SpeedIndex == index) return;
            SpeedIndex = index;
            SpeedOrPauseChanged?.Invoke();
        }

        /// <summary>
        /// 현실 경과 시간(초)만큼 시계를 진행한다. Unity에서는 Time.unscaledDeltaTime을 넘긴다.
        /// </summary>
        public void Advance(double realDeltaSeconds)
        {
            if (realDeltaSeconds <= 0 || !IsRunning) return;

            double gameHours = realDeltaSeconds * Speed * HoursPerDay / RealSecondsPerGameDay;
            AdvanceGameHours(gameHours);
        }

        /// <summary>게임 시간(시)을 직접 진행한다. 테스트·디버그용이며 일시정지·배속을 무시한다.</summary>
        public void AdvanceGameHours(double gameHours)
        {
            if (gameHours <= 0) return;

            double target = _elapsedHours + gameHours;
            long nextBoundary = (long)Math.Floor(_elapsedHours) + 1;

            while (nextBoundary <= target)
            {
                DayPhase before = Phase;
                _elapsedHours = nextBoundary;
                GameTimeStamp stamp = StampNow(nextBoundary);

                HourPassed?.Invoke(stamp);
                if (stamp.HourOfDay == 0) DayStarted?.Invoke(stamp);
                if (stamp.Phase != before) PhaseChanged?.Invoke(stamp);

                nextBoundary++;
            }

            _elapsedHours = target;
        }

        private GameTimeStamp StampNow(long elapsedWholeHours)
        {
            int hour = (int)((_settings.StartHourOfDay + elapsedWholeHours) % HoursPerDay);
            return new GameTimeStamp(elapsedWholeHours, Day, hour, PhaseAt(hour));
        }

        private DayPhase PhaseAt(int hour)
        {
            int dayStart = _settings.DayStartHour;
            int nightStart = _settings.NightStartHour;
            bool isDay = dayStart < nightStart
                ? hour >= dayStart && hour < nightStart
                : hour >= dayStart || hour < nightStart; // 낮이 자정을 넘기는 설정도 허용
            return isDay ? DayPhase.Day : DayPhase.Night;
        }
    }
}
