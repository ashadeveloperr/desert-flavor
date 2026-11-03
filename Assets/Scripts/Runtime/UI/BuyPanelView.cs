using System;
using DesertSettlement.Core.Economy;
using UnityEngine;
using UnityEngine.UI;
using static DesertSettlement.Runtime.UI.UiKit;

namespace DesertSettlement.Runtime.UI
{
    /// <summary>
    /// 왼쪽 아래 시장 패널(2026-10-27 LY2 이슈 #5 → MK 이슈 #18): 곡물(식량)·석재 구매·판매, 그 밖의 재화 판매(결정 FD6)와 샘플 배치.
    /// 운영 중인 시장이 없으면 떠돌이 상인 구매만(값 ×1.5) 되고 판매 버튼은 꺼진다. 재화 판매는 '◀ 재화 ▶'로 고른 재화를 판다.
    /// 좁은 화면에서는 도구 막대와 같은 비율로 줄어든다(<see cref="BuildBarView.BottomScale"/>).
    /// </summary>
    public sealed class BuyPanelView : IUiView
    {
        /// <summary>패널 폭. 바꾸면 도구 막대와 겹치지 않는지(<see cref="BuildBarView.BottomScale"/>) 함께 본다.</summary>
        public const float Width = 248;
        private const int BuyStep = 10;
        private readonly GameUi _ui;
        private readonly RectTransform _panel;
        private readonly Text _title;
        private readonly Button _buyFood, _buyStone, _sellFood, _sellStone, _pick, _sellGoods;
        /// <summary>판매할 재화(곡물 밖, 화면 상태라 저장하지 않음).</summary>
        private Goods _goods = Goods.Meat;

        public BuyPanelView(GameUi ui, Transform canvas)
        {
            _ui = ui;
            float m = BuildBarView.Margin;
            _panel = Panel("Buy Panel", canvas).Place(Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(m, m), new Vector2(Width, 184));
            VerticalLayoutGroup col = Column(_panel, 2, 8);
            Stretch((RectTransform)col.transform, Vector2.zero);
            _title = Label(col.transform, "시장", Theme.Small, Theme.Accent);
            HorizontalLayoutGroup row = Row(col.transform, 4);
            _buyFood = Btn(row.transform, "", () => { _ui.Session.Settlement.BuyFood(BuyStep); _ui.RefreshNow(); }, 84, 46, Theme.Small);
            _buyStone = Btn(row.transform, "", () => { _ui.Session.Settlement.BuyStone(BuyStep); _ui.RefreshNow(); }, 84, 46, Theme.Small);
            Btn(row.transform, "샘플\n배치", () => { _ui.Session.RequestSampleLayout(); _ui.RefreshNow(); }, 56, 46, Theme.Small);
            HorizontalLayoutGroup sellRow = Row(col.transform, 4);
            _sellFood = Btn(sellRow.transform, "", () => { _ui.Session.Settlement.SellFood(BuyStep); _ui.RefreshNow(); }, 114, 46, Theme.Small);
            _sellStone = Btn(sellRow.transform, "", () => { _ui.Session.Settlement.SellStone(BuyStep); _ui.RefreshNow(); }, 114, 46, Theme.Small);
            HorizontalLayoutGroup goodsRow = Row(col.transform, 4);
            _pick = Btn(goodsRow.transform, "", NextGoods, 84, 46, Theme.Small);
            _sellGoods = Btn(goodsRow.transform, "", () => { _ui.Session.Settlement.SellGoods(_goods, BuyStep); _ui.RefreshNow(); }, 144, 46, Theme.Small);
        }

        /// <summary>판매할 재화를 다음 것으로(곡물은 위 버튼이 맡는다).</summary>
        private void NextGoods()
        {
            int n = GoodsCatalog.All.Length;
            do _goods = (Goods)(((int)_goods + 1) % n); while (_goods == Goods.Grain);
            Refresh();
        }

        public void Reset() => Refresh();

        public void Refresh()
        {
            SetScale(_panel, BuildBarView.BottomScale(_ui.CanvasWidth));
            Settlement s = _ui.Session.Settlement;
            if (s == null) return;
            int markets = s.MarketCount;
            _title.text = s.Rules.Trade == null ? "구매·예시"
                : markets == 0 ? $"시장 없음 · 상인 값 ×{s.Rules.Trade.NoMarketPriceMultiplier:0.##}"
                : $"시장 {markets}개 · 값 회복 ×{s.Rules.Trade.RecoveryFactor(markets):0.##}";
            _buyFood.SetText($"곡물 +{BuyStep}\n{Paint(s.NextPrice(true).ToString("0.0") + "/개", Theme.Muted)}");
            _buyStone.SetText($"석재 +{BuyStep}\n{Paint(s.NextPrice(false).ToString("0.0") + "/개", Theme.Muted)}");
            bool canSell = s.Rules.Trade != null && s.HasMarket;
            _sellFood.interactable = canSell && s.Stock(Goods.Grain) >= 1;
            _sellStone.interactable = canSell && s.Stone >= 1;
            _sellFood.SetText($"곡물 −{BuyStep} 팔기\n{Paint(canSell ? s.NextSellPrice(true).ToString("0.00") + "/개" : "판매 불가", Theme.Muted)}");
            _sellStone.SetText($"석재 −{BuyStep} 팔기\n{Paint(canSell ? s.NextSellPrice(false).ToString("0.00") + "/개" : "판매 불가", Theme.Muted)}");
            _pick.SetText($"◀ {GoodsCatalog.Name(_goods)} ▶\n{Paint($"재고 {s.Stock(_goods):0}", Theme.Muted)}");
            double price = s.NextSellPrice(_goods);
            _sellGoods.interactable = canSell && price > 1e-9 && s.Stock(_goods) >= 1;
            _sellGoods.SetText($"{GoodsCatalog.Name(_goods)} −{BuyStep} 팔기\n{Paint(canSell ? price.ToString("0.00") + "/개" : "판매 불가", Theme.Muted)}");
        }
    }
}
