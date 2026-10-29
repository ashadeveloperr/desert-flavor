using System;
using System.Linq;
using DesertSettlement.Core.Config;

namespace DesertSettlement.Core.Economy
{
    /// <summary>
    /// 식량·석재 구매·판매와 시장 가격(결정 MK1~MK9). 수치는 설정 JSON <c>purchase</c>.
    /// - 구매: 살 때마다 구매 압력 +1, 단가 = 기준가 × 배율^압력. 운영 중인 시장이 없으면 떠돌이 상인 배율이 붙는다.
    /// - 판매: 시장이 있어야 한다. 팔 때마다 판매 압력 +1, 단가 = 판매 기준가 × (1 − (압력 ÷ 한도)²).
    /// - 두 압력은 매시간(<see cref="RecoverPrices"/>, 경제 틱 끝) 일정 비율로 0을 향해 줄고, 시장이 많을수록 빨리 준다.
    /// - 시장 규칙이 없으면(<c>Rules.Trade == null</c>, 원본 v0.8 회귀 비교) 시장 없이도 상인 할증 없이 산다.
    /// 플레이어 명령 진입점(<c>BuyFood</c>·<c>SellFood</c> …)은 Settlement.Commands.cs, Do*는 Settlement.Actions.cs.
    /// </summary>
    public sealed partial class Settlement
    {
        // 통계(화면·측정용). 가격 계산에는 아래 압력만 쓴다.
        public int FoodBought { get; private set; }
        public int StoneBought { get; private set; }
        public double FoodSpent { get; private set; }
        public double StoneSpent { get; private set; }
        public int FoodSold { get; private set; }
        public int StoneSold { get; private set; }
        public double FoodEarned { get; private set; }
        public double StoneEarned { get; private set; }
        /// <summary>구매 압력(살 때마다 +1, 매시간 회복). 다음 구매 단가 = 기준가 × 배율^압력.</summary>
        public double FoodBuyPressure { get; private set; }
        public double StoneBuyPressure { get; private set; }
        /// <summary>판매 압력(팔 때마다 +1, 매시간 회복). 판매 단가 = 판매 기준가 × (1 − (압력 ÷ 한도)²).</summary>
        public double FoodSellPressure { get; private set; }
        public double StoneSellPressure { get; private set; }

        /// <summary>운영 중인 시장 수.</summary>
        public int MarketCount => OperatingOf(BuildingKinds.Market).Count();

        /// <summary>시장이 있어 판매할 수 있고 상인 할증 없이 살 수 있는지. 시장 규칙이 없으면(원본 v0.8) 언제나 true.</summary>
        public bool HasMarket => Rules.Trade == null || MarketCount > 0;

        /// <summary>다음 1단위 구매 가격. 시장이 없으면 떠돌이 상인 배율.</summary>
        public double NextPrice(bool food)
        {
            PurchasePrice p = food ? Rules.Config.Purchase.Food : Rules.Config.Purchase.Stone;
            double peddler = HasMarket ? 1 : Rules.Trade.NoMarketPriceMultiplier;
            return p.BaseCost * Rules.PurchasePriceMultiplier * peddler * Math.Pow(p.IncrementMultiplier, food ? FoodBuyPressure : StoneBuyPressure);
        }

        /// <summary>다음 1단위 판매 가격. 시장이 없으면(또는 시장 규칙이 없으면) 0.</summary>
        public double NextSellPrice(bool food)
        {
            if (Rules.Trade == null || !HasMarket) return 0;
            PurchasePrice p = food ? Rules.Config.Purchase.Food : Rules.Config.Purchase.Stone;
            if (p.SellLimit <= 0) return 0;
            double x = (food ? FoodSellPressure : StoneSellPressure) / p.SellLimit;
            return p.BaseCost * p.SellRatio * Math.Max(0, 1 - x * x);
        }

        private int Buy(int units, bool isFood)
        {
            if (units <= 0) return 0;
            double stock = isFood ? Food : Stone;
            double cap = isFood ? FoodCapacity : StoneCapacity;
            int count = Math.Min(units, Math.Max(0, (int)(cap - stock)));
            int bought = 0;
            for (int i = 0; i < count; i++)
            {
                double price = NextPrice(isFood);
                if (Cash < price) break;
                Cash -= price;
                if (isFood) { Food += 1; FoodBought++; FoodBuyPressure += 1; FoodSpent += price; }
                else { Stone += 1; StoneBought++; StoneBuyPressure += 1; StoneSpent += price; }
                bought++;
            }
            return bought;
        }

        /// <summary>시장 판매(1단위씩, 판매가 2차 하락). 운영 중인 시장이 있을 때만, 값이 0이 되면 멈춘다. 실제 판매 수를 돌려준다.</summary>
        private int Sell(int units, bool isFood)
        {
            if (units <= 0 || !HasMarket) return 0;
            int sold = 0;
            for (int i = 0; i < units; i++)
            {
                if ((isFood ? Food : Stone) < 1 - 1e-9) break;
                double price = NextSellPrice(isFood);
                if (price <= 1e-9) break;
                Cash += price;
                if (isFood) { Food -= 1; FoodSold++; FoodSellPressure += 1; FoodEarned += price; }
                else { Stone -= 1; StoneSold++; StoneSellPressure += 1; StoneEarned += price; }
                sold++;
            }
            return sold;
        }

        /// <summary>
        /// 남은 식량·석재를 지금 판매가 곡선대로 전부 판다(매각 정산). 시장이 없거나 시장 규칙이 없으면 0.
        /// 플레이어 명령이 아니라 매각의 결과라 명령 기록에 남기지 않는다(불러오기는 매각 명령을 다시 실행해 같은 결과).
        /// </summary>
        public double LiquidateStock()
        {
            if (Rules.Trade == null || !HasMarket) return 0;
            double before = Cash;
            Sell(int.MaxValue, isFood: true);
            Sell(int.MaxValue, isFood: false);
            return Cash - before;
        }

        /// <summary>시장 규칙이 있을 때 매시간 구매·판매 압력을 일정 비율(시장 수 배율 적용)로 0을 향해 줄인다.</summary>
        private void RecoverPrices()
        {
            TradeSettings t = Rules.Trade;
            if (t == null) return;
            double keep = 1 - Math.Max(0, Math.Min(1, t.RecoveryPerHour * t.RecoveryFactor(MarketCount)));
            FoodBuyPressure *= keep;
            StoneBuyPressure *= keep;
            FoodSellPressure *= keep;
            StoneSellPressure *= keep;
        }
    }
}
