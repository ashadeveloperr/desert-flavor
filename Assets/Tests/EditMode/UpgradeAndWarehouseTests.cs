using System.IO;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;
using NUnit.Framework;
using UnityEngine;

namespace DesertSettlement.Core.Tests
{
    /// <summary>로드맵 1-③ L3 개량·창고 폐쇄(2026-10-23). 결정 근거는 Docs/decision_log_auto.md U1~U3.</summary>
    public class UpgradeAndWarehouseTests
    {
        private static EconomyConfig _config;
        private static readonly HexCoord WarehouseTile = new HexCoord(1, 0);

        [OneTimeSetUp]
        public void Load()
        {
            _config = TestData.Economy();
        }

        [Test]
        public void LevelCap_IsThree_LimitedByEachFacilityData()
        {
            SettlementRules rules = SettlementRules.FromConfig(_config);
            Assert.AreEqual(3, rules.MaxLevelOf(BuildingKinds.Mine), "광산 L3 개방");
            Assert.AreEqual(3, rules.MaxLevelOf(BuildingKinds.Well));
            Assert.AreEqual(2, rules.MaxLevelOf(BuildingKinds.AuctionHall), "설정에 L2까지만 있는 시설은 L2");
            Assert.AreEqual(1, rules.MaxLevelOf(BuildingKinds.Hospital));
        }

        private static Settlement WithWarehouse(out Facility warehouse)
        {
            var map = new HexMap(3);
            map.GetTile(HexCoord.Zero).Terrain = TerrainType.WaterSource;
            SettlementRules rules = SettlementRules.FromConfig(_config);
            rules.FreeBuilds[BuildingKinds.Warehouse] = 1;
            var s = new Settlement(map, rules);
            Assert.IsTrue(s.RequestBuild(BuildingKinds.Warehouse, WarehouseTile).Success);
            warehouse = s.FacilityAt(WarehouseTile);
            return s;
        }

        [Test]
        public void Warehouse_CanClose_WhenStockFits_AndReopen()
        {
            Settlement s = WithWarehouse(out Facility w);
            double capOpen = s.FoodCapacity;
            Assert.IsTrue(s.SetClosed(w.Id, true).Success, "재고가 기본 한도 안이면 닫을 수 있다");
            Assert.AreEqual(s.Rules.BaseFoodStorage, s.FoodCapacity, 1e-9, "닫은 창고는 저장 한도에 안 들어간다");
            Assert.IsTrue(s.SetClosed(w.Id, false).Success);
            Assert.AreEqual(capOpen, s.FoodCapacity, 1e-9);
        }

        [Test]
        public void Warehouse_CannotClose_WhenStockWouldOverflow()
        {
            Settlement s = WithWarehouse(out Facility w);
            s.BuyFood(70);
            Assume.That(s.Food, Is.GreaterThan(s.Rules.BaseFoodStorage), "기본 한도를 넘게 사 둔다");
            Assert.IsFalse(s.SetClosed(w.Id, true).Success, "닫으면 재고가 한도를 넘으므로 거절");
            Assert.AreEqual(FacilityState.Operating, w.State);
        }
    }
}
