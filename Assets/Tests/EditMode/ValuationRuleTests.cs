using System;
using System.IO;
using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Progression;
using NUnit.Framework;
using UnityEngine;

namespace DesertSettlement.Core.Tests
{
    /// <summary>2026-10-23 가치 평가 보완: 가동 숙성(몰아짓기 방지)·석재 배율의 채석장 능력(채석장 철거 방지).</summary>
    public class ValuationRuleTests
    {
        private static EconomyConfig _config;
        private static MapDefinition _map;

        [OneTimeSetUp]
        public void Load()
        {
            _config = TestData.Economy();
            _map = MapLoader.Parse(File.ReadAllText(TestData.MapPaths.First()));
        }

        private static Settlement RunSample(int hours, Action<SettlementRules> tweak = null)
        {
            SettlementRules rules = SettlementRules.FromConfig(_config);
            tweak?.Invoke(rules);
            var s = new Settlement(_map.CreateMap(), rules);
            Assert.IsEmpty(_map.RequestSampleLayout(s));
            s.BuyStone(60);
            for (int h = 0; h < hours; h++) s.AdvanceHour();
            return s;
        }

        [Test]
        public void Config_EnablesBothRules()
        {
            SettlementRules r = SettlementRules.FromConfig(_config);
            Assert.AreEqual(0.7, r.MaturityStartRatio, 1e-12);
            Assert.AreEqual(48, r.MaturityFullHours, 1e-12);
            Assert.IsTrue(r.StoneMultiplierUsesQuarryCapacity);
        }

        [Test]
        public void Maturity_RampsFromStartRatioToFullOverConfiguredHours()
        {
            SettlementRules r = SettlementRules.FromConfig(_config);
            Assert.AreEqual(0.7, r.Maturity(0), 1e-12);
            Assert.AreEqual(0.85, r.Maturity(24), 1e-12);
            Assert.AreEqual(1.0, r.Maturity(48), 1e-12);
            Assert.AreEqual(1.0, r.Maturity(500), 1e-12);
            r.MaturityStartRatio = 1;
            Assert.AreEqual(1.0, r.Maturity(0), 1e-12, "1이면 숙성 없음");
        }

        [Test]
        public void ValuedOre_IsDiscountedRightAfterMineCompletes_AndFullAfter48h()
        {
            Settlement s = RunSample(200);
            Facility mine = s.Facilities.Single(f => f.Kind == BuildingKinds.Mine);
            // 완공 시간에 일손이 아직 다른 시설에 있을 수 있어(1명 단위 배치, 결정 PI1) 처음 생산한 시간을 본다.
            HourRecord justBuilt = s.History.First(h => h.Hour >= mine.CompletedHour && h.OreProduced > 0);
            HourRecord mature = s.History.First(h => h.Hour == mine.CompletedHour + 60);
            Assert.Less(justBuilt.Hour - mine.CompletedHour, 12, "곧 생산을 시작한다");
            Assert.AreEqual(0.7, s.Rules.MaturityStartRatio, 1e-9);
            Assert.AreEqual(justBuilt.OreProduced * s.Rules.Maturity(justBuilt.Hour - mine.CompletedHour), justBuilt.OreValued, 1e-9, "완공 직후 70%에서 오른다");
            Assert.AreEqual(mature.OreProduced, mature.OreValued, 1e-9, "48시간 뒤 100%");
        }

        [Test]
        public void Valuation_UsesMaturedOre_AndRawOreIsReported()
        {
            Settlement s = RunSample(120);
            Facility mine = s.Facilities.Single(f => f.Kind == BuildingKinds.Mine);
            long t = mine.CompletedHour + 30; // 평가 창(직전 24h) 안 광산 나이 6~29h → 숙성 덜 됨
            Settlement fresh = RunSample((int)t);
            ValuationResult v = SettlementValuation.Evaluate(fresh, t);
            Assert.Greater(v.OreRawLast24h, v.OreLast24h, "숙성 반영 실적이 실제 생산보다 작다");
            Settlement off = RunSample((int)t, r => r.MaturityStartRatio = 1);
            Assert.Greater(SettlementValuation.Evaluate(off, t).Value, v.Value, "숙성을 끄면 가치가 더 높다");
        }

        [Test]
        public void StoneMultiplier_DropsWhenQuarryIsDemolished()
        {
            Settlement s = RunSample(200);
            ValuationResult before = SettlementValuation.Evaluate(s, s.CurrentHour);
            Facility quarry = s.Facilities.Single(f => f.Kind == BuildingKinds.Quarry);
            Assert.IsTrue(s.RequestDemolish(quarry.Id).Success);
            for (int h = 0; h < 30 && s.Facilities.Any(f => f.Kind == BuildingKinds.Quarry); h++) s.AdvanceHour();
            Assert.IsFalse(s.Facilities.Any(f => f.Kind == BuildingKinds.Quarry), "철거 완료");
            ValuationResult after = SettlementValuation.Evaluate(s, s.CurrentHour);
            Assert.Less(after.StoneMultiplier, before.StoneMultiplier, "채석장을 철거하면 석재 배율이 떨어진다");
            Assert.IsFalse(after.StoneFromQuarryCapacity);
        }

        [Test]
        public void StoneMultiplier_UsesQuarryCapacityWhenAverageIsLower()
        {
            Settlement s = RunSample(200);
            double capacity = s.OperatingOf(BuildingKinds.Quarry).Sum(q => s.LevelOf(q).StoneProduction ?? 0);
            ValuationResult v = SettlementValuation.Evaluate(s, s.CurrentHour);
            Assert.GreaterOrEqual(v.StoneAveragePerHour, capacity - 1e-9, "석재 기준 = max(24h 평균, 가동 중 채석장 능력)");
            Settlement off = RunSample(200, r => r.StoneMultiplierUsesQuarryCapacity = false);
            Assert.LessOrEqual(SettlementValuation.Evaluate(off, off.CurrentHour).StoneMultiplier, v.StoneMultiplier + 1e-12);
        }
    }
}
