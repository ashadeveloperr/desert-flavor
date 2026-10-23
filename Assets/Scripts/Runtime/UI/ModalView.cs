using System.Linq;
using DesertSettlement.Core.Auction;
using DesertSettlement.Core.Progression;
using UnityEngine;
using UnityEngine.UI;
using static DesertSettlement.Runtime.UI.UiKit;

namespace DesertSettlement.Runtime.UI
{
    /// <summary>
    /// 가운데 창(UI2): 경매(입찰자 카드·매각·보류), 매각 뒤 상점(장비 구매·강화·장착, 다음 지역 후보), 파산.
    /// 뒤를 어둡게 덮어 맵 조작을 막는다(시간은 GameSession이 멈춘다). 내용은 상태가 바뀔 때만 다시 만든다.
    /// 그리는 내용은 <see cref="ModalContent"/>.
    /// </summary>
    public sealed class ModalView : IUiView
    {
        private readonly GameUi _ui;
        private readonly RectTransform _shade;
        private readonly Transform _content;
        private readonly ModalContent _draw;
        private string _key;

        public ModalView(GameUi ui, Transform canvas)
        {
            _ui = ui;
            _shade = Panel("Modal Shade", canvas, new Color(0, 0, 0, 0.45f));
            Stretch(_shade, Vector2.zero);

            RectTransform window = Panel("Modal Window", _shade, Theme.Panel).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 760));
            RectTransform viewport = Rect("Viewport", window);
            Stretch(viewport, new Vector2(12, 12));
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.001f); // 스크롤 휠을 받기 위한 투명 면

            VerticalLayoutGroup col = Column(viewport, 8, 6);
            var content = (RectTransform)col.transform;
            content.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 0));
            FitHeight(content.gameObject);
            _content = content;

            var scroll = window.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.scrollSensitivity = 30;
            _draw = new ModalContent(ui, _content, () => { _key = null; _ui.RefreshNow(); });
        }

        public void Reset()
        {
            _key = null;
            Refresh();
        }

        public void Refresh()
        {
            Campaign c = _ui.Session.Campaign;
            AuctionManager a = _ui.Session.Auctions;
            bool open = c != null && (c.State != CampaignState.InSettlement || (a != null && a.IsOpen));
            _shade.gameObject.SetActive(open);
            if (!open) { _key = null; return; }

            string key = $"{c.State}|{a?.CurrentAuction?.Hour}|{c.Cash:0.00}|{c.OwnedEquipment.Count}|{string.Join(",", c.Loadout)}|" +
                         $"{string.Join(",", c.OwnedEquipment.Select(e => e + c.EquipmentLevel(e)))}|{_draw.Message}";
            if (key == _key) return;
            _key = key;
            Clear(_content);
            if (c.State == CampaignState.Bankrupt) _draw.Bankrupt(c);
            else if (c.State == CampaignState.Shopping) _draw.Shop(c);
            else _draw.Auction(a.CurrentAuction);
        }
    }
}
