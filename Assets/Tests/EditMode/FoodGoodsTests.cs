using System;
using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;
using NUnit.Framework;

namespace DesertSettlement.Core.Tests
{
    /// <summary>
    /// 식품 다양화(결정 FD1~FD9)·생활 수준(LS1~LS7)·향신료(SP1~SP3): 가중치 비율로 함께 먹기, 주민이 먼저 먹고 남은 원료로 가공,
    /// 창고가 넘치면 하위 재화부터 자동 판매(시장이 있을 때, AS1)·버림, 재화 판매, 순 식량 가치, 생활 수준 목표·종류 배율·향신료·부족, 이주 배율.
    /// 시설 물은 끄고(설정 사본) 무료 완공으로 세워 재화 규칙만 본다.
    /// </summary>
    public class FoodGoodsTests
    {
        private static readonly HexCoord[] HouseTiles = { new HexCoord(1, 0), new HexCoord(-1, 0), new HexCoord(0, 1) };
        private static readonly HexCoord A = new HexCoord(2, -1), B = new HexCoord(-2, 1), C = new HexCoord(1, 1), Mountain = new HexCoord(-1, -1);

        /// <summary>
        /// 주택 3채(주민 18명)가 있는 정착지. 주민 물·시설 물을 꺼서 물 없이 돌게 한다(설정은 매번 새로 읽어 고친다).
        /// free = 무료로 바로 세울 시설(종류, 타일).
        /// </summary>
        private static Settlement Town(Action<EconomyConfig> configure = null, Action<SettlementRules> tweak = null, params (string kind, HexCoord tile)[] free)
        {
            EconomyConfig config = TestData.Economy();
            config.Labor.WaterPerResidentPerHour = 0;
            foreach (string k in new[] { BuildingKinds.Orchard, BuildingKinds.Pasture, BuildingKinds.HerbFarm })
                foreach (BuildingLevel lv in config.BuildingLevels[k]) lv.WaterDemand = null;
            configure?.Invoke(config);
            var map = new HexMap(3);
            map.GetTile(Mountain).Terrain = TerrainType.Obstacle;
            SettlementRules rules = SettlementRules.FromConfig(config);
            rules.StartingCash = 5000;
            rules.FreeBuilds[BuildingKinds.House] = HouseTiles.Length - config.Labor.FreeHouses;
            foreach (var group in free.GroupBy(x => x.kind)) rules.FreeBuilds[group.Key] = group.Count();
            tweak?.Invoke(rules);
            var s = new Settlement(map, rules);
            foreach (HexCoord t in HouseTiles) Assert.IsTrue(s.RequestBuild(BuildingKinds.House, t).Success);
            foreach ((string kind, HexCoord tile) in free) Assert.IsTrue(s.RequestBuild(kind, tile).Success, kind);
            return s;
        }

        /// <summary>과수원 생산을 크게(창고가 넘치거나 팔 만큼 재고가 쌓이게).</summary>
        private static void BigOrchard(EconomyConfig c) => c.BuildingLevels[BuildingKinds.Orchard][0].GoodsProduction = 30;

        private static double Fv(Settlement s, Goods g) => s.Rules.FoodValue(g);
        private static GoodsSettings Gs(Settlement s, Goods g) => GoodsCatalog.Settings(s.Rules.Config, g);

        [Test]
        public void Config_HasAllGoods_AndNewFacilities()
        {
            EconomyConfig c = TestData.Economy();
            foreach (Goods g in GoodsCatalog.All) Assert.IsTrue(c.Goods.ContainsKey(GoodsCatalog.Id(g)), GoodsCatalog.Id(g));
            Assert.AreEqual(1.0, c.Goods["grain"].FoodValue, 1e-12, "곡물이 식량 효율의 기준(1)");
            Assert.Greater(c.Goods["meat"].FoodValue, c.Goods["grain"].FoodValue, "고기 > 곡물");
            Assert.Less(c.Goods["fruit"].FoodValue, c.Goods["grain"].FoodValue, "과일 < 곡물");
            Assert.AreEqual("경작지", FacilityCatalog.NameOf(BuildingKinds.Farm));
            CollectionAssert.AreEqual(new[] { TerrainType.Obstacle }, FacilityCatalog.Get(BuildingKinds.HerbFarm).Terrain, "허브 농장은 산지만");
            Assert.AreEqual(Goods.Grain, SettlementRules.InputOf(BuildingKinds.Pasture));
            Assert.AreEqual(Goods.Fruit, SettlementRules.InputOf(BuildingKinds.Winery));
        }

        [Test]
        public void Eat_SplitsDemandByWeights_AmongGoodsInStock()
        {
            Settlement s = Town(free: (BuildingKinds.Orchard, A));
            HourRecord h = s.AdvanceHour();
            double demand = h.FoodDemand;
            double wGrain = Gs(s, Goods.Grain).EatWeight, wFruit = Gs(s, Goods.Fruit).EatWeight;
            Assert.AreEqual(demand * wFruit / (wGrain + wFruit), h.GoodsConsumed[(int)Goods.Fruit], 1e-9, "과일 몫 = 가중치 비율");
            Assert.AreEqual(demand * wGrain / (wGrain + wFruit), h.GoodsConsumed[(int)Goods.Grain], 1e-9);
            Assert.AreEqual(0, h.FoodDeficit, 1e-12);
            Assert.AreEqual(h.GoodsProduced[(int)Goods.Fruit] - h.GoodsConsumed[(int)Goods.Fruit] / Fv(s, Goods.Fruit), s.Stock(Goods.Fruit), 1e-9,
                "실제 소비량 = 맡은 몫 ÷ 식량 효율");
        }

        [Test]
        public void Eat_GoodsThatRunOut_AreCoveredByOthers()
        {
            // 곡물 재고 0 → 과일이 전부 맡는다(과일 생산이 모자라면 부족).
            Settlement s = Town(tweak: r => r.StarterFood = 0, free: (BuildingKinds.Orchard, A));
            HourRecord h = s.AdvanceHour();
            Assert.AreEqual(0, h.GoodsConsumed[(int)Goods.Grain], 1e-12);
            double fruitFood = h.GoodsProduced[(int)Goods.Fruit] * Fv(s, Goods.Fruit);
            Assert.AreEqual(Math.Min(h.FoodDemand, fruitFood), h.GoodsConsumed[(int)Goods.Fruit], 1e-9);
        }

        [Test]
        public void Processing_UsesGrainLeftAfterEating()
        {
            Settlement s = Town(free: (BuildingKinds.Mill, A));
            double grainBefore = s.Stock(Goods.Grain);
            HourRecord h = s.AdvanceHour();
            double bread = h.GoodsProduced[(int)Goods.Bread];
            double per = s.LevelOf(s.FacilityAt(A)).InputPerOutput.Value;
            Assert.Greater(bread, 0);
            Assert.AreEqual(grainBefore - h.GoodsConsumed[(int)Goods.Grain] - bread * per, s.Stock(Goods.Grain), 1e-9, "먹고 남은 곡물로 빵");

            Settlement empty = Town(tweak: r => r.StarterFood = 0, free: (BuildingKinds.Mill, A));
            empty.AdvanceHour();
            Assert.AreEqual(0, empty.Stock(Goods.Bread), 1e-12, "원료가 없으면 못 만든다");
            Assert.AreEqual(0, empty.FacilityAt(A).InputRatio, 1e-12);
        }

        [Test]
        public void Overflow_DiscardsLowestKeepRankFirst()
        {
            Settlement s = Town(BigOrchard, r => { r.BaseFoodStorage = 48; r.StarterFood = 48; }, (BuildingKinds.Orchard, A));
            s.AdvanceHour();
            Assert.Greater(s.Stock(Goods.Grain), 0, "곡물이 아직 남아 있다");
            Assert.Greater(s.DiscardedOf(Goods.Grain), 0, "곡물(보관 순위 0)부터 버린다");
            Assert.AreEqual(0, s.DiscardedOf(Goods.Fruit), 1e-12, "과일은 곡물이 남아 있는 동안 지킨다");
            Assert.LessOrEqual(s.PantryUsed, s.FoodCapacity + 1e-9);
            Assert.AreEqual(0, s.AutoSaleIncome, 1e-12, "시장이 없으면 자동 판매하지 않는다(결정 AS1)");
        }

        [Test]
        public void Overflow_AutoSellsLowestKeepRankFirst_WhenMarketExists()
        {
            // 결정 AS1~AS3: 시장이 있으면 넘친 몫을 버리지 않고, 손으로 팔 때와 같은 판매가 곡선으로 판다(곡물부터).
            Settlement s = Town(BigOrchard, r => { r.BaseFoodStorage = 48; r.StarterFood = 48; }, (BuildingKinds.Orchard, A), (BuildingKinds.Market, B));
            HourRecord h = s.AdvanceHour();
            double sold = s.AutoSoldOf(Goods.Grain);
            Assert.Greater(sold, 0, "곡물(보관 순위 0)부터 판다");
            Assert.AreEqual(0, s.AutoSoldOf(Goods.Fruit), 1e-12, "과일은 곡물이 남아 있는 동안 지킨다");
            Assert.AreEqual(0, s.FoodDiscarded, 1e-12, "판매가가 남아 있으면 버리지 않는다");
            Assert.LessOrEqual(s.PantryUsed, s.FoodCapacity + 1e-9);

            // 압력 0에서 x만큼 팔면 수입 = 기준가 × (x − x³ ÷ (3 × 한도²)) — 1단위씩 파는 손 판매와 같은 곡선.
            PurchasePrice p = s.Rules.Config.Purchase.Food;
            double expected = p.BaseCost * p.SellRatio * (sold - sold * sold * sold / (3 * p.SellLimit * p.SellLimit));
            Assert.AreEqual(expected, h.AutoSaleIncome, 1e-9, "이 시간 기록에 자동 판매 수입");
            Assert.AreEqual(expected, s.AutoSaleIncome, 1e-9);
            Assert.Less(s.NextSellPrice(Goods.Grain), p.BaseCost * p.SellRatio, "판매 압력이 올라 손 판매가도 내려간다");
            Assert.AreEqual(0, h.Discarded, 1e-12);
        }

        [Test]
        public void SellGoods_NeedsMarket_AndPriceFalls()
        {
            Settlement noMarket = Town(BigOrchard, null, (BuildingKinds.Orchard, A));
            noMarket.AdvanceHours(8);
            Assert.AreEqual(0, noMarket.SellGoods(Goods.Fruit, 5), "시장이 없으면 판매 불가");

            // 창고를 크게 잡아 넘침 자동 판매(Overflow_AutoSells… 테스트)가 끼지 않게 한다.
            Settlement s = Town(BigOrchard, r => r.BaseFoodStorage = 1000, (BuildingKinds.Orchard, A), (BuildingKinds.Market, B));
            s.AdvanceHours(8);
            Assert.AreEqual(0, s.AutoSaleIncome, 1e-12);
            double first = s.NextSellPrice(Goods.Fruit), cash = s.Cash;
            Assert.AreEqual(5, s.SellGoods(Goods.Fruit, 5));
            Assert.Greater(s.Cash, cash);
            Assert.Less(s.NextSellPrice(Goods.Fruit), first, "팔수록 판매가가 떨어진다");
        }

        [Test]
        public void NetFoodValue_SubtractsInputs()
        {
            SettlementRules r = SettlementRules.FromConfig(TestData.Economy());
            BuildingLevel pasture = r.Level(BuildingKinds.Pasture, 1), brewery = r.Level(BuildingKinds.Brewery, 1);
            Assert.AreEqual(10 * r.FoodValue(Goods.Meat) - 10 * pasture.InputPerOutput.Value * r.FoodValue(Goods.Grain),
                r.NetFoodValue(BuildingKinds.Pasture, 10, pasture), 1e-9);
            Assert.Less(r.NetFoodValue(BuildingKinds.Brewery, 10, brewery), 0, "기호품 시설은 원료만 빠진다");
        }

        [Test]
        public void Living_TargetFromPoints_TimesDiversity_RisesSlowly()
        {
            Settlement s = Town(free: (BuildingKinds.Orchard, A));
            HourRecord h = s.AdvanceHour();
            LivingSettings l = s.Rules.Config.Living;
            double fruit = h.GoodsConsumed[(int)Goods.Fruit], grain = h.GoodsConsumed[(int)Goods.Grain];
            double avg = fruit * Gs(s, Goods.Fruit).StandardPoints / (fruit + grain);
            Assert.AreEqual(1, s.LastLiving.UpperKinds);
            Assert.AreEqual(avg * l.DiversityMultipliers[0], s.LivingTarget, 1e-9, "등급 점수 평균 × 1종 배율");
            Assert.AreEqual(Math.Min(s.LivingTarget, l.RisePerHour), s.LivingStandard, 1e-9, "목표를 향해 시간당 risePerHour");
            Assert.Greater(s.ReturnFactor, 1);
            Assert.Less(s.LeaveFactor, 1);
            Assert.Greater(s.LivingProductivity, 1);
        }

        [Test]
        public void Living_MoreKindsAndSpice_RaiseTheTarget()
        {
            Settlement one = Town(free: (BuildingKinds.Orchard, A));
            one.AdvanceHour();
            Settlement two = Town(free: new[] { (BuildingKinds.Orchard, A), (BuildingKinds.Mill, B) });
            two.AdvanceHours(2); // 빵은 첫 시간에 만들어져 두 번째 시간에 먹는다
            Assert.AreEqual(2, two.LastLiving.UpperKinds);
            Assert.Greater(two.LivingTarget, one.LivingTarget, "여러 상위 재화를 함께 먹으면 더 오른다");

            Settlement spiced = Town(free: new[] { (BuildingKinds.Orchard, A), (BuildingKinds.HerbFarm, Mountain) });
            spiced.AdvanceHour();
            Assert.Greater(spiced.LastLiving.SpiceCoverage, 0.99);
            Assert.AreEqual(one.LivingTarget * (1 + spiced.Rules.Config.Living.SpiceBonus), spiced.LivingTarget, 1e-6, "향신료가 채워지면 ×1.5");
            Assert.Greater(spiced.DiscardedOf(Goods.Spice) + spiced.Stock(Goods.Spice), 0);
        }

        [Test]
        public void Living_FallsWhenFoodRunsShort_AndSpeedsLeaving()
        {
            Settlement s = Town(tweak: r => r.StarterFood = 0);
            s.AdvanceHour();
            Assert.Less(s.LastLiving.Supply, 0.5);
            Assert.AreEqual(-100, s.LivingTarget, 1e-9, "충족률 50% 이하 → 목표 −100");
            Assert.AreEqual(-s.Rules.Config.Living.FallPerHour, s.LivingStandard, 1e-9);
            Assert.Greater(s.LeaveFactor, 1, "생활 수준이 음수면 더 빨리 떠난다");
            Assert.Less(s.LivingProductivity, 1);
        }

        [Test]
        public void Living_StaysZero_WithoutLabor()
        {
            EconomyConfig c = TestData.Economy();
            var s = new Settlement(new HexMap(3), SettlementRules.ForV08Regression(c));
            s.AdvanceHours(5);
            Assert.AreEqual(0, s.LivingStandard, 1e-12);
            Assert.AreEqual(1, s.LivingProductivity, 1e-12);
        }
    }
}
