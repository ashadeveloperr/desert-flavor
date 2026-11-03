using System;
using System.Linq;
using DesertSettlement.Core.Config;

namespace DesertSettlement.Core.Economy
{
    /// <summary>
    /// 작업 큐: 착공·완공·공사 지연·진행률. 착공과 완공은 분 단위 시각에 일어난다(명령 시각 + 공사 시간에 완공, 결정 JQ2·JQ4).
    /// 정각 정산(<see cref="AdvanceHour"/>, Settlement.Tick.cs)은 그 시간 안에 완공 시각이 든 공사를 먼저 끝내고, 정산이 끝난 정각에 남은 착공을 한다.
    /// </summary>
    public sealed partial class Settlement
    {
        /// <summary>
        /// 진행 중인 시간 안의 시각 time까지 따라간다: 분(<see cref="Minute"/>)을 올리고, 그 사이 완공 시각이 지난 공사를 완공 시각 순서대로 끝낸다
        /// (비는 슬롯에는 그 완공 시각에 다음 작업이 착공). 정각 정산은 하지 않는다 — 다음 정각을 넘는 시각은 이 시간의 59분으로 친다.
        /// 게임은 매 프레임과 명령 직전에 시계 시각으로 부르고(<see cref="TimeSource"/>), 저장 복원은 기록된 명령 시각으로 부른다.
        /// </summary>
        public void AdvanceTo(double time)
        {
            int minute = (int)Math.Floor((time - CurrentHour) * 60 + 1e-6);
            minute = Math.Max(0, Math.Min(59, minute));
            if (minute <= Minute) return;
            Minute = minute;
            CompleteJobsUntil(Now);
        }

        /// <summary>
        /// 완공 시각이 until 이하인 공사를 시각 순서대로 끝낸다. 완공한 공사가 든 정산 시간은 완공 시각이 든 시간이고(17:30 → 17, 정각 17:00 → 16),
        /// 그 시간의 생산부터 낸다. 명령 순간 착공이 켜져 있으면 비는 슬롯·돌려받은 석재로 그 완공 시각에 다음 작업을 착공한다.
        /// 프레임마다 나눠 불러도 한 번에 불러도 같은 결과가 나도록, 시각은 부른 시점이 아니라 각 공사의 완공 시각을 쓴다.
        /// </summary>
        private void CompleteJobsUntil(double until)
        {
            while (true)
            {
                ConstructionJob job = _jobs.Where(j => j.State == JobState.InProgress && j.EndTime <= until + 1e-9)
                    .OrderBy(j => j.EndTime).ThenBy(j => j.Id).FirstOrDefault();
                if (job == null) return;
                CompleteJob(job, job.DeadlineHour);
                if (Rules.StartJobsOnCommand) StartJobs(job.EndTime, job.DeadlineHour);
            }
        }

        private void CompleteJob(ConstructionJob job, long h)
        {
            _jobs.Remove(job);
            Facility f = GetFacility(job.FacilityId);
            switch (job.Type)
            {
                case JobType.Build:
                    f.State = FacilityState.Operating;
                    f.CompletedHour = h;
                    f.ActiveSinceHour = h;
                    if (f.Kind == BuildingKinds.AuctionHall && AuctionHallCompletedHour == null) AuctionHallCompletedHour = h;
                    FacilityCompleted?.Invoke(f); // 주택은 빈 채로 완공되고 유입 속도대로 찬다(결정 HI1)
                    break;
                case JobType.Upgrade:
                    f.Level = job.TargetLevel;
                    f.State = FacilityState.Operating;
                    f.ActiveSinceHour = h;
                    FacilityCompleted?.Invoke(f);
                    break;
                case JobType.Demolish:
                    RemoveFacility(f.Id);
                    _stateBeforeDemolish.Remove(f.Id);
                    AddStone(f.StoneInvested * Rules.DemolishRefundRatio);
                    break;
            }
        }

        /// <summary>
        /// 대기 작업을 슬롯·석재가 되는 만큼 time 시각에 착공한다. 완공 시각 = time + 공사 시간(12:30에 5시간 작업 → 17:30, 결정 JQ4).
        /// hour는 착공을 세는 정산 시간(<see cref="ConstructionJob.StartedHour"/>): 명령 착공은 진행 중인 시간, 정각 착공은 막 정산한 시간.
        /// 하나라도 착공했으면 true.
        /// </summary>
        private bool StartJobs(double time, long hour)
        {
            int running = JobsInProgress;
            bool blocked = false, started = false;
            foreach (ConstructionJob job in _jobs.Where(j => j.State == JobState.Queued).ToList())
            {
                if (running >= Rules.ConstructionSlots) break;
                // C2 확정: FIFO. 앞 작업이 막히면 뒤 건설·개량도 기다린다.
                // 결정 JQ1: 철거는 석재가 들지 않으므로 막힌 작업을 건너뛰어 시작한다(빈 슬롯이 있을 때).
                if (blocked && job.Type != JobType.Demolish) continue;
                if (!CanStart(job))
                {
                    blocked = true;
                    continue;
                }

                Stone -= job.StoneCost;
                job.State = JobState.InProgress;
                job.StartedHour = hour;
                job.StartTime = time;
                job.EndTime = time + job.Hours;
                switch (job.Type)
                {
                    case JobType.Build:
                        var f = new Facility
                        {
                            Id = _nextFacilityId++, Kind = job.Kind, Tile = job.Tile, Edge = job.Edge,
                            State = FacilityState.Constructing, StoneInvested = job.StoneCost,
                        };
                        _facilities.Add(f.Id, f);
                        job.FacilityId = f.Id;
                        break;
                    case JobType.Upgrade:
                        Facility up = GetFacility(job.FacilityId);
                        up.State = FacilityState.Upgrading;
                        up.StoneInvested += job.StoneCost;
                        break;
                    case JobType.Demolish:
                        Facility down = GetFacility(job.FacilityId);
                        _stateBeforeDemolish[down.Id] = down.State;
                        down.State = FacilityState.Demolishing;
                        down.Residents = 0; // 철거하는 주택의 주민은 떠난다
                        break;
                }
                running++;
                started = true;
                JobStarted?.Invoke(job);
            }
            return started;
        }

        /// <summary>
        /// 공사 속도가 1보다 느린 시간 h에는 그 시간에 공사한 몫만큼 완공 시각을 늦춘다: (1 − 속도) × (h 안에서 공사한 시간).
        /// 12:30에 착공했으면 12시는 반 시간만 센다. 이 시간 안에 이미 끝난 공사(<see cref="CompleteJobsUntil"/>가 먼저 끝냄)는 늦추지 않는다.
        /// </summary>
        private void SlowJobs(long h)
        {
            foreach (ConstructionJob job in _jobs.Where(j => j.State == JobState.InProgress))
            {
                double worked = h + 1 - Math.Max(job.StartTime, h);
                if (worked <= 0) continue;
                double delay = (1 - JobSpeed(job)) * worked;
                if (delay <= 1e-12) continue;
                job.EndTime += delay;
                job.DelayHours += delay;
            }
        }

        /// <summary>지금 공사 속도(0~1) = 모래폭풍 공사 속도 × 공사 배치율(결정 D1·LB1~, 0명이면 멈춤).</summary>
        public double JobSpeed(ConstructionJob job) =>
            (Disasters?.ConstructionSpeed ?? 1) * (Rules.LaborOn ? StaffRatio(job) : 1);

        /// <summary>
        /// 진행 중 작업의 진행률(0~1) at 시각 now: 한 일 ÷ 공사 시간. 한 일 = 착공부터 지난 시간 − 반영된 지연 − 이번 시간에 쌓이는 중인 지연(지금 속도로 추정).
        /// 착공 순간 0%, 완공 시각에 100%. 대기 중이면 0, 작업이 없으면 1.
        /// </summary>
        public double JobProgress(ConstructionJob job, double now)
        {
            if (job == null || job.Hours <= 0) return 1;
            if (job.State != JobState.InProgress) return 0;
            if (now >= job.EndTime) return 1;
            bool endsThisHour = job.EndTime <= CurrentHour + 1 + 1e-9; // 마지막 시간은 늦춰지지 않는다(SlowJobs)
            double pending = endsThisHour ? 0 : Math.Max(0, now - Math.Max(job.StartTime, CurrentHour)) * (1 - JobSpeed(job));
            double done = now - job.StartTime - job.DelayHours - pending;
            return Math.Max(0, Math.Min(1, done / job.Hours));
        }

        private bool CanStart(ConstructionJob job)
        {
            if (Stone + 1e-8 < job.StoneCost) return false;
            switch (job.Type)
            {
                case JobType.Upgrade:
                    return GetFacility(job.FacilityId)?.State == FacilityState.Operating;
                case JobType.Demolish:
                    Facility f = GetFacility(job.FacilityId);
                    return f != null && (f.State == FacilityState.Operating || f.State == FacilityState.Closed) && StorageProblemIfRemoved(f) == null;
                default:
                    return true;
            }
        }
    }
}
