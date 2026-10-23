using System;
using System.Linq;
using DesertSettlement.Core.Auction;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Progression;
using UnityEngine;
using UnityEngine.UI;
using static DesertSettlement.Runtime.UI.UiKit;

namespace DesertSettlement.Runtime.UI
{
    /// <summary>가운데 창의 내용: 경매·상점·파산. 예전 임시 화면(AuctionPanel)과 같은 정보를 카드 형태로.</summary>
    public sealed class ModalContent
    {
        private readonly GameUi _ui;
        private readonly Transform _root, _footer;
        private readonly Action _changed;

        /// <summary>상점에서 마지막으로 한 일(구매·강화 결과).</summary>
        public string Message { get; private set; }

        /// <param name="footer">창 아래 고정 줄(스크롤 밖). 상점의 출발 버튼을 둔다.</param>
        public ModalContent(GameUi ui, Transform root, Transform footer, Action changed)
        {
            _ui = ui;
            _root = root;
            _footer = footer;
            _changed = changed;
        }

        private Transform Card(Transform parent)
        {
            RectTransform card = Panel("Card", parent, Theme.PanelLight);
            VerticalLayoutGroup layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4;
            layout.padding = new RectOffset(10, 10, 8, 10);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return card;
        }

        // ───────────── 경매 ─────────────

        public void Auction(AuctionEvent auction)
        {
            if (auction == null) return;
            AuctionManager a = _ui.Session.Auctions;
            bool emergency = auction.Kind == AuctionKind.Emergency;
            Label(_root, emergency ? Paint($"긴급 매각 경매 — 자금이 바닥났습니다 (입찰가 −{auction.Penalty:P0})", Theme.Danger)
                                   : $"정기 경매 — {auction.Hour}시간째", Theme.Big, Theme.Accent);
            string hold = a.Holds > 0 ? Paint($"  (보류 {a.Holds}번으로 ×{a.HoldFactor:0.00} 포함)", Theme.Warn) : "";
            Label(_root, $"당일 시세 ×{auction.Market:0.000}{hold}    정착지 자금 {_ui.Session.Settlement.Cash:0}", Theme.Body, Theme.Muted);
            if (emergency && a.HoursUntilBankruptcy.HasValue)
                Label(_root, Paint($"보류하면 자금이 음수인 채로 {a.HoursUntilBankruptcy}시간 뒤 파산합니다.", Theme.Danger));

            double best = auction.Bids.Count > 0 ? auction.Bids.Max(b => b.Cash) : 0;
            for (int i = 0; i < auction.Bids.Count; i++) BidCard(auction.Bids[i], i, Math.Abs(auction.Bids[i].Cash - best) < 1e-6);

            long next = auction.Hour + _ui.Session.Config.ConfirmedRules.AuctionRepeatsGameHours;
            double penalty = _ui.Session.Config.Auction.HoldMarketPenalty;
            string holdText = emergency ? "보류 (파산 카운트다운 계속)"
                : $"보류 — 계속 운영, 다음 경매 {next}시간째" + (penalty > 0 ? $"  ·  이후 시세 −{penalty:P0}" : "");
            Btn(_root, holdText, () => { _ui.Session.HoldAuction(); _changed(); }, -1, 36);
        }

        private void BidCard(AuctionBid bid, int index, bool best)
        {
            Transform card = Card(_root);
            HorizontalLayoutGroup head = Row(card, 8);
            Text name = Label(head.transform, $"{bid.Bidder.Name}{(best ? Paint("  최고 현금", Theme.Good) : "")}", Theme.Title, Theme.Accent);
            name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            Label(head.transform, $"현금 {Paint(bid.Cash.ToString("0"), Theme.Text)}", Theme.Title, Theme.Muted, TextAnchor.MiddleRight)
                .gameObject.AddComponent<LayoutElement>().preferredWidth = 160;

            string perk = FacilityInfo.PerkText(bid.NextSettlementFoodBonus, bid.NextSettlementFreeBuilds);
            Label(card, $"총액 {bid.TotalOffer:0}{perk}", Theme.Small, Theme.Muted);
            ValuationResult v = bid.Valuation;
            string dev = v.DevelopmentOre > 0.01 ? $" (개발 중 +{v.DevelopmentOre:0.0})" : "";
            string land = Math.Abs(v.OpenLandMultiplier - 1) > 1e-9 ? $" × 빈 땅 {v.OpenLandMultiplier:0.00}" : "";
            Label(card, $"가치 {v.Value:0} = 광물 {v.OreLast24h:0.0}{dev} → {v.BaseValue:0} × 식량 {v.FoodMultiplier:0.00} × 석재 {v.StoneMultiplier:0.00} × 인프라 {v.InfraMultiplier:0.00}{land}", Theme.Small, Theme.Muted);
            if (v.OreRawLast24h - v.OreLast24h > 0.05)
                Label(card, $"막 지은 시설은 실적을 덜 쳐줌: 광물 {v.OreRawLast24h:0.0} 중 {v.OreLast24h:0.0} 인정", Theme.Small, Theme.Warn);
            Btn(card, $"{bid.Bidder.Name}에게 매각", () => { _ui.Session.SellTo(index); _changed(); }, -1, 32);
        }

        // ───────────── 상점 ─────────────

        public void Shop(Campaign c)
        {
            SaleRecord sale = c.Sales.LastOrDefault();
            if (sale != null)
            {
                Label(_root, $"정착지 {sale.SettlementNumber} 매각 — {sale.BidderName}{(sale.Emergency ? " (긴급)" : "")}", Theme.Big, Theme.Accent);
                Label(_root, $"매각 대금 {sale.SaleCash:0}{FacilityInfo.PerkText(sale.FoodBonus, sale.FreeBuilds)}", Theme.Body, Theme.Muted);
            }
            bool record = RecordBook.Submit(c);
            Label(_root, (record ? Paint("새 기록!  ", Theme.Good) : "") + RecordBook.Describe(c), Theme.Small, Theme.Muted);
            Label(_root, $"보유 자금 {Paint(c.Cash.ToString("0"), Theme.Text)}   ·   현장 장비 슬롯 {c.Loadout.Count}/{Campaign.EquipmentSlots} (장착한 것만 다음 정착지에서 효과)", Theme.Body, Theme.Muted);

            Label(_root, "영구 장비", Theme.Title, Theme.Accent);
            foreach (EquipmentDefinition e in c.Shop) EquipmentRow(c, e);
            if (!string.IsNullOrEmpty(Message)) Label(_root, Message, Theme.Body, Theme.Accent);

            Label(_root, $"다음 지역 (정착지 {c.SettlementNumber + 1}) — 지형은 들어가면 보입니다", Theme.Title, Theme.Accent);
            var offers = c.NextOffers();
            foreach (RegionOffer offer in offers) OfferCard(offer);
            // 출발 버튼은 아래 고정 줄에(UX3): 장비 목록이 길어도 스크롤 없이 보인다.
            foreach (RegionOffer offer in offers)
            {
                int index = offer.Index;
                string label = offers.Count > 1
                    ? (offer.IsFixedMap ? $"후보 {index + 1}로 출발" : $"후보 {index + 1}로 출발 (난이도 {offer.Params.D:0.00})")
                    : "다음 정착지로 출발";
                Btn(_footer, label, () => { Message = null; _ui.Session.GoToNextSettlement(index); _changed(); }, -1, 38);
            }
        }

        private void EquipmentRow(Campaign c, EquipmentDefinition e)
        {
            Transform card = Card(_root);
            HorizontalLayoutGroup row = Row(card, 6);
            bool owned = c.Owns(e.Id);
            string title = owned ? $"{e.Name}  {Paint($"Lv{c.EquipmentLevel(e.Id)}/{c.MaxEquipmentLevel(e)}", Theme.Accent)}" : $"{e.Name}  {Paint(e.Price.ToString("0"), Theme.Muted)}";
            Text t = Label(row.transform, title + "\n" + Paint(owned ? c.EffectsOf(e).Describe() : (e.Description ?? e.Effects.Describe()), Theme.Muted), Theme.Small);
            t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            string id = e.Id;
            if (!owned)
            {
                Btn(row.transform, "구매", () => Act(_ui.Session.BuyEquipment(id), $"{e.Name} 구매"), 70, 34);
                return;
            }
            double? cost = c.UpgradeCost(id);
            if (cost.HasValue) Btn(row.transform, $"강화 {cost.Value:0}", () => Act(c.UpgradeEquipment(id), $"{e.Name} Lv{c.EquipmentLevel(id)}로 강화"), 90, 34);
            bool on = c.IsEquipped(id);
            Button equip = Btn(row.transform, on ? "장착됨" : "장착", () => Act(c.SetEquipped(id, !on), null), 70, 34);
            equip.SetOn(on);
        }

        private void Act(CommandResult r, string ok)
        {
            Message = r.Success ? ok : r.Message;
            _changed();
        }

        private void OfferCard(RegionOffer offer)
        {
            Transform card = Card(_root);
            if (offer.IsFixedMap)
            {
                Label(card, $"고정 맵: {offer.FixedMap.Name}", Theme.Title);
            }
            else
            {
                DifficultyParams p = offer.Params;
                Label(card, $"후보 {offer.Index + 1} — 난이도 {p.D:0.00}", Theme.Title, Theme.Accent);
                Label(card, $"반경 {p.Radius} · 우물 {p.WellCount} · 광맥 {p.OreCount} · 장애물 {p.ObstacleDensity:P0}", Theme.Small, Theme.Muted);
                if (offer.Modifiers.Count == 0) Label(card, "특이 사항 없음", Theme.Small, Theme.Muted);
                foreach (RegionModifierDefinition m in offer.Modifiers)
                {
                    bool weak = m.Kind == RegionModifierKind.Weakness;
                    Label(card, $"{Paint(weak ? "약점" : "강점", weak ? Theme.Warn : Theme.Good)} {m.Name}: {m.Effects.Describe()}", Theme.Small);
                }
            }
        }

        // ───────────── 파산 ─────────────

        public void Bankrupt(Campaign c)
        {
            Label(_root, "파산했습니다", Theme.Big, Theme.Danger);
            Label(_root, "이유: " + c.BankruptcyReason);
            Label(_root, $"정착지 {c.SettlementNumber}까지 진행, 매각 {c.Sales.Count}회", Theme.Body, Theme.Muted);
            bool record = RecordBook.Submit(c);
            if (record) Label(_root, "새 기록!", Theme.Title, Theme.Good);
            Label(_root, RecordBook.Describe(c), Theme.Body, Theme.Text);
            Btn(_root, "새 캠페인 시작", () => { _ui.Session.RestartCampaign(); _changed(); }, -1, 40);
        }
    }
}
