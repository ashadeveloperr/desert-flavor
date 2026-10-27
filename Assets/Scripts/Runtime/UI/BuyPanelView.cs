using DesertSettlement.Core.Economy;
using UnityEngine;
using UnityEngine.UI;
using static DesertSettlement.Runtime.UI.UiKit;

namespace DesertSettlement.Runtime.UI
{
    /// <summary>
    /// 왼쪽 아래 구매 패널(2026-10-27 LY2, 이슈 #5): 식량·석재 구매와 샘플 배치. 전에는 도구 막대 오른쪽 끝에 있어 화면 밖으로 넘쳤다.
    /// 좁은 화면에서는 도구 막대와 같은 비율로 줄어든다(<see cref="BuildBarView.BottomScale"/>).
    /// </summary>
    public sealed class BuyPanelView : IUiView
    {
        /// <summary>패널 폭. 바꾸면 도구 막대와 겹치지 않는지(<see cref="BuildBarView.BottomScale"/>) 함께 본다.</summary>
        public const float Width = 248;
        private const int BuyStep = 10;
        private readonly GameUi _ui;
        private readonly RectTransform _panel;
        private readonly Button _buyFood, _buyStone;

        public BuyPanelView(GameUi ui, Transform canvas)
        {
            _ui = ui;
            float m = BuildBarView.Margin;
            _panel = Panel("Buy Panel", canvas).Place(Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(m, m), new Vector2(Width, 84));
            VerticalLayoutGroup col = Column(_panel, 2, 8);
            Stretch((RectTransform)col.transform, Vector2.zero);
            Label(col.transform, "구매·예시", Theme.Small, Theme.Accent);
            HorizontalLayoutGroup row = Row(col.transform, 4);
            _buyFood = Btn(row.transform, "", () => { _ui.Session.Settlement.BuyFood(BuyStep); _ui.RefreshNow(); }, 84, 46, Theme.Small);
            _buyStone = Btn(row.transform, "", () => { _ui.Session.Settlement.BuyStone(BuyStep); _ui.RefreshNow(); }, 84, 46, Theme.Small);
            Btn(row.transform, "샘플\n배치", () => { _ui.Session.RequestSampleLayout(); _ui.RefreshNow(); }, 56, 46, Theme.Small);
        }

        public void Reset() => Refresh();

        public void Refresh()
        {
            SetScale(_panel, BuildBarView.BottomScale(_ui.CanvasWidth));
            Settlement s = _ui.Session.Settlement;
            if (s == null) return;
            _buyFood.SetText($"식량 +{BuyStep}\n{Paint(s.NextPrice(true).ToString("0.0") + "/개", Theme.Muted)}");
            _buyStone.SetText($"석재 +{BuyStep}\n{Paint(s.NextPrice(false).ToString("0.0") + "/개", Theme.Muted)}");
        }
    }
}
