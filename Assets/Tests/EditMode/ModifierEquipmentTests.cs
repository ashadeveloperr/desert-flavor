using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;
using DesertSettlement.Core.Progression;
using NUnit.Framework;
using UnityEngine;

namespace DesertSettlement.Core.Tests
{
    /// <summary>지역 모디파이어·장비 효과·현장 장비 슬롯·다음 지역 후보.</summary>
    public class ModifierEquipmentTests
    {
        private static EconomyConfig _economy;
        private static ProceduralMapConfig _proc;
        private static List<MapDefinition> _maps;

        [OneTimeSetUp]
        public void Load()
        {
            string data = Path.Combine(Application.dataPath, "Data");
            _economy = EconomyConfigLoader.Parse(File.ReadAllText(Path.Combine(data, "Config", "economy_balance_v0.8_config.json")));
            _proc = ProceduralMapConfigLoader.Parse(File.ReadAllText(Path.Combine(data, "Config", "procedural_map_config.json")));
            _maps = Directory.GetFiles(Path.Combine(data, "Maps"), "*.json").OrderBy(p => p, StringComparer.Ordinal)
                .Select(p => MapLoader.Parse(File.ReadAllText(p))).ToList();
        }

        // ───────────── 효과 ─────────────

        [Test]
        public void Effects_Combine_MultipliesAddsAndTakesLowestCap()
        {
            var a = new EconomyEffects { StoneCostMultiplier = 1.25, EfficiencyCap = 0.9, ExtraConstructionSlots = 1 };
            var b = new EconomyEffects { StoneCostMultiplier = 0.9, EfficiencyCapBonus = 0.05, ExtraStorage = 50 };
            EconomyEffects c = EconomyEffects.Combine(new[] { a, b });
            Assert.AreEqual(1.125, c.StoneCostMultiplier, 1e-12);
            Assert.AreEqual(0.95, c.EffectiveEfficiencyCap, 1e-12);
            Assert.AreEqual(1, c.ExtraConstructionSlots);
            Assert.AreEqual(50, c.ExtraStorage);
            Assert.AreEqual("효과 없음", new EconomyEffects().Describe());
            StringAssert.Contains("건설 석재 +25%", a.Describe());
        }

        [Test]
        public void Effects_ChangeSettlementEconomy()
        {
            SettlementRules rules = SettlementRules.FromConfig(_economy);
            double baseLoss = rules.Water.LossPerWholeEdge;
            int baseSlots = rules.ConstructionSlots;
            double baseStorage = rules.BaseStoneStorage;
            rules.ApplyEffects(new EconomyEffects
            {
                StoneCostMultiplier = 1.25, EfficiencyCap = 0.9, CanalLossMultiplier = 2, PurchasePriceMultiplier = 1.5,
                ExtraConstructionSlots = 1, ExtraStorage = 50, UpkeepMultiplier = 1.25,
            });
            Assert.AreEqual(baseLoss * 2, rules.Water.LossPerWholeEdge, 1e-12, "수로 손실");
            Assert.AreEqual(baseSlots + 1, rules.ConstructionSlots);
            Assert.AreEqual(baseStorage + 50, rules.BaseStoneStorage);

            var s = new Settlement(_maps[0].CreateMap(), rules);
            Assert.AreEqual(4.0 * 1.5, s.NextPrice(false), 1e-9, "석재 구매 단가 ×1.5");
            Assert.IsTrue(s.RequestBuild(BuildingKinds.Well, new HexCoord(0, 0)).Success);
            Assert.AreEqual((_economy.GetLevel(BuildingKinds.Well, 1).Stone ?? 0) * 1.25, s.Jobs.Single().StoneCost, 1e-9, "건설 석재 ×1.25");
        }

        [Test]
        public void Effects_EfficiencyCap_LimitsProduction()
        {
            // 같은 샘플 배치를 상한 없음 / 상한 90%로 돌려 광물 생산을 비교한다.
            double Ore(double cap)
            {
                SettlementRules rules = SettlementRules.FromConfig(_economy);
                rules.ApplyEffects(new EconomyEffects { EfficiencyCap = cap });
                var s = new Settlement(_maps[0].CreateMap(), rules);
                Assert.IsEmpty(_maps[0].RequestSampleLayout(s));
                s.BuyStone(60);
                for (int h = 0; h < 120; h++) s.AdvanceHour();
                return s.History.Max(r => r.OreProduced);
            }
            // 맵 1의 광산은 물 공급률이 약 79%라 상한 90%로는 차이가 안 날 수 있어, 그보다 낮은 50%로 확인한다.
            double oreL1 = _economy.GetLevel(BuildingKinds.Mine, 1).OreProduction ?? 0;
            double full = Ore(1.0), capped = Ore(0.5);
            Assert.Greater(full, oreL1 * 0.5 + 1e-6);
            Assert.AreEqual(oreL1 * 0.5, capped, 1e-9, "시간당 광물 = 최대치 × 상한 50%");
        }

        // ───────────── 모디파이어 출현 ─────────────

        [Test]
        public void Modifiers_WeaknessCount_FollowsConfirmedSchedule()
        {
            RegionModifierSettings m = _proc.Modifiers;
            Assert.AreEqual(0, m.WeaknessCount(0.49));
            Assert.AreEqual(1, m.WeaknessCount(0.5));
            Assert.AreEqual(1, m.WeaknessCount(1.49));
            Assert.AreEqual(2, m.WeaknessCount(1.5));
            Assert.AreEqual(3, m.WeaknessCount(2.5));
            Assert.AreEqual(6, m.WeaknessCount(50), "약점 종류 수(6)를 넘지 않음");
        }

        [Test]
        public void Modifiers_Roll_IsDeterministicAndHasNoDuplicates()
        {
            for (int seed = 1; seed <= 30; seed++)
            {
                List<RegionModifierDefinition> a = _proc.Modifiers.Roll(3.0, seed), b = _proc.Modifiers.Roll(3.0, seed);
                CollectionAssert.AreEqual(a.Select(x => x.Id).ToList(), b.Select(x => x.Id).ToList());
                Assert.AreEqual(3, a.Count(x => x.Kind == RegionModifierKind.Weakness));
                Assert.LessOrEqual(a.Count(x => x.Kind == RegionModifierKind.Strength), 1);
                Assert.AreEqual(a.Count, a.Select(x => x.Id).Distinct().Count(), "중복 없음");
            }
        }

        [Test]
        public void Modifiers_StrengthChance_IsRoughlyConfigured()
        {
            int withStrength = Enumerable.Range(1, 2000).Count(seed => _proc.Modifiers.Roll(1.0, seed).Any(x => x.Kind == RegionModifierKind.Strength));
            Assert.AreEqual(_proc.Modifiers.StrengthChance, withStrength / 2000.0, 0.04);
        }

        // ───────────── 캠페인: 후보·슬롯 ─────────────

        [Test]
        public void Campaign_OffersTwoRegions_WithDifferentWeaknesses_AndAppliesChosenOne()
        {
            Campaign c = CampaignInShop(cashBoost: 9000);
            IReadOnlyList<RegionOffer> offers = c.NextOffers();
            Assert.AreEqual(2, offers.Count, "후보 2곳");
            Assert.AreEqual(offers[0].Params.D, offers[1].Params.D, 1e-12, "같은 난이도");
            Assert.GreaterOrEqual(offers[0].Weaknesses.Count(), 1, "D ≥ 0.5이면 약점 1개 이상");
            CollectionAssert.IsEmpty(offers[0].Weaknesses.Select(w => w.Id).Intersect(offers[1].Weaknesses.Select(w => w.Id)), "두 후보의 약점이 겹치지 않음");
            CollectionAssert.AreEqual(offers.Select(o => o.Seed), c.NextOffers().Select(o => o.Seed), "같은 자금이면 같은 후보");

            List<string> chosen = offers[1].Modifiers.Select(m => m.Id).ToList();
            Settlement s = c.StartNextSettlement(1);
            CollectionAssert.AreEqual(chosen, c.CurrentModifiers.Select(m => m.Id).ToList());
            Assert.AreEqual(offers[1].Effects.StoneCostMultiplier, s.Rules.StoneCostMultiplier, 1e-12, "고른 지역의 모디파이어가 규칙에 걸림");
        }

        [Test]
        public void Campaign_PreviewUpkeepMultiplier_MatchesRulesAfterDeparture()
        {
            // 행선지 창 '운영비 ×'(2026-10-27 이슈 #6) = 들어간 뒤 규칙 값(장착 장비·모디파이어·난이도 비례).
            for (int pick = 0; pick < 2; pick++)
            {
                Campaign c = CampaignInShop(cashBoost: 9000);
                foreach (EquipmentDefinition e in c.Shop.Take(3)) Assert.IsTrue(c.BuyEquipment(e.Id).Success, e.Id);
                RegionOffer offer = c.NextOffers()[pick];
                double preview = c.PreviewUpkeepMultiplier(offer);
                Settlement s = c.StartNextSettlement(pick);
                Assert.Greater(preview, 1.0, "생성 맵은 난이도만큼 운영비가 오른다");
                Assert.AreEqual(s.Rules.UpkeepMultiplier, preview, 1e-12, $"후보 {pick + 1}");
            }
        }

        [Test]
        public void Campaign_FixedMapTurn_HasSingleOfferWithoutModifiers()
        {
            var c = new Campaign(_economy, _maps, seed: 3, procedural: _proc);
            IReadOnlyList<RegionOffer> offers = c.NextOffers();
            Assert.AreEqual(1, offers.Count);
            Assert.IsTrue(offers[0].IsFixedMap);
            c.StartNextSettlement();
            Assert.IsEmpty(c.CurrentModifiers);
        }

        [Test]
        public void Campaign_EquipmentSlots_OnlyEquippedGearTakesEffect()
        {
            Campaign c = CampaignInShop(cashBoost: 20000);
            foreach (EquipmentDefinition e in c.Shop) Assert.IsTrue(c.BuyEquipment(e.Id).Success, e.Id);
            Assert.AreEqual(Campaign.EquipmentSlots, c.Loadout.Count, "산 순서대로 5개까지 자동 장착");
            string sixth = c.Shop[5].Id;
            Assert.IsFalse(c.IsEquipped(sixth));
            Assert.IsFalse(c.SetEquipped(sixth, true).Success, "슬롯이 가득 참");

            Assert.IsTrue(c.SetEquipped("wellPump", false).Success);
            Assert.IsTrue(c.SetEquipped(sixth, true).Success);
            Assert.IsFalse(c.SetEquipped("nope", true).Success, "가지지 않은 장비");

            Settlement s = c.StartNextSettlement();
            Assert.AreEqual(1.0 * c.CurrentModifiers.Aggregate(1.0, (m, x) => m * x.Effects.WellSupplyMultiplier), s.Rules.WellSupplyMultiplier, 1e-12,
                "우물 장비를 뺐으므로 우물 배율은 모디파이어만");
            Assert.IsFalse(c.SetEquipped(sixth, false).Success, "정착지 안에서는 바꿀 수 없음");
        }

        /// <summary>첫 정착지를 샘플 배치로 첫 경매까지 돌려 판 뒤(상점), 시작 자금을 늘려 높은 난이도 후보가 나오게 한다.</summary>
        private static Campaign CampaignInShop(double cashBoost)
        {
            EconomyConfig rich = EconomyConfigLoader.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "Data", "Config", "economy_balance_v0.8_config.json")));
            rich.Prototype.StartingCash += cashBoost;
            var c = new Campaign(rich, _maps, seed: 11, procedural: _proc);
            Settlement s = c.StartNextSettlement();
            Assert.IsEmpty(c.CurrentMap.RequestSampleLayout(s));
            for (int h = 0; h < 600 && !c.Auctions.IsOpen; h++) s.AdvanceHour();
            Assert.IsTrue(c.Auctions.IsOpen);
            c.Auctions.Sell(0);
            Assert.AreEqual(CampaignState.Shopping, c.State);
            return c;
        }
    }
}
