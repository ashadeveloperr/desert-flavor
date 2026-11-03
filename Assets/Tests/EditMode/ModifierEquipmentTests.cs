using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
            _economy = TestData.Economy();
            _proc = TestData.Procedural();
            _maps = TestData.Maps();
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
        public void Effects_EveryPropertySurvivesCombineScaledDescribeAndApply()
        {
            // 결정 MT1: EconomyEffects에 속성을 더하고 Combine·Scaled·Describe·SettlementRules.ApplyEffects 중 하나를 빠뜨리면 여기서 걸린다.
            // 속성 목록은 반사로 읽으므로 새 속성도 저절로 검사된다.
            var notScaled = new HashSet<string> { nameof(EconomyEffects.EfficiencyCap), nameof(EconomyEffects.ExtraConstructionSlots) }; // Scaled 주석: 약점 전용 상한·정수 슬롯
            var defaults = new EconomyEffects();
            List<PropertyInfo> props = typeof(EconomyEffects).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite).ToList();
            Assert.Greater(props.Count, 15);
            foreach (PropertyInfo p in props)
            {
                // '효과 있음' 값: 배율(기본 1)은 0.8, 더하는 값(기본 0)은 0.3, 정수는 2.
                object value;
                if (p.PropertyType == typeof(int)) value = 2;
                else if (p.PropertyType == typeof(double)) value = (double)p.GetValue(defaults) == 1 ? 0.8 : 0.3;
                else { Assert.Fail($"{p.Name}: 숫자가 아닌 효과 속성은 이 테스트에 값 규칙을 더해야 한다"); continue; }
                var e = new EconomyEffects();
                p.SetValue(e, value);
                double v = Convert.ToDouble(value);

                Assert.AreEqual(v, Convert.ToDouble(p.GetValue(EconomyEffects.Combine(new[] { e }))), 1e-12, $"Combine에서 {p.Name}이 빠짐");
                Assert.AreEqual(v, Convert.ToDouble(p.GetValue(e.Scaled(1))), 1e-12, $"Scaled에서 {p.Name}이 빠짐");
                bool changedAt2 = Math.Abs(Convert.ToDouble(p.GetValue(e.Scaled(2))) - v) > 1e-12;
                Assert.AreEqual(!notScaled.Contains(p.Name), changedAt2, $"Scaled(2)가 {p.Name}을 {(changedAt2 ? "바꿈" : "안 바꿈")}");
                Assert.AreNotEqual("효과 없음", e.Describe(), $"Describe에 {p.Name}이 없음");

                // 효율 상한 보너스는 상한이 1보다 낮을 때만 드러나므로 두 규칙 모두 상한 0.9에서 시작한다.
                SettlementRules before = SettlementRules.FromConfig(_economy), after = SettlementRules.FromConfig(_economy);
                before.ApplyEffects(new EconomyEffects { EfficiencyCap = 0.9 });
                after.ApplyEffects(new EconomyEffects { EfficiencyCap = 0.9 });
                after.ApplyEffects(e);
                Assert.AreNotEqual(RulesSnapshot(before), RulesSnapshot(after), $"SettlementRules.ApplyEffects가 {p.Name}을 반영하지 않음");
            }
        }

        /// <summary>
        /// 규칙의 숫자 속성 전부 + 수로 손실 + 정착지가 바로 읽는 효과 묶음(<see cref="SettlementRules.Effects"/>, 결정 BM1)의 숫자 속성 전부.
        /// </summary>
        private static string RulesSnapshot(SettlementRules r) =>
            string.Join("|", typeof(SettlementRules).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetIndexParameters().Length == 0 && (p.PropertyType == typeof(double) || p.PropertyType == typeof(int)))
                .Select(p => $"{p.Name}={p.GetValue(r)}")) + $"|loss={r.Water.LossPerWholeEdge}|"
            + string.Join("|", typeof(EconomyEffects).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(double) || p.PropertyType == typeof(int)).Select(p => $"e.{p.Name}={p.GetValue(r.Effects)}"));

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
            Assert.AreEqual(4.0 * 1.5 * rules.Trade.NoMarketPriceMultiplier, s.NextPrice(false), 1e-9, "석재 구매 단가 ×1.5(시장이 없어 상인 값도 곱해짐, 2026-10-27 이슈 #18)");
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
            Campaign c = CampaignAfterSale(cashBoost: 9000);
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
            // 행선지 창 '운영비 ×'(2026-10-27 이슈 #6) = 들어간 뒤 규칙 값(받은 영구 모디파이어·지역 모디파이어·난이도 비례).
            for (int pick = 0; pick < 2; pick++)
            {
                Campaign c = CampaignAfterSale(cashBoost: 9000);
                RegionOffer offer = c.NextOffers()[pick];
                double preview = c.PreviewUpkeepMultiplier(offer);
                Settlement s = c.StartNextSettlement(pick);
                Assert.Greater(preview, 1.0, "생성 맵은 난이도만큼 운영비가 오른다");
                Assert.AreEqual(s.Rules.UpkeepMultiplier, preview, 1e-12, $"후보 {pick + 1}");
            }
        }

        [Test]
        public void ScaledWeakness_StrengthensAndKeepsAFloor()
        {
            // 후반 가속(2026-10-27 LD2, 이슈 #12): 1에서 벗어난 만큼 s배, 줄어드는 배율은 0.3 아래로 내려가지 않는다.
            Assert.AreEqual(1.75, new EconomyEffects { StoneCostMultiplier = 1.25 }.ScaledWeakness(3).StoneCostMultiplier, 1e-12, "무른 지반 +25% × 3");
            Assert.AreEqual(0.3, new EconomyEffects { FoodOutputMultiplier = 0.75 }.ScaledWeakness(4).FoodOutputMultiplier, 1e-12, "염분 토양이 0이 되지 않음");
            Assert.AreEqual(0.8, new EconomyEffects { EfficiencyCap = 0.9 }.ScaledWeakness(2).EfficiencyCap, 1e-12, "고지대 효율 상한 0.9 → 0.8");
            Assert.AreEqual(1.0, new EconomyEffects { EfficiencyCap = 1 }.ScaledWeakness(3).EfficiencyCap, 1e-12, "상한 없는 약점은 그대로");
        }

        [Test]
        public void Campaign_LateDifficulty_ScalesWeaknessesUpkeepAndDisasters()
        {
            // D가 lateStartD를 넘으면 약점·운영비·재해가 세진다(LD1~LD3). 후보 카드에 보이는 약점 값 = 실제 규칙 값.
            // 설정의 약점 가속은 꺼져 있으므로(결정 LD5) 이 테스트에서만 켜서 배율이 걸리는 길을 확인한다.
            DifficultyCurve curve = _proc.Difficulty;
            double weaknessLate = curve.WeaknessLatePerD;
            curve.WeaknessLatePerD = 0.5;
            try { LateDifficultyScales(curve); }
            finally { curve.WeaknessLatePerD = weaknessLate; }
        }

        private void LateDifficultyScales(DifficultyCurve curve)
        {
            Campaign c = CampaignAfterSale(cashBoost: 90000);
            RegionOffer offer = c.NextOffers()[0];
            double d = offer.Params.D;
            Assert.Greater(d, curve.LateStartD + 1, "후반 난이도 후보");
            double s = 1 + curve.WeaknessLatePerD * (d - curve.LateStartD);
            Assert.IsNotEmpty(offer.Weaknesses);
            foreach (RegionModifierDefinition w in offer.Weaknesses)
            {
                EconomyEffects original = _proc.Modifiers.Definitions.Single(x => x.Id == w.Id).Effects;
                Assert.AreEqual(original.ScaledWeakness(s).Describe(), w.Effects.Describe(), w.Id);
            }
            double preview = c.PreviewUpkeepMultiplier(offer);
            Settlement started = c.StartNextSettlement(0);
            Assert.AreEqual(started.Rules.UpkeepMultiplier, preview, 1e-12);
            double late = d - curve.LateStartD;
            double upkeepFromD = 1 + curve.UpkeepPerD * d + curve.UpkeepLatePerD2 * late * late;
            Assert.AreEqual(upkeepFromD * offer.Effects.UpkeepMultiplier * c.ModifierEffects.UpkeepMultiplier, started.Rules.UpkeepMultiplier, 1e-9, "지역 × 받은 모디파이어 × (1 + 0.5D + 0.2x²)");
            Assert.AreEqual(1 + curve.DisasterLatePerD * late, c.Disasters.Intensity, 1e-12);
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

        /// <summary>첫 정착지를 샘플 배치로 첫 경매까지 돌려 판 뒤(행선지 선택), 시작 자금을 늘려 높은 난이도 후보가 나오게 한다.</summary>
        private static Campaign CampaignAfterSale(double cashBoost)
        {
            EconomyConfig rich = TestData.Economy();
            rich.Prototype.StartingCash += cashBoost;
            var c = new Campaign(rich, _maps, seed: 11, procedural: _proc);
            Settlement s = c.StartNextSettlement();
            Assert.IsEmpty(c.CurrentMap.RequestSampleLayout(s));
            for (int h = 0; h < 600 && !c.Auctions.IsOpen; h++) s.AdvanceHour();
            Assert.IsTrue(c.Auctions.IsOpen);
            c.Auctions.Sell(0);
            Assert.AreEqual(CampaignState.ChoosingDestination, c.State);
            return c;
        }
    }
}
