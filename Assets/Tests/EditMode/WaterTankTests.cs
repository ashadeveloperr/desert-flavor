using System.IO;
using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;
using DesertSettlement.Core.Water;
using NUnit.Framework;
using UnityEngine;

namespace DesertSettlement.Core.Tests
{
    /// <summary>물탱크(로드맵 1-②, 2026-10-23). 규칙은 Docs/decision_log_auto.md T1~T7.</summary>
    public class WaterTankTests
    {
        private static EconomyConfig _config;

        // 한 줄: 위쪽 농장(-1,0) ─ 우물(0,0) ─ 빈 사막(1,0) ─ 탱크(2,0) ─ 아래쪽 농장(3,0)
        // 우물과 탱크 사이에 빈 타일을 두어, 그 사이 수로(빈 타일 북서 변)를 닫으면 우물과 탱크가 확실히 끊기게 한다.
        private static readonly HexCoord WellTile = HexCoord.Zero;
        private static readonly HexCoord GapTile = new HexCoord(1, 0);
        private static readonly HexCoord TankTile = new HexCoord(2, 0);
        private static readonly HexCoord DownFarmTile = new HexCoord(3, 0);
        private static readonly HexCoord UpFarmTile = new HexCoord(-1, 0);
        private static readonly EdgeId WellToTank = EdgeId.Of(GapTile, HexDirection.NorthWest);

        [OneTimeSetUp]
        public void Load()
        {
            _config = EconomyConfigLoader.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "Data", "Config", "economy_balance_v0.8_config.json")));
        }

        private sealed class Site
        {
            public Settlement S;
            public Facility Well, Tank, DownFarm, UpFarm;
        }

        /// <summary>시설은 무료 완공 배치, 수로는 공사(8시간이면 모두 완공).</summary>
        private static Site Build(bool withTank = true)
        {
            var map = new HexMap(3);
            map.GetTile(WellTile).Terrain = TerrainType.WaterSource;
            SettlementRules rules = SettlementRules.FromConfig(_config);
            rules.FreeBuilds[BuildingKinds.Well] = 1;
            rules.FreeBuilds[BuildingKinds.Farm] = 2;
            rules.FreeBuilds[BuildingKinds.WaterTank] = 1;
            rules.StartJobsOnCommand = false; // 수로 완공 시각을 고정(10시간 뒤 탱크가 아직 차는 중이어야 충전 확인이 됨)
            var s = new Settlement(map, rules);
            Assert.IsTrue(s.RequestBuild(BuildingKinds.Well, WellTile).Success);
            Assert.IsTrue(s.RequestBuild(BuildingKinds.Farm, DownFarmTile).Success);
            Assert.IsTrue(s.RequestBuild(BuildingKinds.Farm, UpFarmTile).Success);
            if (withTank) Assert.IsTrue(s.RequestBuild(BuildingKinds.WaterTank, TankTile).Success);
            // 우물 → 빈 타일 위쪽 둘레 → 탱크 위쪽 둘레 → 농장. 각 타일의 동쪽 변이 다음 타일과의 공유 변.
            // 위쪽 농장은 우물 서쪽 공유 변으로 우물에 붙는다(탱크를 지나지 않음).
            foreach (EdgeId e in new[]
            {
                EdgeId.Of(WellTile, HexDirection.East), WellToTank, EdgeId.Of(GapTile, HexDirection.NorthEast), EdgeId.Of(GapTile, HexDirection.East),
                EdgeId.Of(TankTile, HexDirection.NorthWest), EdgeId.Of(TankTile, HexDirection.NorthEast), EdgeId.Of(TankTile, HexDirection.East),
                EdgeId.Of(WellTile, HexDirection.West),
            })
            {
                Assert.IsTrue(s.RequestCanal(e).Success, e.ToString());
            }
            s.AdvanceHours(10);
            Facility At(HexCoord t) => s.FacilityAt(t);
            return new Site { S = s, Well = At(WellTile), Tank = withTank ? At(TankTile) : null, DownFarm = At(DownFarmTile), UpFarm = At(UpFarmTile) };
        }

        [Test]
        public void Tank_CanBeBuiltOnDesertOnly()
        {
            var map = new HexMap(3);
            map.GetTile(WellTile).Terrain = TerrainType.WaterSource;
            var s = new Settlement(map, SettlementRules.FromConfig(_config));
            Assert.IsTrue(s.CanBuild(BuildingKinds.WaterTank, TankTile).Success, "사막 가능");
            Assert.IsFalse(s.CanBuild(BuildingKinds.WaterTank, WellTile).Success, "수원 불가");
        }

        [Test]
        public void Tank_ChargesOnlyFromLeftover_UpToCapacity()
        {
            Site x = Build();
            x.S.AdvanceHour();
            Assert.AreEqual(WaterTankMode.Charging, x.Tank.TankMode);
            Assert.Greater(x.Tank.StoredWater, 0);
            Assert.LessOrEqual(x.Tank.TankFlowLastHour, x.S.TankCapacityOf(x.Tank));
            Assert.LessOrEqual(x.Tank.TankFlowLastHour, _config.GetLevel(BuildingKinds.WaterTank, 1).ChargePerHour.Value + 1e-9, "시간당 충전 상한");
            Assert.AreEqual(1.0, x.S.WaterRateOf(x.DownFarm), 1e-9, "탱크가 있어도 시설은 먼저 다 받는다");
            Assert.AreEqual(1.0, x.S.WaterRateOf(x.UpFarm), 1e-9);

            x.S.AdvanceHours(20);
            Assert.AreEqual(x.S.TankCapacityOf(x.Tank), x.Tank.StoredWater, 1e-9, "가득 차면 멈춤");
            Assert.AreEqual(WaterTankMode.Idle, x.Tank.TankMode);
        }

        [Test]
        public void Tank_DischargesToDownstreamOnly_WhenWellCloses()
        {
            Site x = Build();
            x.S.AdvanceHours(5);
            double before = x.Tank.StoredWater;
            Assert.IsTrue(x.S.SetClosed(x.Well.Id, true).Success);
            x.S.AdvanceHour();

            Assert.AreEqual(WaterTankMode.Discharging, x.Tank.TankMode, "방출 우선, 같은 시간에 충전 없음");
            Assert.Less(x.Tank.StoredWater, before);
            Assert.Greater(x.S.WaterRateOf(x.DownFarm), 0.8, "하류 농장은 탱크 물을 받는다(손실만큼 덜)");
            Assert.AreEqual(0.0, x.S.WaterRateOf(x.UpFarm), 1e-12, "우물 반대편(탱크를 지나지 않는) 농장은 못 받는다");
        }

        [Test]
        public void ClosedCanalUpstream_TankStillFeedsDownstream()
        {
            Site x = Build();
            x.S.AdvanceHours(5);
            Facility upstreamCanal = x.S.FacilityOn(WellToTank, BuildingKinds.CanalEdge);
            Assert.IsTrue(x.S.SetClosed(upstreamCanal.Id, true).Success);
            x.S.AdvanceHour();

            Assert.AreEqual(1.0, x.S.WaterRateOf(x.UpFarm), 1e-9, "우물은 그대로 위쪽 농장에 준다");
            Assert.AreEqual(WaterTankMode.Discharging, x.Tank.TankMode, "상류 수로를 닫아도 탱크 물은 하류로 간다(T4)");
            Assert.Greater(x.S.WaterRateOf(x.DownFarm), 0.8);
        }

        [Test]
        public void WithoutTank_ClosedUpstreamCanal_CutsDownstream()
        {
            Site x = Build(withTank: false);
            Facility upstreamCanal = x.S.FacilityOn(WellToTank, BuildingKinds.CanalEdge);
            Assert.IsTrue(x.S.SetClosed(upstreamCanal.Id, true).Success);
            x.S.AdvanceHour();
            Assert.AreEqual(0.0, x.S.WaterRateOf(x.DownFarm), 1e-12, "비교: 탱크가 없으면 단수");
        }

        [Test]
        public void WaterRoutes_ListTheCanalsUsed_IncludingTankDischarge()
        {
            // 물길 표시(2026-10-23): 물 배분이 실제로 쓴 수로를 돌려준다.
            Site x = Build();
            x.S.AdvanceHour();
            var received = WaterRoutes.EdgesOf(WaterRoutes.ReceivedBy(x.S.LastWater, x.DownFarm.Id.ToString()));
            CollectionAssert.Contains(received, WellToTank, "우물 → 아래쪽 농장 물길은 우물~탱크 사이 수로를 지난다");
            CollectionAssert.DoesNotContain(received, EdgeId.Of(WellTile, HexDirection.West), "반대편 수로는 지나지 않는다");
            Assert.IsNotEmpty(WaterRoutes.SentBy(x.S.LastWater, x.Well.Id.ToString()), "우물에서 나간 물길");

            Assert.IsTrue(x.S.SetClosed(x.Well.Id, true).Success);
            x.S.AdvanceHour();
            var fromTank = WaterRoutes.SentBy(x.S.LastWater, "tank" + x.Tank.Id);
            Assert.IsNotEmpty(fromTank, "우물을 닫으면 탱크 방출 물길이 보인다");
            Assert.AreEqual(x.DownFarm.Id.ToString(), x.S.LastWater.Consumers.Values.Single(r => r.Intakes.Any(f => f.SourceId == "tank" + x.Tank.Id)).Id);
        }

        [Test]
        public void ClosedTank_KeepsWater_AndDoesNothing()
        {
            Site x = Build();
            x.S.AdvanceHours(5);
            double stored = x.Tank.StoredWater;
            Assert.IsTrue(x.S.SetClosed(x.Tank.Id, true).Success);
            Assert.IsTrue(x.S.SetClosed(x.Well.Id, true).Success);
            x.S.AdvanceHour();
            Assert.AreEqual(stored, x.Tank.StoredWater, 1e-12);
            Assert.AreEqual(WaterTankMode.Idle, x.Tank.TankMode);
            Assert.AreEqual(0.0, x.S.WaterRateOf(x.DownFarm), 1e-12);
        }
    }
}
