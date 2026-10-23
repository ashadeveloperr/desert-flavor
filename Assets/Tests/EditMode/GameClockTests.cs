using System.Collections.Generic;
using DesertSettlement.Core.Timing;
using NUnit.Framework;

namespace DesertSettlement.Core.Tests
{
    public class GameClockTests
    {
        private const double DaySeconds = 90.0;
        private const double Epsilon = 1e-9;

        private static GameClock NewClock(GameTimeSettings settings = null) =>
            new GameClock(settings ?? new GameTimeSettings(), DaySeconds);

        [Test]
        public void StartsAtNoonOnDayOne_InDaytime()
        {
            GameClock clock = NewClock();
            Assert.AreEqual(12.0, clock.HourOfDay, Epsilon);
            Assert.AreEqual(1, clock.Day);
            Assert.AreEqual(DayPhase.Day, clock.Phase);
            Assert.AreEqual(1f, clock.Speed);
        }

        [Test]
        public void OneGameHourIs3Point75SecondsAt1x()
        {
            GameClock clock = NewClock();
            var hours = new List<GameTimeStamp>();
            clock.HourPassed += hours.Add;

            clock.Advance(3.74);
            Assert.AreEqual(0, hours.Count);
            clock.Advance(0.02); // 합계 3.76초 > 3.75초
            Assert.AreEqual(1, hours.Count);
            Assert.AreEqual(13, hours[0].HourOfDay);
            Assert.AreEqual(1, hours[0].ElapsedHours);
        }

        [Test]
        public void OneGameDayIs90SecondsAt1x()
        {
            GameClock clock = NewClock();
            clock.Advance(DaySeconds);
            Assert.AreEqual(24.0, clock.ElapsedHours, 1e-6);
            Assert.AreEqual(12.0, clock.HourOfDay, 1e-6);
            Assert.AreEqual(2, clock.Day);
        }

        [Test]
        public void SpeedMultipliesTime_FourXDayIs22Point5Seconds()
        {
            GameClock clock = NewClock();
            clock.SetSpeedIndex(3); // 4배속
            clock.Advance(22.5);
            Assert.AreEqual(24.0, clock.ElapsedHours, 1e-6);
        }

        [Test]
        public void PausedOrZeroSpeed_DoesNotAdvance()
        {
            GameClock clock = NewClock();
            clock.SetPaused(true);
            clock.Advance(1000);
            Assert.AreEqual(0.0, clock.ElapsedHours);

            clock.SetPaused(false);
            clock.SetSpeedIndex(0); // 0배속
            clock.Advance(1000);
            Assert.AreEqual(0.0, clock.ElapsedHours);
            Assert.IsFalse(clock.IsRunning);
        }

        [Test]
        public void LargeDelta_EmitsEveryHourInOrder()
        {
            GameClock clock = NewClock();
            var hours = new List<GameTimeStamp>();
            clock.HourPassed += hours.Add;

            clock.Advance(DaySeconds * 2 + 1.0); // 48시간 + α
            Assert.AreEqual(48, hours.Count);
            for (int i = 0; i < hours.Count; i++)
            {
                Assert.AreEqual(i + 1, hours[i].ElapsedHours);
                Assert.AreEqual((12 + i + 1) % 24, hours[i].HourOfDay);
            }
        }

        [Test]
        public void DayStarted_FiresAtMidnight_WithNewDayNumber()
        {
            GameClock clock = NewClock();
            var days = new List<GameTimeStamp>();
            clock.DayStarted += days.Add;

            clock.AdvanceGameHours(11.5);
            Assert.AreEqual(0, days.Count, "12시 시작 → 23:30, 아직 자정 전");
            clock.AdvanceGameHours(0.5);
            Assert.AreEqual(1, days.Count);
            Assert.AreEqual(2, days[0].Day);
            Assert.AreEqual(0, days[0].HourOfDay);
        }

        [Test]
        public void PhaseChanges_AtNightAndDayStartHours()
        {
            GameClock clock = NewClock(); // 낮 5시, 밤 20시
            var changes = new List<GameTimeStamp>();
            clock.PhaseChanged += changes.Add;

            clock.AdvanceGameHours(24);
            Assert.AreEqual(2, changes.Count);
            Assert.AreEqual(20, changes[0].HourOfDay);
            Assert.AreEqual(DayPhase.Night, changes[0].Phase);
            Assert.AreEqual(5, changes[1].HourOfDay);
            Assert.AreEqual(DayPhase.Day, changes[1].Phase);
        }

        [Test]
        public void PhaseBoundariesAreEditable_IncludingDayAcrossMidnight()
        {
            var settings = new GameTimeSettings { DayStartHour = 20, NightStartHour = 4 };
            GameClock clock = NewClock(settings);
            Assert.AreEqual(DayPhase.Night, clock.Phase, "12시는 밤");
            clock.AdvanceGameHours(9); // 21시
            Assert.AreEqual(DayPhase.Day, clock.Phase);
            clock.AdvanceGameHours(4); // 1시
            Assert.AreEqual(DayPhase.Day, clock.Phase);
        }

        [Test]
        public void ResetForNewSettlement_ReturnsToNoonDayOne()
        {
            GameClock clock = NewClock();
            clock.AdvanceGameHours(50);
            clock.ResetForNewSettlement();
            Assert.AreEqual(12.0, clock.HourOfDay, Epsilon);
            Assert.AreEqual(1, clock.Day);
        }

        [Test]
        public void InvalidSettings_AreRejected()
        {
            Assert.Throws<System.ArgumentException>(() => NewClock(new GameTimeSettings { StartHourOfDay = 24 }));
            Assert.Throws<System.ArgumentException>(() => NewClock(new GameTimeSettings { DayStartHour = 6, NightStartHour = 6 }));
            Assert.Throws<System.ArgumentException>(() => NewClock(new GameTimeSettings { SpeedSteps = new float[0] }));
            Assert.Throws<System.ArgumentException>(() => NewClock(new GameTimeSettings { DefaultSpeedIndex = 9 }));
        }
    }
}
