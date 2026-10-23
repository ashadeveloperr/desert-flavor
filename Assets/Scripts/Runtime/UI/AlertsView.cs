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
    /// 목록은 내용이 바뀔 때만 다시 만든다(UI5).
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
            string key = string.Join("|", alerts.Select(a => a.text));
            if (key != _alertKey)
            {
                _alertKey = key;
                Clear(_alerts);
                foreach ((string text, Color color) in alerts) Label(_alerts, text, Theme.Body, color);
            }

            _queueTitle.text = $"작업 대기열  {s.JobsInProgress}/{s.Rules.ConstructionSlots} 진행";
            List<ConstructionJob> jobs = s.Jobs.ToList();
            string qkey = string.Join("|", jobs.Select(j => $"{j.Id}{j.State}{j.DeadlineHour}{(s.Stone + 1e-8 < j.StoneCost ? "!" : "")}"));
            if (qkey == _queueKey) return;
            _queueKey = qkey;
            Clear(_queue);
            if (jobs.Count == 0) Label(_queue, "비어 있음 — 아래 도구로 건설을 예약하세요", Theme.Small, Theme.Muted);
            List<ConstructionJob> queued = jobs.Where(j => j.State == JobState.Queued).ToList();
            foreach (ConstructionJob job in jobs) AddJobRow(s, job, queued.IndexOf(job));
        }

        private void AddJobRow(Settlement s, ConstructionJob job, int queuedIndex)
        {
            HorizontalLayoutGroup row = Row(_queue, 4);
            string type = job.Type == JobType.Build ? "건설" : job.Type == JobType.Upgrade ? "개량" : "철거";
            string state = job.State == JobState.InProgress ? Paint($"~{job.DeadlineHour}h", Theme.Good)
                : s.Stone + 1e-8 < job.StoneCost ? Paint($"석재 {job.StoneCost:0} 부족", Theme.Warn) : Paint("대기", Theme.Muted);
            Text t = Label(row.transform, $"{type} {FacilityInfo.KindName(job.Kind)} L{job.TargetLevel}  {state}", Theme.Small);
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
                    list.Add(("  예고 입찰자: " + string.Join(", ", auctions.AnnouncedBidders.Select(b => b.Name)), Theme.Muted));
                if (auctions.Holds > 0) list.Add(($"  보류 {auctions.Holds}번 — 시세 ×{auctions.HoldFactor:0.00}", Theme.Warn));
            }
            else
            {
                list.Add(($"경매장 완공 {s.Rules.Config.ConfirmedRules.AuctionRepeatsGameHours}시간 뒤 첫 경매", Theme.Muted));
            }
            if (auctions.HoursUntilBankruptcy.HasValue) list.Add(($"⚠ 자금 음수 — {auctions.HoursUntilBankruptcy}시간 뒤 파산", Theme.Danger));
            foreach (string kind in BuildingKinds.All)
            {
                int free = s.FreeBuildsRemaining(kind);
                if (free > 0) list.Add(($"입찰자 혜택: 완공된 {FacilityInfo.KindName(kind)} {free}개 무료 배치", Theme.Good));
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
                if (ep.Active) list.Add(($"☣ 전염병 — 심각도 {ep.Severity:P0} · 저항력 {ep.Resistance:P0} · 생산 −{ep.Damage:P0}", Theme.Danger));
            }
            return list;
        }
    }
}
