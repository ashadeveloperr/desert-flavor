using System.Linq;
using DesertSettlement.Core.Auction;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Progression;
using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 경매·상점·파산 화면(임시 IMGUI, 화면 가운데 창). 정식 UI를 만들면 지워도 된다.
    /// - 경매가 열리면: 경매 종류·당일 시세, 입찰자별 총액·현금·다음 정착지 혜택·가치 내역, 매각/보류 버튼
    /// - 매각 후: 정산 요약, 영구 장비 상점·현장 장비 슬롯(5), 다음 지역 후보(약점·강점) 중 선택
    /// - 파산: 이유와 '새 캠페인'
    /// 창이 떠 있는 동안 맵 클릭은 막힌다(시간도 GameSession이 멈춘다).
    /// </summary>
    [RequireComponent(typeof(GameSession))]
    public sealed class AuctionPanel : MonoBehaviour
    {
        [SerializeField] private float width = 560f;

        private GameSession _session;
        private Vector2 _scroll;
        private string _shopMessage;

        private void Awake()
        {
            _session = GetComponent<GameSession>();
        }

        private void OnDisable() => HudInputBlocker.Clear(this);

        private void OnGUI()
        {
            if (!_session.IsReady)
            {
                HudInputBlocker.Clear(this);
                return;
            }

            Campaign campaign = _session.Campaign;
            bool modal = campaign.State != CampaignState.InSettlement || _session.Auctions.IsOpen;
            if (!modal)
            {
                HudInputBlocker.Clear(this);
                return;
            }

            float height = Mathf.Min(Screen.height - 40, 520);
            var rect = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
            HudInputBlocker.SetRects(this, new Rect(0, 0, Screen.width, Screen.height)); // 창 밖 클릭도 막는다
            GUILayout.BeginArea(rect, GUI.skin.window);
            _scroll = GUILayout.BeginScrollView(_scroll);

            if (campaign.State == CampaignState.Bankrupt) DrawBankrupt(campaign);
            else if (campaign.State == CampaignState.Shopping) DrawShop(campaign);
            else DrawAuction(_session.Auctions.CurrentAuction);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawAuction(AuctionEvent auction)
        {
            bool emergency = auction.Kind == AuctionKind.Emergency;
            GUILayout.Label(emergency
                ? $"⚠ 긴급 매각 경매 — 자금이 바닥났습니다 (입찰가 −{auction.Penalty:P0})"
                : $"정기 경매 — 정착지 {auction.Hour}시간째");
            string holdNote = _session.Auctions.Holds > 0 ? $" (보류 {_session.Auctions.Holds}번으로 ×{_session.Auctions.HoldFactor:0.00} 포함)" : "";
            GUILayout.Label($"당일 시세 ×{auction.Market:0.000}{holdNote}   정착지 자금 {_session.Settlement.Cash:0.0}");
            if (emergency && _session.Auctions.HoursUntilBankruptcy.HasValue)
            {
                GUILayout.Label($"보류하면 자금이 음수인 채로 {_session.Auctions.HoursUntilBankruptcy}시간 뒤 파산합니다.");
            }
            GUILayout.Space(6);

            for (int i = 0; i < auction.Bids.Count; i++)
            {
                AuctionBid bid = auction.Bids[i];
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"{bid.Bidder.Name}: 총액 {bid.TotalOffer:0.0}");
                GUILayout.Label($"   현금 {bid.Cash:0.0}{FacilityInfo.PerkText(bid.NextSettlementFoodBonus, bid.NextSettlementFreeBuilds)}");
                var v = bid.Valuation;
                string dev = v.DevelopmentOre > 0.01 ? $" (개발 중 자산 +{v.DevelopmentOre:0.0})" : "";
                GUILayout.Label($"   가치 {v.Value:0.0} = 광물 {v.OreLast24h:0.0}{dev} 기반 {v.BaseValue:0.0} × 식량 {v.FoodMultiplier:0.00} × 석재 {v.StoneMultiplier:0.00} × 인프라 {v.InfraMultiplier:0.00}{(System.Math.Abs(v.OpenLandMultiplier - 1) > 1e-9 ? $" × 빈 땅 {v.OpenLandMultiplier:0.00}" : "")}");
                // 가동 숙성(막 완공한 시설은 실적을 덜 쳐줌)·채석장 능력이 가치에 준 영향을 보여 준다.
                var notes = new System.Collections.Generic.List<string>();
                if (v.OreRawLast24h - v.OreLast24h > 0.05) notes.Add($"광물 실적 {v.OreRawLast24h:0.0} 중 {v.OreLast24h:0.0}만 인정(막 지은 시설은 {_session.Settlement.Rules.MaturityFullHours:0}시간 동안 {_session.Settlement.Rules.MaturityStartRatio:P0}→100%)");
                if (v.StoneFromQuarryCapacity) notes.Add("석재 배율은 가동 중인 채석장의 생산 능력 기준");
                if (notes.Count > 0) GUILayout.Label("   " + string.Join(" · ", notes));
                if (GUILayout.Button($"{bid.Bidder.Name}에게 매각")) _session.SellTo(i);
                GUILayout.EndVertical();
            }

            GUILayout.Space(6);
            long next = (_session.Auctions.CurrentAuction?.Hour ?? 0) + _session.Config.ConfirmedRules.AuctionRepeatsGameHours;
            double holdPenalty = _session.Config.Auction.HoldMarketPenalty;
            string penaltyText = holdPenalty > 0 ? $", 보류할 때마다 이후 시세 −{holdPenalty:P0}" : "";
            string holdText = emergency ? "보류 (파산 카운트다운 계속)" : $"보류 — 계속 운영, 다음 경매 {next}시간째{penaltyText}";
            if (GUILayout.Button(holdText, GUILayout.Height(28))) _session.HoldAuction();
        }

        private void DrawShop(Campaign campaign)
        {
            SaleRecord sale = campaign.Sales.LastOrDefault();
            if (sale != null)
            {
                GUILayout.Label($"정착지 {sale.SettlementNumber} 매각 완료 — {sale.BidderName}{(sale.Emergency ? " (긴급 매각)" : "")}");
                GUILayout.Label($"매각 대금 {sale.SaleCash:0.0}{FacilityInfo.PerkText(sale.FoodBonus, sale.FreeBuilds)}");
            }
            GUILayout.Label($"보유 자금 {campaign.Cash:0.0}   (식량·석재는 정착지에 남고, 구매 단가는 다음 정착지에서 초기화)");

            // 1) 영구 장비: 구매 + 현장 장비 슬롯(5개).
            GUILayout.Space(8);
            GUILayout.Label($"영구 장비 — 현장 장비 슬롯 {campaign.Loadout.Count}/{Campaign.EquipmentSlots} (체크한 장비만 다음 정착지에서 효과)");
            foreach (EquipmentDefinition e in campaign.Shop)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                if (campaign.Owns(e.Id))
                {
                    int lv = campaign.EquipmentLevel(e.Id), max = campaign.MaxEquipmentLevel(e);
                    GUILayout.Label($"{e.Name} Lv{lv}/{max}\n<size=11>{campaign.EffectsOf(e).Describe()}</size>", RichLabel);
                    double? cost = campaign.UpgradeCost(e.Id);
                    if (cost.HasValue && GUILayout.Button($"강화 {cost.Value:0}", GUILayout.Width(80)))
                    {
                        var u = campaign.UpgradeEquipment(e.Id);
                        _shopMessage = u.Success ? $"{e.Name} Lv{campaign.EquipmentLevel(e.Id)}로 강화" : u.Message;
                    }
                    bool on = campaign.IsEquipped(e.Id);
                    bool want = GUILayout.Toggle(on, "장착", GUILayout.Width(60));
                    if (want != on)
                    {
                        var r = campaign.SetEquipped(e.Id, want);
                        _shopMessage = r.Success ? null : r.Message;
                    }
                }
                else
                {
                    GUILayout.Label($"{e.Name} — {e.Price:0}\n<size=11>{e.Description ?? e.Effects.Describe()}</size>", RichLabel);
                }
                if (!campaign.Owns(e.Id) && GUILayout.Button("구매", GUILayout.Width(60)))
                {
                    var r = _session.BuyEquipment(e.Id);
                    _shopMessage = r.Success ? $"{e.Name} 구매" : r.Message;
                }
                GUILayout.EndHorizontal();
            }
            if (!string.IsNullOrEmpty(_shopMessage)) GUILayout.Label(_shopMessage);

            // 2) 다음 지역 후보. 난이도는 들어가는 순간의 보유 자금으로 정해진다(장비를 사면 내려간다).
            GUILayout.Space(8);
            GUILayout.Label($"다음 지역 (정착지 {campaign.SettlementNumber + 1}) — 지형 배치는 들어가면 보입니다");
            var offers = campaign.NextOffers();
            foreach (RegionOffer offer in offers)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                if (offer.IsFixedMap)
                {
                    GUILayout.Label($"고정 맵: {offer.FixedMap.Name}");
                }
                else
                {
                    DifficultyParams p = offer.Params;
                    GUILayout.Label($"후보 {offer.Index + 1} — 난이도 {p.D:0.00} (반경 {p.Radius}, 우물 {p.WellCount}, 광맥 {p.OreCount}, 장애물 {p.ObstacleDensity:P0})");
                    if (offer.Modifiers.Count == 0) GUILayout.Label("  특이 사항 없음");
                    foreach (RegionModifierDefinition m in offer.Modifiers)
                    {
                        string tag = m.Kind == RegionModifierKind.Weakness ? "<color=#e08a6a>약점</color>" : "<color=#8ac46a>강점</color>";
                        GUILayout.Label($"  {tag} {m.Name}: {m.Effects.Describe()}", RichLabel);
                    }
                }
                if (GUILayout.Button(offers.Count > 1 ? $"후보 {offer.Index + 1}로 출발" : "다음 정착지로", GUILayout.Height(28)))
                {
                    _shopMessage = null;
                    _session.GoToNextSettlement(offer.Index);
                }
                GUILayout.EndVertical();
            }
        }

        private static GUIStyle _richLabel;
        private static GUIStyle RichLabel => _richLabel ??= new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };

        private void DrawBankrupt(Campaign campaign)
        {
            GUILayout.Label("파산했습니다.");
            GUILayout.Label("이유: " + campaign.BankruptcyReason);
            GUILayout.Label($"정착지 {campaign.SettlementNumber}까지 진행, 매각 {campaign.Sales.Count}회");
            GUILayout.Space(8);
            if (GUILayout.Button("새 캠페인 시작", GUILayout.Height(32))) _session.RestartCampaign();
        }
    }
}
