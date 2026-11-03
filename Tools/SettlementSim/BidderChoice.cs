using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Auction;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Progression;

namespace SettlementSim
{
    /// <summary>
    /// 측정 봇의 매각·행선지 선택(결정 BM1~·SH1, 장비 상점 대신).
    /// - 매각: 현금 × (1 + 모디파이어 점수)가 가장 큰 입찰자. 점수는 '이 모디파이어가 앞으로의 정착지 순이익을 몇 % 올릴까'의 대략값.
    /// - 행선지: 받은 모디파이어로 막지 못하는 약점이 적은 후보, 같으면 강점이 많은 후보.
    /// </summary>
    internal static class BidderChoice
    {
        /// <summary>점수 가중치(BM 측정용). 0이면 늘 최고 현금.</summary>
        public static double ScoreWeight = 1;

        /// <summary>약점 id → 그 약점을 줄이는 입찰자 모디파이어 id.</summary>
        internal static readonly Dictionary<string, string> CounterOf = new Dictionary<string, string>
        {
            ["softGround"] = "stoneCost", ["thinAir"] = "efficiencyCap", ["dryAquifer"] = "wellSupply",
            ["sandWind"] = "canalLoss", ["salineSoil"] = "grain",
        };

        private static readonly Dictionary<string, double> Scores = new Dictionary<string, double>
        {
            ["ore"] = 0.06, ["output"] = 0.04, ["generalUpkeep"] = 0.02, ["upkeep"] = 0.03, ["stoneCost"] = 0.03, ["buildTime"] = 0.03,
            ["constructionSlot"] = 0.05, ["wellSupply"] = 0.03, ["canalLoss"] = 0.02, ["startingCash"] = 0.05, ["efficiencyCap"] = 0.02,
            ["stoneOutput"] = 0.02, ["grain"] = 0.02, ["arrival"] = 0.02, ["houseArrival"] = 0.02, ["ration"] = 0.02,
        };

        public static double Score(BidderModifierDefinition m) => m == null ? 0 : Scores.TryGetValue(m.Id, out double v) ? v : 0.01;

        public static int PickBid(AuctionEvent a)
        {
            List<AuctionBid> bids = a.Bids.ToList();
            return bids.IndexOf(bids.OrderByDescending(b => b.Cash * (1 + ScoreWeight * Score(b.Modifier))).First());
        }

        /// <summary>행선지를 고르고 다음 정착지에 들어간다.</summary>
        public static void ChooseNext(Campaign c) => c.StartNextSettlement(PickOffer(c));

        /// <summary>고를 행선지 후보 번호: 받은 모디파이어로 막지 못하는 약점이 적은 곳, 같으면 강점이 많은 곳.</summary>
        public static int PickOffer(Campaign c)
        {
            var owned = new HashSet<string>(c.Modifiers.Select(m => m.Id));
            return c.NextOffers()
                .OrderBy(o => o.Weaknesses.Count(w => !CounterOf.ContainsKey(w.Id) || !owned.Contains(CounterOf[w.Id])))
                .ThenByDescending(o => o.Strengths.Count()).First().Index;
        }

        /// <summary>id로 모디파이어 효과(측정용).</summary>
        public static EconomyEffects Effects(EconomyConfig config, string id) => config.BidderModifiers.Definitions.Single(d => d.Id == id).Effects;
    }
}
