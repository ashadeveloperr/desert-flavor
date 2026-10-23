using System.Collections.Generic;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using UnityEngine;
using UnityEngine.UI;
using static DesertSettlement.Runtime.UI.UiKit;

namespace DesertSettlement.Runtime.UI
{
    /// <summary>
    /// 아래 가운데 건설 도구 막대(UI2, Cities: Skylines·Anno 식): 분류별 도구 버튼(비용·무료 완공 표시),
    /// 가리킨 곳의 불가 이유·마지막 결과 한 줄, 오른쪽에 식량·석재 구매와 샘플 배치.
    /// </summary>
    public sealed class BuildBarView : IUiView
    {
        // 버튼 하나 = 도구(선택·관리) 또는 시설 종류. 시설 버튼은 시설 목록(FacilityCatalog)의 분류·순서로 만든다(2026-10-24 CL2).
        private sealed class ToolButton
        {
            public BuildTool Tool;
            public string Kind;
            public Button Button;
        }

        private const int BuyStep = 10;
        private readonly GameUi _ui;
        private readonly List<ToolButton> _buttons = new List<ToolButton>();
        private readonly Text _status;
        private readonly Button _buyFood, _buyStone;

        public BuildBarView(GameUi ui, Transform canvas)
        {
            _ui = ui;
            RectTransform bar = Panel("Build Bar", canvas).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(1240, 104));

            _status = Label(bar, "", Theme.Small, Theme.Muted, TextAnchor.MiddleCenter);
            _status.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -2), new Vector2(-20, 20));

            HorizontalLayoutGroup row = Row(bar, 14, 0);
            ((RectTransform)row.transform).Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(-20, 70));
            Transform Group(string title)
            {
                VerticalLayoutGroup col = Column(row.transform, 2);
                Label(col.transform, title, Theme.Small, Theme.Accent);
                return Row(col.transform, 4).transform;
            }
            void ToolBtn(Transform parent, BuildTool tool) =>
                _buttons.Add(new ToolButton { Tool = tool, Button = Btn(parent, "", () => { _ui.Input.SelectTool(tool); _ui.RefreshNow(); }, 86, 46, Theme.Small) });

            ToolBtn(Group(""), BuildTool.Select);
            foreach (string group in FacilityCatalog.Groups)
            {
                Transform parent = Group(group);
                foreach (FacilityType type in FacilityCatalog.InGroup(group))
                {
                    string kind = type.Kind;
                    _buttons.Add(new ToolButton
                    {
                        Tool = type.OnEdge ? BuildTool.Canal : BuildTool.Build,
                        Kind = kind,
                        Button = Btn(parent, "", () => { _ui.Input.SelectBuild(kind); _ui.RefreshNow(); }, 86, 46, Theme.Small),
                    });
                }
            }
            Transform manage = Group("관리");
            foreach (BuildTool tool in new[] { BuildTool.Upgrade, BuildTool.Demolish, BuildTool.ToggleClose }) ToolBtn(manage, tool);

            VerticalLayoutGroup buy = Column(row.transform, 2);
            Label(buy.transform, "구매·예시", Theme.Small, Theme.Accent);
            HorizontalLayoutGroup buyRow = Row(buy.transform, 4);
            _buyFood = Btn(buyRow.transform, "", () => { _ui.Session.Settlement.BuyFood(BuyStep); _ui.RefreshNow(); }, 92, 46, Theme.Small);
            _buyStone = Btn(buyRow.transform, "", () => { _ui.Session.Settlement.BuyStone(BuyStep); _ui.RefreshNow(); }, 92, 46, Theme.Small);
            Btn(buyRow.transform, "샘플\n배치", () => { _ui.Session.RequestSampleLayout(); _ui.RefreshNow(); }, 60, 46, Theme.Small);
        }

        public void Reset() => Refresh();

        public void Refresh()
        {
            Settlement s = _ui.Session.Settlement;
            if (s == null) return;
            foreach (ToolButton b in _buttons)
            {
                string kind = b.Kind;
                string label = kind != null ? FacilityCatalog.NameOf(kind) : BuildInputController.ToolName(b.Tool);
                BuildingLevel lv = kind != null ? s.Rules.Config.GetLevel(kind, 1) : null;
                int free = kind != null ? s.FreeBuildsRemaining(kind) : 0;
                if (free > 0) label += "\n" + Paint($"무료 {free}", Theme.Good);
                else if (lv != null) label += "\n" + Paint($"석 {lv.Stone:0} · {s.Rules.JobHours(lv.Hours)}h", Theme.Muted);
                b.Button.SetText(label);
                b.Button.SetOn(_ui.Input.Tool == b.Tool && _ui.Input.CurrentKind == kind);
            }
            _buyFood.SetText($"식량 +{BuyStep}\n{Paint(s.NextPrice(true).ToString("0.0") + "/개", Theme.Muted)}");
            _buyStone.SetText($"석재 +{BuyStep}\n{Paint(s.NextPrice(false).ToString("0.0") + "/개", Theme.Muted)}");

            string hover = _ui.Input.HoverMessage, last = _ui.Input.LastResult;
            _status.text = !string.IsNullOrEmpty(hover) ? Paint("✕ " + hover, Theme.Danger)
                : !string.IsNullOrEmpty(last) ? last
                : "우클릭·Esc = 선택 도구   ·   선택 도구로 시설을 누르면 오른쪽에 정보와 물길";
        }
    }
}
