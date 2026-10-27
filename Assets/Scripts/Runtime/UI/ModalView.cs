using System.Linq;
using DesertSettlement.Core.Auction;
using DesertSettlement.Core.Progression;
using UnityEngine;
using UnityEngine.UI;
using static DesertSettlement.Runtime.UI.UiKit;

namespace DesertSettlement.Runtime.UI
{
    /// <summary>
    /// 가운데 창(UI2): 경매(입찰자 카드·매각·보류), 매각 뒤 행선지 선택(후보 카드) → 상점(장비 구매·강화·장착) → 출발, 파산.
    /// 뒤를 어둡게 덮어 맵 조작을 막는다(시간은 GameSession이 멈춘다). 내용은 상태가 바뀔 때만 다시 만든다.
    /// 그리는 내용은 <see cref="ModalContent"/>.
    /// </summary>
    public sealed class ModalView : IUiView
    {
        private readonly GameUi _ui;
        private readonly RectTransform _shade;
        private readonly RectTransform _window, _viewport, _content, _footer;
        private readonly ScrollRect _scroll;
        private readonly ModalContent _draw;
        /// <summary>창 폭과 최대 높이(1920×1080 기준). 내용이 짧으면 창도 줄어든다(UX2).</summary>
        private const float WindowWidth = 820, MaxHeight = 840, MinHeight = 200, FooterHeight = 52;
        private string _key;
        private Vector2 _fitSize;

        public ModalView(GameUi ui, Transform canvas)
        {
            _ui = ui;
            _shade = Panel("Modal Shade", canvas, new Color(0, 0, 0, 0.45f));
            Stretch(_shade, Vector2.zero);

            RectTransform window = Panel("Modal Window", _shade, Theme.Panel).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WindowWidth, MaxHeight));
            _window = window;
            RectTransform viewport = Rect("Viewport", window);
            Stretch(viewport, new Vector2(12, 12));
            _viewport = viewport;

            // 아래 고정 줄(UX3): 상점의 '출발' 버튼처럼 스크롤과 상관없이 늘 보여야 하는 버튼.
            HorizontalLayoutGroup footer = Row(window, 8);
            _footer = (RectTransform)footer.transform;
            _footer.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(-24, FooterHeight - 14));
            footer.childForceExpandWidth = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.001f); // 스크롤 휠을 받기 위한 투명 면

            VerticalLayoutGroup col = Column(viewport, 8, 6);
            var content = (RectTransform)col.transform;
            content.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 0));
            FitHeight(content.gameObject);
            _content = content;

            var scroll = window.gameObject.AddComponent<ScrollRect>();
            _scroll = scroll;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.scrollSensitivity = 30;
            _draw = new ModalContent(ui, _content, _footer, () => { _key = null; _ui.RefreshNow(); });
        }

        public void Reset()
        {
            _key = null;
            _draw.ClearChoice(); // 새 캠페인·불러오기: 행선지 창부터 다시
            Refresh();
        }

        public void Refresh()
        {
            Campaign c = _ui.Session.Campaign;
            AuctionManager a = _ui.Session.Auctions;
            bool open = c != null && (c.State != CampaignState.InSettlement || (a != null && a.IsOpen));
            _shade.gameObject.SetActive(open);
            if (c == null || c.State != CampaignState.Shopping) _draw.ClearChoice(); // 행선지 선택은 상점에 있는 동안만(DS1)
            if (!open) { _key = null; return; }

            string key = $"{c.State}|{a?.CurrentAuction?.Hour}|{c.Cash:0.00}|{c.OwnedEquipment.Count}|{string.Join(",", c.Loadout)}|" +
                         $"{string.Join(",", c.OwnedEquipment.Select(e => e + c.EquipmentLevel(e)))}|{_draw.Message}|{_draw.ChosenOffer}|{_draw.ShowsDestination}";
            // 화면 크기가 바뀌면(창 크기 조절) 글자 줄바꿈이 달라지므로 창 높이를 다시 잰다(2026-10-27 이슈 #6에서 발견).
            var size = new Vector2(_ui.CanvasWidth, ((RectTransform)_shade.parent).rect.height);
            if (key == _key) { if (size != _fitSize) FitWindow(); return; }
            _key = key;
            Clear(_content);
            Clear(_footer);
            if (c.State == CampaignState.Bankrupt) _draw.Bankrupt(c);
            else if (c.State == CampaignState.Shopping && _draw.ShowsDestination) _draw.Destination(c);
            else if (c.State == CampaignState.Shopping) _draw.Shop(c);
            else _draw.Auction(a.CurrentAuction);
            FitWindow();
        }

        /// <summary>창 높이를 내용에 맞추고(최대 MaxHeight, 넘치면 스크롤), 아래 고정 줄이 있으면 그만큼 비워 둔다.</summary>
        private void FitWindow()
        {
            _fitSize = new Vector2(_ui.CanvasWidth, ((RectTransform)_shade.parent).rect.height);
            bool footer = _footer.childCount > 0;
            _footer.gameObject.SetActive(footer);
            float bottom = footer ? FooterHeight + 8 : 12;
            _viewport.offsetMin = new Vector2(12, bottom);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            float height = Mathf.Clamp(LayoutUtility.GetPreferredHeight(_content) + 12 + bottom, MinHeight, MaxHeight);
            _window.sizeDelta = new Vector2(WindowWidth, height);
            _scroll.verticalNormalizedPosition = 1f;
        }
    }
}
