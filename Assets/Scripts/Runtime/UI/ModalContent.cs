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
    /// <summary>가운데 창의 내용: 경매·행선지·상점·파산(카드 형태).</summary>
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
            string people = v.PopulationValue > 0.5 ? $" + 주민 {v.PopulationValue:0}" : "";
            Label(card, $"가치 {v.Value:0} = 광물 {v.OreLast24h:0.0}{dev}{people} → {v.BaseValue:0} × 식량 {v.FoodMultiplier:0.00} × 석재 {v.StoneMultiplier:0.00} × 인프라 {v.InfraMultiplier:0.00}{land}", Theme.Small, Theme.Muted);
            if (v.OreRawLast24h - v.OreLast24h > 0.05)
                Label(card, $"막 지은 시설은 실적을 덜 쳐줌: 광물 {v.OreRawLast24h:0.0} 중 {v.OreLast24h:0.0} 인정", Theme.Small, Theme.Warn);
            Btn(card, $"{bid.Bidder.Name}에게 매각", () => { _ui.Session.SellTo(index); _changed(); }, -1, 32);
        }

        // ───────────── 행선지 → 상점 (2026-10-27 DS1~DS3, 이슈 #6) ─────────────
        // 매각 뒤 먼저 행선지 창에서 후보를 고르고(카드 클릭), 그 지역을 보며 상점에서 장비를 산 뒤 출발한다.
        // 고른 후보는 화면 상태일 뿐 Core에는 출발할 때만 넘긴다(저장·불러오기하면 행선지 창부터 다시).

        /// <summary>고른 후보 번호(null = 아직 안 고름). 상점을 떠나면 지운다.</summary>
        public int? ChosenOffer { get; private set; }
        private bool _picking;

        /// <summary>행선지 창을 보여 줄 차례인지(아직 안 골랐거나 '행선지 다시 고르기').</summary>
        public bool ShowsDestination => ChosenOffer == null || _picking;

        /// <summary>상점 상태를 벗어났을 때(출발·새 캠페인·불러오기) 선택을 지운다.</summary>
        public void ClearChoice()
        {
            ChosenOffer = null;
            _picking = false;
            Message = null;
        }

        public void Destination(Campaign c)
        {
            SaleRecord sale = c.Sales.LastOrDefault();
            if (sale != null)
            {
                Label(_root, $"정착지 {sale.SettlementNumber} 매각 — {sale.BidderName}{(sale.Emergency ? " (긴급)" : "")}", Theme.Big, Theme.Accent);
                Label(_root, $"매각 대금 {sale.SaleCash:0}{(sale.StockSaleCash > 0.5 ? $" · 남은 식량·석재 매도 +{sale.StockSaleCash:0}" : "")}{FacilityInfo.PerkText(sale.FoodBonus, sale.FreeBuilds)}", Theme.Body, Theme.Muted);
            }
            bool record = RecordBook.Submit(c);
            Label(_root, (record ? Paint("새 기록!  ", Theme.Good) : "") + RecordBook.Describe(c), Theme.Small, Theme.Muted);
            Label(_root, $"보유 자금 {Paint(c.Cash.ToString("0"), Theme.Text)}", Theme.Body, Theme.Muted);

            var offers = c.NextOffers();
            Label(_root, $"다음 지역 (정착지 {c.SettlementNumber + 1}) — {(offers.Count > 1 ? "카드를 눌러 고르세요. " : "")}고른 뒤 상점에서 장비를 준비합니다. 지형은 들어가면 보입니다", Theme.Title, Theme.Accent);
            HorizontalLayoutGroup row = Row(_root, 8);
            row.childForceExpandWidth = true;
            row.childAlignment = TextAnchor.UpperLeft;
            foreach (RegionOffer offer in offers) OfferCard(c, offer, row.transform);
            if (ChosenOffer != null) Btn(_footer, $"← 상점으로 (후보 {ChosenOffer.Value + 1} 그대로)", () => { _picking = false; _changed(); }, -1, 38);
        }

        public void Shop(Campaign c)
        {
            var offers = c.NextOffers();
            int chosen = Math.Min(ChosenOffer ?? 0, offers.Count - 1);
            RegionOffer offer = offers[chosen];
            Label(_root, "상점 — 다음 정착지 준비", Theme.Big, Theme.Accent);
            // 행선지 요약: 장비를 고를 때 약점을 보며 고르게(예: 모래바람 지대 → 수로 방풍막).
            Transform summary = Card(_root);
            Label(summary, "행선지: " + OfferTitle(offer) + $"   ·   운영비 ×{c.PreviewUpkeepMultiplier(offer):0.00}", Theme.Title, Theme.Accent);
            ModifierLines(summary, offer);
            Label(_root, $"보유 자금 {Paint(c.Cash.ToString("0"), Theme.Text)}   ·   현장 장비 슬롯 {c.Loadout.Count}/{Campaign.EquipmentSlots} (장착한 것만 다음 정착지에서 효과)", Theme.Body, Theme.Muted);
            if (!offer.IsFixedMap) Label(_root, "장비를 사면 보유 자금이 줄어 행선지 난이도도 다시 계산됩니다.", Theme.Small, Theme.Muted);

            Label(_root, "영구 장비", Theme.Title, Theme.Accent);
            foreach (EquipmentDefinition e in c.Shop) EquipmentRow(c, e);
            if (!string.IsNullOrEmpty(Message)) Label(_root, Message, Theme.Body, Theme.Accent);

            // 아래 고정 줄(UX3): 장비 목록이 길어도 스크롤 없이 보인다.
            Btn(_footer, "← 행선지 다시 고르기", () => { _picking = true; Message = null; _changed(); }, -1, 38);
            Btn(_footer, offers.Count > 1 ? $"후보 {chosen + 1}로 출발 →" : "다음 정착지로 출발 →", () =>
            {
                ClearChoice();
                _ui.Session.GoToNextSettlement(chosen);
                _changed();
            }, -1, 38);
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
            if (cost.HasValue) Btn(row.transform, $"강화 {cost.Value:0}", () => Act(_ui.Session.UpgradeEquipment(id), $"{e.Name} Lv{c.EquipmentLevel(id)}로 강화"), 90, 34);
            bool on = c.IsEquipped(id);
            Button equip = Btn(row.transform, on ? "장착됨" : "장착", () => Act(_ui.Session.SetEquipped(id, !on), null), 70, 34);
            equip.SetOn(on);
        }

        private void Act(CommandResult r, string ok)
        {
            Message = r.Success ? ok : r.Message;
            _changed();
        }

        /// <summary>행선지 후보 카드(DS2): 카드 어디를 눌러도 고른다. 고른 카드는 강조색.</summary>
        private void OfferCard(Campaign c, RegionOffer offer, Transform parent)
        {
            Transform card = Card(parent);
            var le = card.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 1; // 후보 카드 폭을 똑같이 나눈다(글자 길이와 상관없이)
            le.flexibleWidth = 1;
            int index = offer.Index;
            bool chosen = ChosenOffer == index;
            var image = card.GetComponent<Image>();
            if (chosen) image.color = Theme.ButtonOn;
            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.2f, 1.1f);
            button.colors = colors;
            void Pick() { ChosenOffer = index; _picking = false; Message = null; _changed(); }
            button.onClick.AddListener(Pick);

            Label(card, OfferTitle(offer), Theme.Title, chosen ? Theme.Text : Theme.Accent);
            if (!offer.IsFixedMap)
            {
                DifficultyParams p = offer.Params;
                Label(card, $"반경 {p.Radius} · 우물 {p.WellCount} · 광맥 {p.OreCount} · 장애물 {p.ObstacleDensity:P0}", Theme.Small, Theme.Muted);
            }
            double upkeep = c.PreviewUpkeepMultiplier(offer);
            Label(card, $"운영비 ×{upkeep:0.00}" + (upkeep > 1.005 ? Paint("  (난이도·약점·장착 장비 반영)", Theme.Muted) : ""), Theme.Small, upkeep > 1.005 ? Theme.Warn : Theme.Text);
            ModifierLines(card, offer);
            Btn(card, chosen ? "고름 — 상점으로" : "이 지역으로", Pick, -1, 34).SetOn(chosen);
        }

        private static string OfferTitle(RegionOffer offer) =>
            offer.IsFixedMap ? $"고정 맵: {offer.FixedMap.Name}" : $"후보 {offer.Index + 1} — 난이도 {offer.Params.D:0.00}";

        private static void ModifierLines(Transform card, RegionOffer offer)
        {
            if (offer.IsFixedMap) return;
            if (offer.Modifiers.Count == 0) Label(card, "특이 사항 없음", Theme.Small, Theme.Muted);
            foreach (RegionModifierDefinition m in offer.Modifiers)
            {
                bool weak = m.Kind == RegionModifierKind.Weakness;
                Label(card, $"{Paint(weak ? "약점" : "강점", weak ? Theme.Warn : Theme.Good)} {m.Name}: {m.Effects.Describe()}", Theme.Small);
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
