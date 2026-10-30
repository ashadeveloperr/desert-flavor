using System;
using System.IO;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;
using NUnit.Framework;
using UnityEngine;

namespace DesertSettlement.Core.Tests
{
    /// <summary>시장(MK1~MK8·AS1~AS3): 상인 구매·판매 2차 하락·가격 회복·시장 수 배율·매각 일괄 매도·넘친 석재 자동 판매.</summary>
    public class MarketTests
    {
        private static EconomyConfig _config;
        private static readonly HexCoord MarketA = new HexCoord(-1, 0), MarketB = new HexCoord(0, 1), MarketC = new HexCoord(-1, 1);

        [OneTimeSetUp]
        public void LoadConfig()
        {
            _config = TestData.Economy();
        }

        /// <summary>무료 완공 배치로 시장을 바로 운영 상태로 짓는다.</summary>
        private static Settlement NewSettlement(int markets, Action<SettlementRules> tweak = null)
        {
            var map = new HexMap(3);
            map.GetTile(HexCoord.Zero).Terrain = TerrainType.WaterSource;
            SettlementRules rules = SettlementRules.FromConfig(_config);
            rules.FreeBuilds[BuildingKinds.Market] = markets;
            rules.StartingCash = 5000;
            tweak?.Invoke(rules);
            var s = new Settlement(map, rules);
            HexCoord[] tiles = { MarketA, MarketB, MarketC };
            for (int i = 0; i < markets; i++) Assert.IsTrue(s.RequestBuild(BuildingKinds.Market, tiles[i]).Success);
            Assert.AreEqual(markets, s.MarketCount);
            return s;
        }

        [Test]
        public void Config_HasChosenValues()
        {
            TradeSettings t = _config.Purchase.Trade;
            Assert.AreEqual(0.08, t.RecoveryPerHour, 1e-12);
            Assert.AreEqual(1.5, t.NoMarketPriceMultiplier, 1e-12);
            Assert.AreEqual(0.5, _config.Purchase.Food.SellRatio, 1e-12);
            Assert.AreEqual(100, _config.Purchase.Food.SellLimit, 1e-12);
            Assert.AreEqual(60, _config.Purchase.Stone.SellLimit, 1e-12);
            Assert.IsNotNull(_config.GetLevel(BuildingKinds.Market, 1));
            Assert.AreEqual(1, _config.MaxLevel(BuildingKinds.Market), "개량 없음");
        }

        [Test]
        public void RecoveryFactor_GrowsByShrinkingSteps()
        {
            TradeSettings t = _config.Purchase.Trade;
            Assert.AreEqual(1, t.RecoveryFactor(0), 1e-12);
            Assert.AreEqual(1, t.RecoveryFactor(1), 1e-12);
            Assert.AreEqual(1.2, t.RecoveryFactor(2), 1e-12);
            Assert.AreEqual(1.35, t.RecoveryFactor(3), 1e-12);
            Assert.AreEqual(1.4625, t.RecoveryFactor(4), 1e-12);
            Assert.Less(t.RecoveryFactor(50), 1.8 + 1e-9, "상한 ×1.8");
        }

        [Test]
        public void WithoutMarket_PeddlerCostsMore_AndSellingIsImpossible()
        {
            Settlement s = NewSettlement(0);
            Assert.IsFalse(s.HasMarket);
            Assert.AreEqual(2 * 1.5, s.NextPrice(true), 1e-9);
            Assert.AreEqual(4 * 1.5, s.NextPrice(false), 1e-9);
            Assert.AreEqual(0, s.NextSellPrice(true));
            Assert.AreEqual(0, s.SellFood(5));
            Assert.AreEqual(0, s.LiquidateStock(), "시장이 없으면 매각 때도 재고를 팔지 않는다");
        }

        [Test]
        public void Selling_DropsQuadratically_AndRecoversEachHour()
        {
            Settlement s = NewSettlement(1);
            Assert.AreEqual(1.0, s.NextSellPrice(true), 1e-9, "식량 판매 기준가 = 2 × 0.5");
            Assert.AreEqual(2.0, s.NextSellPrice(false), 1e-9);
            double food = s.Food, cash = s.Cash;
            Assert.AreEqual(40, s.SellFood(40));
            Assert.AreEqual(food - 40, s.Food, 1e-9);
            double expected = 0;
            for (int i = 0; i < 40; i++) expected += 1 - Math.Pow(i / 100.0, 2);
            Assert.AreEqual(cash + expected, s.Cash, 1e-9, "1개씩 (1 − (압력/100)²)");
            Assert.AreEqual(1 - 0.16, s.NextSellPrice(true), 1e-9);

            s.AdvanceHour();
            Assert.AreEqual(40 * 0.92, s.FoodSellPressure, 1e-9, "시장 1개: 시간당 8% 회복");
        }

        [Test]
        public void Buying_CompoundsOnCurrentPrice_AndRecoversWithMoreMarketsFaster()
        {
            Settlement one = NewSettlement(1), three = NewSettlement(3);
            foreach (Settlement s in new[] { one, three })
            {
                Assert.AreEqual(10, s.BuyStone(10));
                Assert.AreEqual(4 * Math.Pow(1.03, 10), s.NextPrice(false), 1e-9, "살 때마다 현재 가격 × 1.03");
                s.AdvanceHour();
            }
            Assert.AreEqual(10 * (1 - 0.08), one.StoneBuyPressure, 1e-9);
            Assert.AreEqual(10 * (1 - 0.08 * 1.35), three.StoneBuyPressure, 1e-9, "시장 3개 ×1.35");
            Assert.Greater(three.NextPrice(false), 4 - 1e-9, "기준가보다 싸지지 않는다");
        }

        [Test]
        public void SellingStopsAtZeroPrice()
        {
            Settlement s = NewSettlement(1, r => r.BaseFoodStorage = 500);
            s.BuyFood(300); // 식량 = 곡물(결정 FD6), 재고 300
            Assert.AreEqual(100, s.SellFood(300), "압력이 한도(100)에 이르면 값 0 → 멈춤");
            Assert.AreEqual(0, s.NextSellPrice(true), 1e-9);
        }

        [Test]
        public void LiquidateStock_SellsEverythingAlongTheCurve()
        {
            Settlement s = NewSettlement(1);
            double food = Math.Floor(s.Food), stone = Math.Floor(s.Stone), cash = s.Cash;
            double expected = 0;
            for (int i = 0; i < food; i++) expected += 1.0 * Math.Max(0, 1 - Math.Pow(i / 100.0, 2));
            for (int i = 0; i < stone; i++) expected += 2.0 * Math.Max(0, 1 - Math.Pow(i / 60.0, 2));
            Assert.AreEqual(expected, s.LiquidateStock(), 1e-9);
            Assert.AreEqual(cash + expected, s.Cash, 1e-9);
            Assert.Less(s.Food, 1);
            Assert.AreEqual(Math.Max(0, stone - 60), s.Stone, 1e-9, "석재는 한도 60개째에 값이 0이 되어 나머지는 남는다");
        }

        [Test]
        public void OriginalRules_WithoutTrade_KeepOldPrices()
        {
            Settlement s = NewSettlement(0, r => r.Trade = null);
            Assert.IsTrue(s.HasMarket, "원본 v0.8: 어디서나 구매");
            Assert.AreEqual(2, s.NextPrice(true), 1e-9);
            s.BuyFood(5);
            s.AdvanceHour();
            Assert.AreEqual(2 * Math.Pow(1.02, 5), s.NextPrice(true), 1e-9, "회복 없음");
            Assert.AreEqual(0, s.SellFood(5), "판매 없음");
        }

        [Test]
        public void StoneRefundOverflow_AutoSellsWithMarket_AndIsDiscardedWithout()
        {
            // 결정 AS1~AS3: 취소로 돌려받은 석재가 한도를 넘으면 시장이 있을 때 판매가 곡선으로 자동 판매, 없으면 버림.
            foreach (int markets in new[] { 1, 0 })
            {
                Settlement s = NewSettlement(markets);
                CommandResult build = s.RequestBuild(BuildingKinds.Warehouse, new HexCoord(1, -1));
                Assert.IsTrue(build.Success);
                ConstructionJob job = s.GetJob(build.JobId);
                Assert.AreEqual(JobState.InProgress, job.State, "명령 순간 착공으로 석재를 썼다");
                s.BuyStone((int)Math.Round(s.StoneCapacity - s.Stone));
                Assert.AreEqual(s.StoneCapacity, s.Stone, 1e-9, "석재 창고를 다시 채움");

                double refund = job.StoneCost * s.Rules.CancelRefundRatio, cash = s.Cash, discarded = s.StoneDiscarded;
                Assert.IsTrue(s.CancelJob(job.Id).Success);
                Assert.AreEqual(s.StoneCapacity, s.Stone, 1e-9);
                if (markets > 0)
                {
                    PurchasePrice p = _config.Purchase.Stone;
                    double expected = p.BaseCost * p.SellRatio * (refund - refund * refund * refund / (3 * p.SellLimit * p.SellLimit));
                    Assert.AreEqual(refund, s.StoneAutoSold, 1e-9, "넘친 환급 석재를 판다");
                    Assert.AreEqual(cash + expected, s.Cash, 1e-9, "손 판매와 같은 곡선의 값");
                    Assert.AreEqual(discarded, s.StoneDiscarded, 1e-12, "버리지 않는다");
                    Assert.AreEqual(expected, s.AdvanceHour().AutoSaleIncome, 1e-9, "명령 순간의 자동 판매도 다음 시간 기록에 들어간다");
                }
                else
                {
                    Assert.AreEqual(0, s.StoneAutoSold, 1e-12);
                    Assert.AreEqual(cash, s.Cash, 1e-9);
                    Assert.AreEqual(discarded + refund, s.StoneDiscarded, 1e-9, "시장이 없으면 버린다");
                    Assert.AreEqual(refund, s.AdvanceHour().Discarded, 1e-9, "버린 양도 다음 시간 기록에 들어간다");
                }
            }
        }
    }
}
