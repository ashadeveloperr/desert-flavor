using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;
using NUnit.Framework;
using UnityEngine;

namespace DesertSettlement.Core.Tests
{
    /// <summary>모래폭풍·메뚜기떼·작물 탄저병(2026-10-23 로드맵 1-①, Docs/decision_log_auto.md D1~D7).</summary>
    public class DisasterTypesTests
    {
        private static EconomyConfig _config;
        private static readonly HexCoord FarmTile = new HexCoord(1, 0);

        [OneTimeSetUp]
        public void Load()
        {
            _config = TestData.Economy();
        }

        /// <summary>반경 3 맵, 가운데 수원, 농장 하나(무료 완공). 장비 효과를 걸 수 있다.</summary>
        private static Settlement NewSettlement(params string[] equipment)
        {
            var map = new HexMap(3);
            map.GetTile(HexCoord.Zero).Terrain = TerrainType.WaterSource;
            SettlementRules rules = SettlementRules.FromConfig(_config);
            foreach (string id in equipment) rules.ApplyEffects(_config.Equipment.Single(e => e.Id == id).Effects);
            rules.FreeBuilds[BuildingKinds.Farm] = 1;
            var s = new Settlement(map, rules);
            Assert.IsTrue(s.RequestBuild(BuildingKinds.Farm, FarmTile).Success);
            return s;
        }

        /// <summary>정확히 한 번, 유예 직후 시작하는 typeId 재해.</summary>
        private static DisasterManager AttachOne(Settlement s, string typeId, int seed = 1, double intensity = 1) =>
            DisasterManager.Attach(s, new DisasterSettings
            {
                GraceHours = _config.Disasters.GraceHours, WarningHours = _config.Disasters.WarningHours, ScheduleWindowHours = 1,
                CountWeights = new List<double> { 0, 1 }, Types = _config.Disasters.Types.Where(t => t.Id == typeId).ToList(),
                Epidemic = _config.Disasters.Epidemic, Sandstorm = _config.Disasters.Sandstorm, Locusts = _config.Disasters.Locusts, Blight = _config.Disasters.Blight,
            }, seed, intensity: intensity);

        private static void RunUntilActive(Settlement s, DisasterManager d, string typeId)
        {
            for (int i = 0; i < 200 && !d.IsActive(typeId); i++) s.AdvanceHour();
            Assert.IsTrue(d.IsActive(typeId), typeId + " 시작");
        }

        [Test]
        public void Intensity_LengthensAndDeepens_Disasters()
        {
            // 후반 가속(2026-10-27 LD3, 이슈 #12): 세기 배수만큼 지속 시간·피해가 늘고, 피해는 100%를 넘지 않는다.
            Settlement plain = NewSettlement(), strong = NewSettlement();
            DisasterManager d1 = AttachOne(plain, DisasterManager.Blight), d2 = AttachOne(strong, DisasterManager.Blight, intensity: 1.6);
            Assert.AreEqual(1.6, d2.Intensity, 1e-12);
            Assert.AreEqual((int)Math.Round(d1.Events[0].DurationHours * 1.6), d2.Events[0].DurationHours, "같은 시드, 지속 시간 × 1.6");
            RunUntilActive(plain, d1, DisasterManager.Blight);
            RunUntilActive(strong, d2, DisasterManager.Blight);
            Facility farm1 = plain.Facilities.Single(f => f.Kind == BuildingKinds.Farm), farm2 = strong.Facilities.Single(f => f.Kind == BuildingKinds.Farm);
            Assert.AreEqual(0.5, d1.ProductionFactor(farm1), 1e-9, "기본 탄저병 −50%");
            Assert.AreEqual(0.2, d2.ProductionFactor(farm2), 1e-9, "× 1.6 → −80%");

            Settlement s3 = NewSettlement();
            DisasterManager d3 = AttachOne(s3, DisasterManager.Blight, intensity: 3);
            RunUntilActive(s3, d3, DisasterManager.Blight);
            Assert.AreEqual(0, d3.ProductionFactor(s3.Facilities.Single(f => f.Kind == BuildingKinds.Farm)), 1e-9, "피해는 최대 100%");
        }

        [Test]
        public void Config_HasFourTypes_InsectNet_AndRegionWeights()
        {
            CollectionAssert.AreEquivalent(new[] { "epidemic", "sandstorm", "locusts", "blight" }, _config.Disasters.Types.Select(t => t.Id));
            Assert.AreEqual(450, _config.Equipment.Single(e => e.Id == "insectNet").Price, 1e-9);
            Assert.AreEqual(3, _config.Disasters.RegionWeightMultipliers["sandWind"]["sandstorm"], 1e-9);
            Assert.AreEqual(3, _config.Disasters.RegionWeightMultipliers["salineSoil"]["blight"], 1e-9);
        }

        [Test]
        public void Sandstorm_DoublesCanalLoss_HalvesConstruction_AndEndsOnTime()
        {
            Settlement s = NewSettlement();
            DisasterManager d = AttachOne(s, DisasterManager.Sandstorm);
            DisasterEvent e = d.Events.Single();
            Assert.That(e.DurationHours, Is.InRange(6, 18));
            RunUntilActive(s, d, DisasterManager.Sandstorm);
            Assert.AreEqual(2.0, d.CanalLossFactor, 1e-12);
            Assert.AreEqual(0.5, d.ConstructionSpeed, 1e-12);

            // 폭풍 중 착공한 작업은 늦게 끝난다.
            ConstructionJob job = s.GetJob(s.RequestBuild(BuildingKinds.Warehouse, new HexCoord(-1, 0)).JobId);
            long plain = job.DeadlineHour; // 명령 순간 착공(JQ2). 폭풍이 없으면 이 시각에 완공
            s.AdvanceHours(4);
            Assert.Greater(job.DeadlineHour, plain, "공사 속도 −50%");

            for (int i = 0; i < 30 && d.IsActive(DisasterManager.Sandstorm); i++) s.AdvanceHour();
            Assert.AreEqual(DisasterState.Ended, e.State);
            Assert.AreEqual(e.StartHour + e.DurationHours, e.EndHour);
            Assert.AreEqual(1.0, d.CanalLossFactor, 1e-12, "끝나면 원래대로");
        }

        [Test]
        public void Windbreak_HalvesSandstormLossIncrease()
        {
            Settlement s = NewSettlement("windbreak");
            DisasterManager d = AttachOne(s, DisasterManager.Sandstorm);
            RunUntilActive(s, d, DisasterManager.Sandstorm);
            Assert.AreEqual(1.5, d.CanalLossFactor, 1e-12, "×2 → ×1.5");
            Assert.AreEqual(0.5, d.ConstructionSpeed, 1e-12, "방풍막은 공사 속도에는 영향 없음(D3)");
        }

        [Test]
        public void Locusts_StopFarms_AndEatFoodStock_InsectNetHalves()
        {
            foreach (bool net in new[] { false, true })
            {
                Settlement s = net ? NewSettlement("insectNet") : NewSettlement();
                DisasterManager d = AttachOne(s, DisasterManager.Locusts);
                for (int i = 0; i < 200 && d.Events.Single().State != DisasterState.Warned; i++) s.AdvanceHour();
                while (s.CurrentHour < d.Events.Single().StartHour) s.AdvanceHour();
                double before = s.Food;
                Facility farm = s.FacilityAt(FarmTile);
                s.AdvanceHour(); // 이 시간 시작에 메뚜기떼가 온다
                Assert.IsTrue(d.IsActive(DisasterManager.Locusts));
                double expectedLoss = 0.15 * (net ? 0.5 : 1);
                Assert.AreEqual(net ? 0.5 : 0.0, d.ProductionFactor(farm), 1e-12, "농장 생산 0 (방충망 50%)");
                // 시간 시작에 재고 −15%(방충망 −7.5%), 그다음 이번 시간 생산·소비.
                HourRecord rec = s.History.Last();
                double expected = Math.Min(s.FoodCapacity, Math.Max(0, before * (1 - expectedLoss) + rec.FoodProduced - rec.FoodDemand));
                Assert.AreEqual(expected, s.Food, 1e-9, "식량 재고 손실");
                Assert.That(d.Events.Single().DurationHours, Is.InRange(4, 12));
            }
        }

        [Test]
        public void Blight_HalvesFarms_IrrigationHalvesDamage()
        {
            Settlement plain = NewSettlement();
            DisasterManager d = AttachOne(plain, DisasterManager.Blight);
            RunUntilActive(plain, d, DisasterManager.Blight);
            Assert.AreEqual(0.5, d.ProductionFactor(plain.FacilityAt(FarmTile)), 1e-12);
            Assert.That(d.Events.Single().DurationHours, Is.InRange(48, 96));

            Settlement irrigated = NewSettlement("irrigation");
            DisasterManager d2 = AttachOne(irrigated, DisasterManager.Blight);
            RunUntilActive(irrigated, d2, DisasterManager.Blight);
            Assert.AreEqual(0.75, d2.ProductionFactor(irrigated.FacilityAt(FarmTile)), 1e-12, "관개 설비: 피해 절반");
        }

        [Test]
        public void SandWindRegion_MakesSandstormsMoreLikely()
        {
            int Count(IEnumerable<string> region)
            {
                int n = 0;
                for (int seed = 0; seed < 300; seed++)
                {
                    var map = new HexMap(3);
                    var s = new Settlement(map, SettlementRules.FromConfig(_config));
                    DisasterManager d = DisasterManager.Attach(s, _config.Disasters, seed, region);
                    n += d.Events.Count(e => e.TypeId == DisasterManager.Sandstorm);
                }
                return n;
            }
            int normal = Count(null), sandWind = Count(new[] { "sandWind" });
            Assert.Greater(sandWind, normal * 1.8, $"모래바람 지대는 모래폭풍 3배 가중치(보통 {normal}, 모래바람 {sandWind})");
        }
    }
}
