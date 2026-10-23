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
    /// - 매각 후: 정산 요약, 영구 장비 상점, '다음 정착지로'
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
            GUILayout.Label($"당일 시세 ×{auction.Market:0.000}   정착지 자금 {_session.Settlement.Cash:0.0}");
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
                GUILayout.Label($"   가치 {v.Value:0.0} = 광물 {v.OreLast24h:0.0}{dev} 기반 {v.BaseValue:0.0} × 식량 {v.FoodMultiplier:0.00} × 석재 {v.StoneMultiplier:0.00} × 인프라 {v.InfraMultiplier:0.00}");
                if (GUILayout.Button($"{bid.Bidder.Name}에게 매각")) _session.SellTo(i);
                GUILayout.EndVertical();
            }

            GUILayout.Space(6);
            long next = (_session.Auctions.CurrentAuction?.Hour ?? 0) + _session.Config.ConfirmedRules.AuctionRepeatsGameHours;
            string holdText = emergency ? "보류 (파산 카운트다운 계속)" : $"보류 — 계속 운영, 다음 경매 {next}시간째";
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
            GUILayout.Space(8);
            GUILayout.Label("영구 장비 상점");
            foreach (EquipmentDefinition e in campaign.Shop)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label($"{e.Name} — {e.Price:0}  (우물 방출 ×{e.WellSupplyMultiplier:0.00})");
                if (campaign.Owns(e.Id)) GUILayout.Label("보유");
                else if (GUILayout.Button("구매", GUILayout.Width(60)))
                {
                    var r = _session.BuyEquipment(e.Id);
                    _shopMessage = r.Success ? $"{e.Name} 구매" : r.Message;
                }
                GUILayout.EndHorizontal();
            }
            if (!string.IsNullOrEmpty(_shopMessage)) GUILayout.Label(_shopMessage);
            GUILayout.Space(8);
            // 다음 정착지 난이도는 들어가는 순간의 보유 자금으로 정해진다(장비를 사면 내려간다).
            DifficultyParams next = campaign.PreviewNextDifficulty();
            GUILayout.Label(next == null
                ? "다음 정착지: 고정 맵"
                : $"다음 정착지: 생성 맵 — 난이도 {next.D:0.00} (반경 {next.Radius}, 우물 {next.WellCount}, 광맥 {next.OreCount}, 장애물 {next.ObstacleDensity:P0})");
            if (GUILayout.Button($"다음 정착지로 (정착지 {campaign.SettlementNumber + 1})", GUILayout.Height(32)))
            {
                _shopMessage = null;
                _session.GoToNextSettlement();
            }
        }

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
