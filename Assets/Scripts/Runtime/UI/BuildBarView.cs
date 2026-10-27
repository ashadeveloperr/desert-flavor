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
    /// 가리킨 곳의 불가 이유·마지막 결과 한 줄. 식량·석재 구매와 샘플 배치는 왼쪽 아래 <see cref="BuyPanelView"/>(2026-10-27 LY2, 이슈 #5).
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

        /// <summary>막대 폭. 도구 13개(선택 + 시설 9 + 관리 3)가 약 1226이라 딱 들어간다. 시설을 늘리면 이 값과 <see cref="BottomScale"/>을 함께 본다.</summary>
        public const float BarWidth = 1240;
        /// <summary>화면 가장자리·구역 사이 여백.</summary>
        public const float Margin = 8;
        private readonly GameUi _ui;
        private readonly RectTransform _bar;
        private readonly List<ToolButton> _buttons = new List<ToolButton>();
        private readonly Text _status;

        public BuildBarView(GameUi ui, Transform canvas)
        {
            _ui = ui;
            RectTransform bar = _bar = Panel("Build Bar", canvas).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, Margin), new Vector2(BarWidth, 104));

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

        }

        /// <summary>
        /// 아래 줄(도구 막대 + 왼쪽 아래 구매 패널)의 축소 비율(LY3). 막대는 가운데, 구매 패널은 왼쪽 아래라
        /// 화면 절반 폭에 '막대 절반 + 구매 패널 + 여백'이 들어가야 겹치지 않는다. 16:10(폭 1822)까지는 1.
        /// </summary>
        public static float BottomScale(float canvasWidth) => FitScale(canvasWidth / 2f - 3 * Margin, BarWidth / 2f + BuyPanelView.Width);

        public void Reset() => Refresh();

        public void Refresh()
        {
            SetScale(_bar, BottomScale(_ui.CanvasWidth));
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
            string hover = _ui.Input.HoverMessage, last = _ui.Input.LastResult;
            _status.text = !string.IsNullOrEmpty(hover) ? Paint("✕ " + hover, Theme.Danger)
                : !string.IsNullOrEmpty(last) ? last
                : "우클릭·Esc = 선택 도구   ·   선택 도구로 시설을 누르면 오른쪽에 정보와 물길";
        }
    }
}
