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
    /// <summary>재해(2026-10-23: 전염병·병원·방역 장비). Docs/work_items_2026-10-23.md 4번.</summary>
    public class DisasterTests
    {
        private static EconomyConfig _config;

        [OneTimeSetUp]
        public void Load()
        {
            _config = EconomyConfigLoader.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "Data", "Config", "economy_balance_v0.8_config.json")));
        }

        /// <summary>반경 3 사막 맵(가운데 수원). 시설은 무료 완공 배치로 바로 세운다.</summary>
        private static Settlement NewSettlement(Action<SettlementRules> tweak = null)
        {
            var map = new HexMap(3);
            map.GetTile(HexCoord.Zero).Terrain = TerrainType.WaterSource;
            SettlementRules rules = SettlementRules.FromConfig(_config);
            tweak?.Invoke(rules);
            return new Settlement(map, rules);
        }

        /// <summary>정확히 한 번, 유예 직후 시작하는 전염병 설정.</summary>
        private static DisasterSettings OneEpidemicAtGrace() => new DisasterSettings
        {
            GraceHours = _config.Disasters.GraceHours,
            WarningHours = _config.Disasters.WarningHours,
            ScheduleWindowHours = 1,
            CountWeights = new List<double> { 0, 1 },
            Types = _config.Disasters.Types.Where(t => t.Id == DisasterManager.EpidemicId).ToList(), // 2026-10-23 재해 4종: 이 테스트는 전염병만
            Epidemic = _config.Disasters.Epidemic,
        };

        // ───────────── 일정 ─────────────

        [Test]
        public void Config_HasEpidemicHospitalAndVaccination()
        {
            Assert.AreEqual(48, _config.Disasters.GraceHours);
            Assert.AreEqual(6, _config.Disasters.WarningHours);
            Assert.IsTrue(_config.Disasters.Types.Any(t => t.Id == "epidemic"));
            Assert.AreEqual(0.5, _config.Disasters.Epidemic.HospitalDamageFactor, 1e-12);
            Assert.AreEqual(20, _config.GetLevel(BuildingKinds.Hospital, 1).Stone);
            Assert.AreEqual(0.7, _config.Disasters.Epidemic.SeverityFallResistanceRatio, 1e-12);
            Assert.AreEqual(1.3, _config.Equipment.Single(e => e.Id == "vaccination").Effects.EpidemicResistanceGrowthMultiplier, 1e-12);
            CollectionAssert.Contains(SettlementRules.FromConfig(_config).AllowedTerrain[BuildingKinds.Hospital], TerrainType.Desert);
        }

        [Test]
        public void Schedule_IsDeterministic_AfterGrace_WithWarning()
        {
            for (int seed = 1; seed <= 50; seed++)
            {
                DisasterManager a = DisasterManager.Attach(NewSettlement(), _config.Disasters, seed);
                DisasterManager b = DisasterManager.Attach(NewSettlement(), _config.Disasters, seed);
                CollectionAssert.AreEqual(a.Events.Select(e => e.StartHour), b.Events.Select(e => e.StartHour), "같은 시드 = 같은 일정");
                Assert.LessOrEqual(a.Events.Count, 2, "정착지마다 0~2번");
                foreach (DisasterEvent e in a.Events)
                {
                    Assert.GreaterOrEqual(e.StartHour, 48, "첫 48시간은 재해 없음");
                    Assert.Less(e.StartHour, 48 + _config.Disasters.ScheduleWindowHours);
                    Assert.AreEqual(e.StartHour - 6, e.WarningHour, "6시간 전 예고");
                }
            }
        }

        [Test]
        public void Schedule_CountFollowsWeights()
        {
            var counts = new int[3];
            const int n = 3000;
            for (int seed = 0; seed < n; seed++) counts[DisasterManager.Attach(NewSettlement(), _config.Disasters, seed).Events.Count]++;
            IList<double> w = _config.Disasters.EffectiveCountWeights;
            for (int k = 0; k < 3; k++) Assert.AreEqual(w[k] / w.Sum(), counts[k] / (double)n, 0.03, $"{k}번 확률");
        }

        // ───────────── 전염병 ─────────────

        [Test]
        public void Epidemic_WarnsStartsGrowsResistsAndEnds()
        {
            Settlement s = NewSettlement();
            DisasterManager d = DisasterManager.Attach(s, OneEpidemicAtGrace(), 1);
            var log = new List<string>();
            d.Warned += e => log.Add($"warn {s.CurrentHour}");
            d.Started += e => log.Add($"start {s.CurrentHour}");
            d.Ended += e => log.Add($"end {e.EndHour}");

            double peakSeverity = 0;
            for (int h = 0; h < 400 && !log.Any(x => x.StartsWith("end")); h++)
            {
                s.AdvanceHour();
                if (d.Epidemic.Active)
                {
                    peakSeverity = Math.Max(peakSeverity, d.Epidemic.Severity);
                    HourRecord r = s.History.Last();
                    Assert.AreEqual(d.Epidemic.Damage, r.EpidemicDamage, 0.06, "피해 = S × (1 − R)");
                }
            }
            Assert.AreEqual("warn 42", log[0], "48h − 6h에 예고");
            Assert.AreEqual("start 48", log[1]);
            Assert.IsTrue(log.Any(x => x.StartsWith("end")), "언젠가 끝난다(저항력이 심각도를 넘음)");
            long end = d.Events[0].EndHour;
            Assert.That(end - 48, Is.InRange(36, 72), "2~3일 안에 끝난다(EP1 목표, 설정 수치 약 50h)");
            Assert.Greater(peakSeverity, _config.Disasters.Epidemic.InitialSeverity, "심각도가 오르다가");
            Assert.IsFalse(d.Epidemic.Active);
            Assert.Greater(d.Epidemic.Resistance, 0.2, "저항력이 남아 있다");

            double r0 = d.Epidemic.Resistance;
            for (int h = 0; h < 10; h++) s.AdvanceHour();
            Assert.Less(d.Epidemic.Resistance, r0, "끝난 뒤 저항력은 천천히 준다");
        }

        [Test]
        public void Epidemic_PeaksThenWeakens_WithoutSticking()
        {
            // 이슈 #1(EP1): 예전 규칙(R ≥ S)은 심각도가 100%에 붙어 '끝없이 오른다'로 보였다.
            // 목표 곡선: 피해 정점 24~36h·30~40%, 심각도도 정점 뒤 줄어들고(↓ 표시), 100%에 닿지 않는다.
            Settlement s = NewSettlement();
            DisasterManager d = DisasterManager.Attach(s, OneEpidemicAtGrace(), 1);
            double peakDamage = 0, peakSeverity = 0;
            long peakHour = -1, weakeningFrom = -1;
            bool rose = false;
            for (int h = 0; h < 200; h++)
            {
                double before = d.Epidemic.Severity;
                s.AdvanceHour();
                if (!d.Epidemic.Active) { if (peakHour >= 0) break; continue; }
                long sinceStart = s.CurrentHour - 48;
                if (d.Epidemic.Damage > peakDamage) { peakDamage = d.Epidemic.Damage; peakHour = sinceStart; }
                peakSeverity = Math.Max(peakSeverity, d.Epidemic.Severity);
                if (d.Epidemic.Severity > before) rose = true;
                if (d.Epidemic.Weakening && weakeningFrom < 0) weakeningFrom = sinceStart;
                if (weakeningFrom >= 0) Assert.IsTrue(d.Epidemic.Weakening, "한 번 약해지기 시작하면 끝까지 약해진다");
            }
            Assert.IsTrue(rose, "처음에는 퍼진다(↑)");
            Assert.That(peakHour, Is.InRange(24, 36), "피해 정점 시각");
            Assert.That(peakDamage, Is.InRange(0.30, 0.40), "최대 피해");
            Assert.Less(peakSeverity, 0.9, "심각도가 100%에 붙지 않는다");
            Assert.That(weakeningFrom, Is.InRange(24, 40), "정점 무렵부터 '약해지는 중'");
        }

        [Test]
        public void Epidemic_DamagesProducersOnly_HospitalHalvesNearby()
        {
            Settlement s = NewSettlement(r =>
            {
                r.FreeBuilds[BuildingKinds.Farm] = 2;
                r.FreeBuilds[BuildingKinds.Hospital] = 1;
            });
            Assert.IsTrue(s.RequestBuild(BuildingKinds.Farm, new HexCoord(1, -1)).Success);
            Assert.IsTrue(s.RequestBuild(BuildingKinds.Farm, new HexCoord(-3, 0)).Success);
            Assert.IsTrue(s.RequestBuild(BuildingKinds.Hospital, new HexCoord(2, -1)).Success);
            Facility near = s.FacilityAt(new HexCoord(1, -1)), far = s.FacilityAt(new HexCoord(-3, 0));
            Facility hospital = s.FacilityAt(new HexCoord(2, -1));

            DisasterManager d = DisasterManager.Attach(s, OneEpidemicAtGrace(), 1);
            for (int h = 0; h < 70; h++) s.AdvanceHour();
            Assert.IsTrue(d.Epidemic.Active);
            double damage = d.Epidemic.Damage;
            Assert.Greater(damage, 0.05);
            Assert.AreEqual(1 - damage, d.ProductionFactor(far), 1e-12, "병원에서 먼 농장: 피해 그대로");
            Assert.AreEqual(1 - damage * 0.5, d.ProductionFactor(near), 1e-12, "병원 이웃 농장: 피해 절반");
            Assert.AreEqual(1.0, d.ProductionFactor(hospital), 1e-12, "생산 시설이 아니면 영향 없음");
        }

        [Test]
        public void InitialResistanceEffect_StartsResistance_AndIsTheFloor()
        {
            Settlement s = NewSettlement(r => r.ApplyEffects(new EconomyEffects { InitialEpidemicResistance = 0.3 }));
            DisasterManager d = DisasterManager.Attach(s, OneEpidemicAtGrace(), 1);
            Assert.AreEqual(0.3, d.Epidemic.Resistance, 1e-12, "시작 저항력 30%");
            for (int h = 0; h < 600; h++) s.AdvanceHour();
            Assert.IsFalse(d.Epidemic.Active);
            Assert.AreEqual(0.3, d.Epidemic.Resistance, 1e-9, "끝난 뒤 줄어도 30% 아래로는 안 내려감");
        }

        [Test]
        public void Vaccination_SpeedsResistanceGrowth_ButIsNotImmunity()
        {
            long Duration(bool vaccinated)
            {
                Settlement s = NewSettlement(r =>
                {
                    if (vaccinated) r.ApplyEffects(_config.Equipment.Single(e => e.Id == "vaccination").Effects);
                });
                DisasterManager d = DisasterManager.Attach(s, OneEpidemicAtGrace(), 1);
                Assert.AreEqual(0, d.Epidemic.Resistance, 1e-12, "방역 장비도 시작 저항력은 0");
                for (int h = 0; h < 600; h++) s.AdvanceHour();
                Assert.IsFalse(d.Epidemic.Active);
                return d.Events[0].EndHour - d.Events[0].StartHour;
            }
            long plain = Duration(false), vaccinated = Duration(true);
            Assert.Less(vaccinated, plain, "저항력이 빨리 올라 더 일찍 끝남");
            Assert.Greater(vaccinated, 12, "면역이 아님: 반나절 이상 이어짐");
        }

        [Test]
        public void Vaccination_LowersPeakDamage()
        {
            double Peak(bool vaccinated)
            {
                Settlement s = NewSettlement(r =>
                {
                    if (vaccinated) r.ApplyEffects(_config.Equipment.Single(e => e.Id == "vaccination").Effects);
                });
                DisasterManager d = DisasterManager.Attach(s, OneEpidemicAtGrace(), 1);
                double peak = 0;
                for (int h = 0; h < 300; h++) { s.AdvanceHour(); peak = Math.Max(peak, s.History.Last().EpidemicDamage); }
                return peak;
            }
            Assert.Less(Peak(true), Peak(false));
        }
    }
}
