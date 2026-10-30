using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Auction;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using UnityEngine;
using UnityEngine.UI;
using static DesertSettlement.Runtime.UI.UiKit;

namespace DesertSettlement.Runtime.UI
{
    /// <summary>
    /// 왼쪽 패널(UI2, Frostpunk 식 알림 목록): 경매 일정·예고 입찰자, 파산 경고, 입찰자 혜택, 재해(예고·진행), 그 아래 작업 대기열.
    /// 목록은 내용이 바뀔 때만 다시 만든다(UI5). 비교는 그릴 글자·색·버튼 그 자체로 해서, 비교 키에 값을 빠뜨려 갱신이 안 되는 일이 없다(결정 UK2).
    /// </summary>
    public sealed class AlertsView : IUiView
    {
        private readonly GameUi _ui;
        private readonly Transform _alerts, _queue;
        private readonly Text _queueTitle;
        private string _alertKey, _queueKey;

        public AlertsView(GameUi ui, Transform canvas)
        {
            _ui = ui;
            RectTransform panel = Panel("Left Panel", canvas).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -72), new Vector2(330, 0));
            VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6;
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            FitHeight(panel.gameObject); // 내용만큼만 세로로 늘어난다

            Label(panel, "알림", Theme.Title, Theme.Accent);
            _alerts = Column(panel, 4).transform;
            _queueTitle = Label(panel, "", Theme.Title, Theme.Accent);
            _queue = Column(panel, 3).transform;
        }

        public void Reset()
        {
            _alertKey = _queueKey = null;
            Refresh();
        }

        public void Refresh()
        {
            Settlement s = _ui.Session.Settlement;
            AuctionManager auctions = _ui.Session.Auctions;
            if (s == null || auctions == null) return;

            List<(string text, Color color)> alerts = BuildAlerts(s, auctions);
            string key = string.Join("\n", alerts.Select(a => a.text + "#" + ColorUtility.ToHtmlStringRGBA(a.color)));
            if (key != _alertKey)
            {
                _alertKey = key;
                Clear(_alerts);
                foreach ((string text, Color color) in alerts) Label(_alerts, text, Theme.Body, color);
            }

            _queueTitle.text = $"작업 대기열  {s.JobsInProgress}/{s.Rules.ConstructionSlots} 진행";
            List<ConstructionJob> jobs = s.Jobs.ToList();
            List<ConstructionJob> queued = jobs.Where(j => j.State == JobState.Queued).ToList();
            // 줄마다 글자와 버튼을 정하는 값(작업 번호·대기 순서·상태)을 그대로 비교한다.
            string qkey = string.Join("\n", jobs.Select(j => $"{JobText(s, j)}|{j.Id}|{queued.IndexOf(j)}|{j.State}"));
            if (qkey == _queueKey) return;
            _queueKey = qkey;
            Clear(_queue);
            if (jobs.Count == 0) Label(_queue, "비어 있음 — 아래 도구로 건설을 예약하세요", Theme.Small, Theme.Muted);
            foreach (ConstructionJob job in jobs) AddJobRow(s, job, queued.IndexOf(job));
        }

        /// <summary>대기열 한 줄의 글자: 종류·시설·목표 레벨·상태(완공 시각 / 석재 부족 / 대기).</summary>
        private static string JobText(Settlement s, ConstructionJob job)
        {
            string type = job.Type == JobType.Build ? "건설" : job.Type == JobType.Upgrade ? "개량" : "철거";
            string state = job.State == JobState.InProgress ? Paint($"~{job.DeadlineHour}h", Theme.Good)
                : s.Stone + 1e-8 < job.StoneCost ? Paint($"석재 {job.StoneCost:0} 부족", Theme.Warn) : Paint("대기", Theme.Muted);
            // 공사 인원이 모자라면 그만큼 느려진다(결정 LB1~).
            if (job.State == JobState.InProgress && s.StaffRatio(job) < 0.999)
                state += " " + Paint($"인원 {FacilityInfo.People(s.LastLabor.OfJob(job.Id))}/{FacilityInfo.People(s.WorkersNeeded(job))}", Theme.Warn);
            return $"{type} {FacilityInfo.KindName(job.Kind)} L{job.TargetLevel}  {state}";
        }

        private void AddJobRow(Settlement s, ConstructionJob job, int queuedIndex)
        {
            HorizontalLayoutGroup row = Row(_queue, 4);
            Text t = Label(row.transform, JobText(s, job), Theme.Small);
            t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            int id = job.Id;
            if (job.State == JobState.Queued && queuedIndex > 0)
                Btn(row.transform, "▲", () => { s.MoveQueuedJob(id, queuedIndex - 1); _ui.RefreshNow(); }, 26, 22, Theme.Small);
            Btn(row.transform, job.State == JobState.InProgress ? "취소 50%" : "취소", () => { s.CancelJob(id); _ui.RefreshNow(); },
                job.State == JobState.InProgress ? 64 : 40, 22, Theme.Small);
        }

        private static List<(string, Color)> BuildAlerts(Settlement s, AuctionManager auctions)
        {
            var list = new List<(string, Color)>();
            if (auctions.NextAuctionHour.HasValue)
            {
                long left = auctions.NextAuctionHour.Value - s.CurrentHour;
                list.Add(($"경매까지 {left}시간 ({auctions.NextAuctionHour}시간째)", Theme.Text));
                if (auctions.AnnouncedBidders.Count > 0)
                {
                    // 2026-10-26 AH1: 입찰자 수 = 경매장 1개일 때 2명 + 경매장이 하나 늘 때마다 1명(최대 attendeesMax).
                    int max = Mathf.Min(s.Rules.Config.Auction.AttendeesMax, s.Rules.Config.Auction.Bidders.Count);
                    string more = auctions.AttendeeCount < max ? " · 경매장을 더 지으면 +1명" : "";
                    list.Add(($"  예고 입찰자 {auctions.AttendeeCount}명(경매장 {auctions.OperatingAuctionHalls}개{more}): " +
                              string.Join(", ", auctions.AnnouncedBidders.Select(b => b.Name)), Theme.Muted));
                }
                if (auctions.Holds > 0) list.Add(($"  보류 {auctions.Holds}번 — 시세 ×{auctions.HoldFactor:0.00}", Theme.Warn));
            }
            else
            {
                list.Add(($"경매장 완공 {s.Rules.Config.ConfirmedRules.AuctionRepeatsGameHours}시간 뒤 첫 경매", Theme.Muted));
            }
            if (auctions.HoursUntilBankruptcy.HasValue) list.Add(($"⚠ 자금 음수 — {auctions.HoursUntilBankruptcy}시간 뒤 파산", Theme.Danger));
            else if (CashRunsOutIn(s) is double runOut)
            {
                // 자금 경고(RC4): 지난 3시간 추세로 24시간 안에 0이 되면 미리 알린다. 0 이하가 되면 바로 긴급 매각(경매장·광산이 없으면 파산).
                list.Add(($"⚠ 이대로면 약 {runOut:0}시간 뒤 자금 0 — 0 이하가 되면 바로 긴급 매각(−{s.Rules.Config.Auction.EmergencyPenalty:P0})", Theme.Warn));
            }
            foreach (string kind in BuildingKinds.All)
            {
                int free = s.FreeBuildsRemaining(kind);
                if (free <= 0) continue;
                // 주택은 정착지마다 첫 1채가 무료(결정 LB1~). 입찰자 혜택으로 더 받을 수도 있다.
                string why = kind == BuildingKinds.House ? "무료 주택" : "입찰자 혜택";
                list.Add(($"{why}: 완공된 {FacilityInfo.KindName(kind)} {free}개 바로 배치 (석재·공사 없음)", Theme.Good));
            }
            if (s.Rules.LaborOn)
            {
                // 노동력(결정 LB1~): 떠나는 주택, 일손 부족.
                int leaving = s.Facilities.Count(s.IsLeaving);
                if (leaving > 0)
                    list.Add(($"⚠ 주민이 떠나는 중 — 주택 {leaving}채 ({(s.FoodSatisfaction < 0.999 ? "식량 부족" : "물 부족: 주택까지 수로·우물")})", Theme.Danger));
                LaborAssignment labor = s.LastLabor;
                if (labor.Assigned + 0.5 < labor.Needed)
                    list.Add(($"⚠ 일손 부족 — 필요 {labor.Needed:0} · 배치 {labor.Assigned:0}명 (주택을 더 짓거나 우선 고용으로 순서 조정)", Theme.Warn));
            }
            DisasterManager d = s.Disasters;
            if (d != null)
            {
                foreach (DisasterEvent e in d.Visible)
                {
                    if (e.State == DisasterState.Warned) list.Add(($"⚠ {e.Name} 예고 — {e.StartHour - s.CurrentHour}시간 뒤", Theme.Warn));
                    else if (e.State == DisasterState.Active && e.DurationHours > 0)
                        list.Add(($"⚠ {e.Name} — {e.PlannedEndHour - s.CurrentHour}시간 남음 · {FacilityInfo.TimedDisasterText(s, e)}", Theme.Danger));
                }
                EpidemicState ep = d.Epidemic;
                // 감염·회복 모형(결정 EV1~): 아픈 주민은 일하지 못하고, 병상만큼 병원에서 빨리 낫는다.
                if (ep.Active)
                    list.Add(($"☣ 전염병 {(ep.Weakening ? "↓ 약해지는 중" : "↑ 확산 중")} — 아픈 주민 {ep.Sick:0}명({ep.SickShare:P0}, 일 못 함) · " +
                              $"병원 {ep.Hospitalized:0}/{d.HospitalBeds:0}병상 · 면역 {ep.Immune:0}명",
                        ep.Weakening ? Theme.Warn : Theme.Danger));
            }
            return list;
        }

        /// <summary>지난 3시간 자금 추세로 0이 될 때까지 남은 시간(24시간 안일 때만). 자금이 늘거나 이미 0 이하면 null.</summary>
        private static double? CashRunsOutIn(Settlement s)
        {
            int n = s.History.Count;
            if (n < 4 || s.Cash <= 0) return null;
            double perHour = (s.History[n - 1].Cash - s.History[n - 4].Cash) / 3.0;
            if (perHour >= -1e-6) return null;
            double hours = s.Cash / -perHour;
            return hours <= 24 ? hours : (double?)null;
        }
    }
}
