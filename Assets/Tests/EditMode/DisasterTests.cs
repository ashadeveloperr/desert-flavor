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
    /// <summary>재해 일정과 전염병(감염·회복 모형, 결정 EV1~EV7): 퍼짐·정점·끝, 아픈 주민은 일 못 함, 병원 병상, 방역 장비 면역.</summary>
    public class DisasterTests
    {
        private static EconomyConfig _config;

        [OneTimeSetUp]
        public void Load()
        {
            _config = TestData.Economy();
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
            Assert.AreEqual(36, _config.Disasters.GraceHours, "2026-10-26 AU2: 경매 주기 48h에 맞춰 48 → 36");
            Assert.AreEqual(6, _config.Disasters.WarningHours);
            Assert.IsTrue(_config.Disasters.Types.Any(t => t.Id == "epidemic"));
            Assert.AreEqual(20, _config.GetLevel(BuildingKinds.Hospital, 1).Stone);
            Assert.Greater(_config.GetLevel(BuildingKinds.Hospital, 1).Beds ?? 0, 0, "병원 병상(결정 EV3)");
            Assert.Greater(_config.Equipment.Single(e => e.Id == "vaccination").Effects.EpidemicImmuneShare, 0, "방역 장비 = 처음부터 면역인 주민 비율(결정 EV5)");
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
                    int grace = _config.Disasters.GraceHours;
                    Assert.GreaterOrEqual(e.StartHour, grace, "유예 시간 동안은 재해 없음");
                    Assert.Less(e.StartHour, grace + _config.Disasters.ScheduleWindowHours);
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


        // ───────────── 전염병(감염·회복 모형, 결정 EV1~EV7) ─────────────

        private static readonly HexCoord[] HouseTiles = { new HexCoord(1, 0), new HexCoord(-1, 0), new HexCoord(0, 1) };

        /// <summary>
        /// 주택 3채(무료, 주민 18명)가 있는 정착지. 전염병만 보려고 주민이 물·식량 때문에 떠나지 않게 한다
        /// (주민 물 0, 식량 넉넉 — 설정 객체는 공유하므로 노동력 설정은 사본을 고친다).
        /// </summary>
        private static Settlement NewTown(Action<SettlementRules> tweak = null)
        {
            Settlement s = NewSettlement(r =>
            {
                r.Labor = new LaborSettings
                {
                    FoodPerResidentPerHour = r.Labor.FoodPerResidentPerHour, WaterPerResidentPerHour = 0,
                    LeavePerHour = r.Labor.LeavePerHour, ReturnPerHour = r.Labor.ReturnPerHour,
                    FreeHouses = r.Labor.FreeHouses, CrewPerConstructionSlot = r.Labor.CrewPerConstructionSlot,
                };
                r.FreeBuilds[BuildingKinds.House] = HouseTiles.Length;
                r.BaseFoodStorage = r.StarterFood = 100000;
                tweak?.Invoke(r);
            });
            foreach (HexCoord t in HouseTiles) Assert.IsTrue(s.RequestBuild(BuildingKinds.House, t).Success);
            TestData.MoveIn(s); // 주택은 빈 채로 생긴다(결정 HI1)
            Assert.AreEqual(18, s.Residents, 1e-9);
            return s;
        }

        private static (double peak, long peakHour, long duration) RunEpidemic(Settlement s, DisasterManager d)
        {
            double peak = 0;
            long peakHour = -1;
            for (int h = 0; h < 400 && d.Events[0].State != DisasterState.Ended; h++)
            {
                s.AdvanceHour();
                if (d.Epidemic.SickShare > peak) { peak = d.Epidemic.SickShare; peakHour = s.CurrentHour - d.Events[0].StartHour; }
            }
            Assert.AreEqual(DisasterState.Ended, d.Events[0].State, "언젠가 끝난다");
            return (peak, peakHour, d.Events[0].EndHour - d.Events[0].StartHour);
        }

        [Test]
        public void Epidemic_WarnsStarts_SpreadsPeaksAndEnds_LeavingImmunity()
        {
            Settlement s = NewTown();
            long attached = s.CurrentHour; // 재해 일정은 붙인 시각부터(입주에 한 시간 씀)
            DisasterManager d = DisasterManager.Attach(s, OneEpidemicAtGrace(), 1);
            var log = new List<string>();
            d.Warned += e => log.Add($"warn {s.CurrentHour}");
            d.Started += e => log.Add($"start {s.CurrentHour}");
            bool rose = false, weakened = false;
            double previous = 0;
            for (int h = 0; h < 400 && d.Events[0].State != DisasterState.Ended; h++)
            {
                s.AdvanceHour();
                if (!d.Epidemic.Active) continue;
                if (d.Epidemic.Sick > previous + 1e-9) rose = true;
                if (d.Epidemic.Weakening) weakened = true;
                if (weakened) Assert.IsTrue(d.Epidemic.Weakening, "정점을 지나면 끝까지 줄어든다");
                previous = d.Epidemic.Sick;
                Assert.AreEqual(18, d.Epidemic.Susceptible + d.Epidemic.Sick + d.Epidemic.Immune, 1e-6, "걸릴 수 있음 + 아픔 + 면역 = 주민");
            }
            int grace = _config.Disasters.GraceHours;
            Assert.AreEqual($"warn {attached + grace - 6}", log[0], "유예 − 6h에 예고");
            Assert.AreEqual($"start {attached + grace}", log[1]);
            Assert.IsTrue(rose, "처음에는 퍼진다(↑)");
            Assert.IsTrue(weakened, "정점 뒤 약해진다(↓)");
            Assert.IsFalse(d.Epidemic.Active);
            Assert.AreEqual(0, d.Epidemic.Sick, 1e-12, "끝나면 남은 환자도 회복");
            Assert.Greater(d.Epidemic.Immune, 9, "회복한 주민은 면역으로 남는다");
        }

        [Test]
        public void SickResidents_CannotWork()
        {
            Settlement s = NewTown();
            DisasterManager d = DisasterManager.Attach(s, OneEpidemicAtGrace(), 1);
            while (!d.Epidemic.Active || d.Epidemic.Sick < 3) s.AdvanceHour();
            s.AdvanceHour();
            LaborAssignment labor = s.LastLabor;
            Assert.Greater(labor.Sick, 2);
            Assert.AreEqual(labor.Residents - labor.Sick, labor.IdleResidents, 1e-9, "일터가 없으면 아프지 않은 주민만 남는다");
            Assert.AreEqual(d.Epidemic.Sick, labor.Sick, 1e-6, "화면 배치(LastLabor)는 정산이 끝난 뒤의 환자 수로 다시 계산된다");
        }

        [Test]
        public void Hospital_SpeedsRecovery_LowersPeakAndShortens()
        {
            (double peak, long peakHour, long duration) Run(bool hospital)
            {
                Settlement s = NewTown(r => { if (hospital) r.FreeBuilds[BuildingKinds.Hospital] = 1; });
                if (hospital) Assert.IsTrue(s.RequestBuild(BuildingKinds.Hospital, new HexCoord(2, -1)).Success);
                DisasterManager d = DisasterManager.Attach(s, OneEpidemicAtGrace(), 1);
                Assert.AreEqual(hospital ? s.Rules.Config.GetLevel(BuildingKinds.Hospital, 1).Beds.Value : 0, d.HospitalBeds, 1e-12);
                return RunEpidemic(s, d);
            }
            var plain = Run(false);
            var withHospital = Run(true);
            Assert.Less(withHospital.peak, plain.peak, "병원이 정점을 낮춘다");
            Assert.Less(withHospital.duration, plain.duration, "병원이 빨리 끝낸다");
            Assert.Greater(_config.Disasters.Epidemic.HospitalRecoveryPerHour, _config.Disasters.Epidemic.HomeRecoveryPerHour, "병원 회복이 집보다 빠르다(결정 EV3)");
        }

        [Test]
        public void Vaccination_MakesAShareImmune_AndLowersPeak()
        {
            double vaccine = _config.Equipment.Single(e => e.Id == "vaccination").Effects.EpidemicImmuneShare;
            double Peak(bool vaccinated)
            {
                Settlement s = NewTown(r => { if (vaccinated) r.ApplyEffects(_config.Equipment.Single(e => e.Id == "vaccination").Effects); });
                DisasterManager d = DisasterManager.Attach(s, OneEpidemicAtGrace(), 1);
                while (!d.Epidemic.Active) s.AdvanceHour();
                if (vaccinated) Assert.GreaterOrEqual(d.Epidemic.Immune, vaccine * (18 - d.Epidemic.Sick) - 1e-9, "처음부터 면역인 주민 비율(결정 EV5)");
                else Assert.AreEqual(d.Epidemic.RecoveredLastHour, d.Epidemic.Immune, 1e-9, "방역이 없으면 면역은 첫 시간에 회복한 주민뿐");
                return RunEpidemic(s, d).peak;
            }
            Assert.Less(Peak(true), Peak(false));
        }

        [Test]
        public void Epidemic_DoesNotScaleProduction()
        {
            // 결정 EV2: 전염병은 생산 배율이 아니라 일하지 못하는 주민으로 나타난다.
            Settlement s = NewTown(r => r.FreeBuilds[BuildingKinds.Farm] = 1);
            Assert.IsTrue(s.RequestBuild(BuildingKinds.Farm, new HexCoord(2, -1)).Success);
            DisasterManager d = DisasterManager.Attach(s, OneEpidemicAtGrace(), 1);
            while (!d.Epidemic.Active) s.AdvanceHour();
            Assert.AreEqual(1.0, d.ProductionFactor(s.FacilityAt(new HexCoord(2, -1))), 1e-12);
        }
    }
}
