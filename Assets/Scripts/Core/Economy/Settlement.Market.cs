using System;
using System.Linq;
using DesertSettlement.Core.Config;

namespace DesertSettlement.Core.Economy
{
    /// <summary>
    /// 식량(곡물)·석재 구매·판매, 그 밖의 재화 판매와 시장 가격(결정 MK1~MK9·FD6). 수치는 설정 JSON <c>purchase</c>·<c>goods</c>.
    /// - 구매: 살 때마다 구매 압력 +1, 단가 = 기준가 × 배율^압력. 운영 중인 시장이 없으면 떠돌이 상인 배율이 붙는다.
    /// - 판매: 시장이 있어야 한다. 팔 때마다 판매 압력 +1, 단가 = 판매 기준가 × (1 − (압력 ÷ 한도)²).
    /// - 두 압력은 매시간(<see cref="RecoverPrices"/>, 경제 틱 끝) 일정 비율로 0을 향해 줄고, 시장이 많을수록 빨리 준다.
    /// - 넘침 자동 판매(결정 AS1~AS3): 식품 창고·석재 한도를 넘는 몫은 시장이 있으면 같은 판매가 곡선으로 저절로 팔고,
    ///   시장이 없거나 값이 바닥이면 버린다(<see cref="AutoSellGoods"/>·<see cref="AutoSellStone"/>). 명령이 아닌 정산 규칙이라 저장 기록에 남지 않는다.
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
            return p.BaseCost * p.SellRatio * Rules.Effects.SellPriceMultiplier * Math.Max(0, 1 - x * x);
        }

        /// <summary>식량 구매·판매(곡물)와 석재. 식량 = 곡물(결정 FD6).</summary>
        private int Buy(int units, bool isFood)
        {
            if (units <= 0) return 0;
            double room = isFood ? FoodCapacity - PantryUsed : StoneCapacity - Stone;
            int count = Math.Min(units, Math.Max(0, (int)room));
            int bought = 0;
            for (int i = 0; i < count; i++)
            {
                double price = NextPrice(isFood);
                if (Cash < price) break;
                Cash -= price;
                if (isFood) { AddGoods(Goods.Grain, 1); FoodBought++; FoodBuyPressure += 1; FoodSpent += price; }
                else { Stone += 1; StoneBought++; StoneBuyPressure += 1; StoneSpent += price; }
                bought++;
            }
            return bought;
        }

        /// <summary>시장 판매(1단위씩, 판매가 2차 하락). 운영 중인 시장이 있을 때만, 값이 0이 되면 멈춘다. 실제 판매 수를 돌려준다. 식량 = 곡물.</summary>
        private int Sell(int units, bool isFood)
        {
            if (units <= 0 || !HasMarket) return 0;
            int sold = 0;
            for (int i = 0; i < units; i++)
            {
                if ((isFood ? Stock(Goods.Grain) : Stone) < 1 - 1e-9) break;
                double price = NextSellPrice(isFood);
                if (price <= 1e-9) break;
                Cash += price;
                if (isFood) { AddGoods(Goods.Grain, -1); FoodSold++; FoodSellPressure += 1; FoodEarned += price; }
                else { Stone -= 1; StoneSold++; StoneSellPressure += 1; StoneEarned += price; }
                sold++;
            }
            return sold;
        }

        // ── 곡물 밖의 재화(고기·과일·빵·맥주·와인·향신료): 시장이 있을 때 팔기만(결정 FD6). 값은 설정 goods.*.sellPrice·sellLimit. ──

        private readonly double[] _goodsSellPressure = new double[GoodsCatalog.All.Length];
        private readonly double[] _goodsEarned = new double[GoodsCatalog.All.Length];

        /// <summary>재화 판매로 번 돈(통계).</summary>
        public double EarnedFrom(Goods g) => g == Goods.Grain ? FoodEarned : _goodsEarned[(int)g];

        /// <summary>재화를 시장에 팔 수 있는지(곡물은 <see cref="NextSellPrice(bool)"/>, 나머지는 판매가가 설정돼 있을 때).</summary>
        public bool CanSell(Goods g) => g == Goods.Grain ? NextSellPrice(true) > 1e-9 : NextSellPrice(g) > 1e-9;

        /// <summary>재화 1단위 판매가(곡물이면 식량 판매가). 시장이 없거나 판매가가 없으면 0.</summary>
        public double NextSellPrice(Goods g)
        {
            if (g == Goods.Grain) return NextSellPrice(true);
            if (Rules.Trade == null || !HasMarket) return 0;
            GoodsSettings s = GoodsCatalog.Settings(Rules.Config, g);
            if (s.SellPrice <= 0 || s.SellLimit <= 0) return 0;
            double x = _goodsSellPressure[(int)g] / s.SellLimit;
            return s.SellPrice * Rules.Effects.SellPriceMultiplier * Math.Max(0, 1 - x * x);
        }

        /// <summary>곡물 밖의 재화 판매(1단위씩). 곡물은 <see cref="Sell(int, bool)"/>.</summary>
        private int SellOther(Goods g, int units)
        {
            if (g == Goods.Grain) return Sell(units, isFood: true);
            if (units <= 0 || !HasMarket) return 0;
            int sold = 0;
            for (int i = 0; i < units; i++)
            {
                if (Stock(g) < 1 - 1e-9) break;
                double price = NextSellPrice(g);
                if (price <= 1e-9) break;
                Cash += price;
                AddGoods(g, -1);
                _goodsSellPressure[(int)g] += 1;
                _goodsEarned[(int)g] += price;
                sold++;
            }
            return sold;
        }

        /// <summary>
        /// 남은 재화·석재를 지금 판매가 곡선대로 전부 판다(매각 정산). 시장이 없거나 시장 규칙이 없으면 0.
        /// 플레이어 명령이 아니라 매각의 결과라 명령 기록에 남기지 않는다(불러오기는 매각 명령을 다시 실행해 같은 결과).
        /// </summary>
        public double LiquidateStock()
        {
            if (Rules.Trade == null || !HasMarket) return 0;
            double before = Cash;
            foreach (Goods g in GoodsCatalog.All) SellOther(g, int.MaxValue);
            Sell(int.MaxValue, isFood: false);
            return Cash - before;
        }

        // ── 넘침 자동 판매(결정 AS1~AS3) ──

        /// <summary>넘침 자동 판매로 번 돈 합(이 정착지, 통계·화면).</summary>
        public double AutoSaleIncome { get; private set; }
        /// <summary>넘침 자동 판매로 판 재화(재화별)·석재 양.</summary>
        public double AutoSoldOf(Goods g) => _autoSold[(int)g];
        public double StoneAutoSold { get; private set; }
        private readonly double[] _autoSold = new double[GoodsCatalog.All.Length];

        // 다음 시간 기록(HourRecord)에 넣을 몫: 정산 중 자동 판매와 명령 순간(철거·취소 석재 환급)의 자동 판매·버림을 함께 센다.
        private double _autoIncomeSinceRecord, _discardedSinceRecord;

        /// <summary>
        /// 판매가 곡선(기준가 × (1 − (압력 ÷ 한도)²))에서 압력 pressure부터 amount를 연속으로 팔 때 실제로 판 양과 수입.
        /// 곡선을 압력 구간에서 적분하고, 압력이 한도에 닿으면(값 0) 더 팔지 않는다. 1단위씩 파는 손 판매와 같은 곡선이다.
        /// </summary>
        private static (double Sold, double Income) SellAlongCurve(double basePrice, double limit, double pressure, double amount)
        {
            if (basePrice <= 0 || limit <= 0 || amount <= 1e-12 || pressure >= limit) return (0, 0);
            double end = Math.Min(pressure + amount, limit);
            double sold = end - pressure;
            double income = basePrice * (sold - (end * end * end - pressure * pressure * pressure) / (3 * limit * limit));
            return (sold, Math.Max(0, income));
        }

        /// <summary>
        /// 재화 amount(창고에서 넘친 몫)를 시장에 자동으로 판다. 시장이 없거나 시장 규칙이 없거나 값이 바닥이면 덜 판다.
        /// 재고에서 판 양만큼 빼고 판매 압력을 올린다. 판 양을 돌려준다.
        /// </summary>
        private double AutoSellGoods(Goods g, double amount)
        {
            if (Rules.Trade == null || !HasMarket) return 0;
            amount = Math.Min(amount, _stock[(int)g]);
            double sold, income;
            if (g == Goods.Grain)
            {
                PurchasePrice p = Rules.Config.Purchase.Food;
                (sold, income) = SellAlongCurve(p.BaseCost * p.SellRatio * Rules.Effects.SellPriceMultiplier, p.SellLimit, FoodSellPressure, amount);
                FoodSellPressure += sold;
            }
            else
            {
                GoodsSettings s = GoodsCatalog.Settings(Rules.Config, g);
                (sold, income) = SellAlongCurve(s.SellPrice * Rules.Effects.SellPriceMultiplier, s.SellLimit, _goodsSellPressure[(int)g], amount);
                _goodsSellPressure[(int)g] += sold;
            }
            _stock[(int)g] -= sold;
            _autoSold[(int)g] += sold;
            AddAutoIncome(income);
            return sold;
        }

        /// <summary>석재 amount(한도에서 넘친 몫)를 시장에 자동으로 판다. 석재 재고는 건드리지 않는다(넘친 몫은 아직 창고 밖). 판 양을 돌려준다.</summary>
        private double AutoSellStone(double amount)
        {
            if (Rules.Trade == null || !HasMarket) return 0;
            PurchasePrice p = Rules.Config.Purchase.Stone;
            (double sold, double income) = SellAlongCurve(p.BaseCost * p.SellRatio * Rules.Effects.SellPriceMultiplier, p.SellLimit, StoneSellPressure, amount);
            StoneSellPressure += sold;
            StoneAutoSold += sold;
            AddAutoIncome(income);
            return sold;
        }

        private void AddAutoIncome(double income)
        {
            Cash += income;
            AutoSaleIncome += income;
            _autoIncomeSinceRecord += income;
        }

        /// <summary>석재 한도를 넘는 몫: 자동 판매하고 남은 것은 버린다(정산·석재 환급 공통).</summary>
        private void SellOrDiscardStone(double overflow)
        {
            if (overflow <= 1e-12) return;
            double discarded = overflow - AutoSellStone(overflow);
            StoneDiscarded += discarded;
            _discardedSinceRecord += discarded;
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
            for (int i = 0; i < _goodsSellPressure.Length; i++) _goodsSellPressure[i] *= keep;
        }
    }
}
