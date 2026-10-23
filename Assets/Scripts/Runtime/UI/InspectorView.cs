using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using UnityEngine;
using UnityEngine.UI;
using static DesertSettlement.Runtime.UI.UiKit;

namespace DesertSettlement.Runtime.UI
{
    /// <summary>
    /// 오른쪽 패널(UI2): 선택한 시설의 이름·레벨·상태, 정보(생산·물·물길·재해 등 <see cref="FacilityInfo.Describe"/>),
    /// 개량·철거·폐쇄(수로는 닫기/열기)·선택 해제 버튼. 선택이 없으면 숨는다. 맵에는 물길 띠가 함께 보인다.
    /// </summary>
    public sealed class InspectorView : IUiView
    {
        private readonly GameUi _ui;
        private readonly RectTransform _panel;
        private readonly Text _title;
        private readonly Transform _lines;
        private readonly Button _upgrade, _demolish, _close;
        private string _key;

        public InspectorView(GameUi ui, Transform canvas)
        {
            _ui = ui;
            _panel = Panel("Inspector", canvas).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-8, -100), new Vector2(380, 0));
            VerticalLayoutGroup layout = _panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6;
            layout.padding = new RectOffset(12, 12, 10, 12);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            FitHeight(_panel.gameObject);

            HorizontalLayoutGroup head = Row(_panel, 6);
            _title = Label(head.transform, "", Theme.Title, Theme.Accent);
            _title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            Btn(head.transform, "✕", () => { _ui.Input.SelectedFacilityId = -1; _ui.RefreshNow(); }, 28, 24);

            _lines = Column(_panel, 2).transform;

            HorizontalLayoutGroup actions = Row(_panel, 6);
            _upgrade = Btn(actions.transform, "개량", () => Do(f => _ui.Session.Settlement.RequestUpgrade(f.Id)), 100);
            _demolish = Btn(actions.transform, "철거", () => Do(f => _ui.Session.Settlement.RequestDemolish(f.Id)), 100);
            _close = Btn(actions.transform, "폐쇄", () => Do(f => _ui.Session.Settlement.SetClosed(f.Id, f.State == FacilityState.Operating)), 110);
        }

        private void Do(System.Func<Facility, CommandResult> command)
        {
            Settlement s = _ui.Session.Settlement;
            Facility f = s?.GetFacility(_ui.Input.SelectedFacilityId);
            if (f == null) return;
            CommandResult r = command(f);
            if (!r.Success) _ui.Input.ReportResult(r.Message);
            _key = null;
            _ui.RefreshNow();
        }

        public void Reset()
        {
            _key = null;
            Refresh();
        }

        public void Refresh()
        {
            Settlement s = _ui.Session.Settlement;
            Facility f = s?.GetFacility(_ui.Input.SelectedFacilityId);
            _panel.gameObject.SetActive(f != null);
            if (f == null) return;

            _title.text = $"{FacilityInfo.KindName(f.Kind)} L{f.Level}  {Paint("#" + f.Id, Theme.Muted)}  {StateChip(f.State)}";
            List<string> lines = FacilityInfo.Describe(s, f, _ui.View.JobProgress(s, f)).Skip(1).ToList(); // 첫 줄(이름)은 제목과 겹친다
            lines.Add(Paint($"투입 석재 {f.StoneInvested:0} (철거 시 {f.StoneInvested * s.Rules.DemolishRefundRatio:0} 환급)", Theme.Muted));
            string key = string.Join("\n", lines);
            if (key != _key)
            {
                _key = key;
                Clear(_lines);
                foreach (string line in lines) Label(_lines, line, Theme.Small);
            }

            Set(_upgrade, s.CanUpgrade(f.Id));
            Set(_demolish, s.CanDemolish(f.Id));
            bool closable = !s.Rules.CannotClose.Contains(f.Kind) && (f.State == FacilityState.Operating || f.State == FacilityState.Closed);
            _close.gameObject.SetActive(closable);
            bool canal = f.Kind == BuildingKinds.CanalEdge;
            _close.SetText(f.State == FacilityState.Closed ? (canal ? "수로 열기" : "재개") : (canal ? "수로 닫기" : "폐쇄"));
        }

        private static void Set(Button b, CommandResult check) => b.interactable = check.Success;

        private static string StateChip(FacilityState state)
        {
            switch (state)
            {
                case FacilityState.Operating: return Paint("운영 중", Theme.Good);
                case FacilityState.Closed: return Paint("폐쇄", Theme.Warn);
                case FacilityState.Constructing: return Paint("공사 중", Theme.Water);
                case FacilityState.Upgrading: return Paint("개량 중", Theme.Water);
                default: return Paint("철거 중", Theme.Danger);
            }
        }
    }
}
