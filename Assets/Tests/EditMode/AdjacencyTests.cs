using System;
using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;
using NUnit.Framework;

namespace DesertSettlement.Core.Tests
{
    /// <summary>인접 보너스(결정 AJ1~AJ4)와 식품 부패(결정 DC1~DC2).</summary>
    public class AdjacencyTests
    {
        private static readonly HexCoord Center = HexCoord.Zero;

        /// <summary>물 없는 사막 맵. 무료 배치(종류마다 6개)로 시설을 바로 운영 중으로 놓는다(석재·공사 없음).</summary>
        private static Settlement NewSettlement(Action<SettlementRules> tweak = null)
        {
            EconomyConfig config = TestData.Economy();
            SettlementRules rules = SettlementRules.FromConfig(config);
            rules.StartingCash = 5000;
            foreach (string kind in new[] { BuildingKinds.Farm, BuildingKinds.Orchard, BuildingKinds.Mill, BuildingKinds.House, BuildingKinds.Warehouse })
                rules.FreeBuilds[kind] = 6; // 무료 배치는 정착지를 만들 때 정해진다
            tweak?.Invoke(rules);
            return new Settlement(new HexMap(3), rules);
        }

        private static Facility Place(Settlement s, string kind, HexCoord tile)
        {
            Assert.IsTrue(s.RequestBuild(kind, tile).Success, $"{kind} @ {tile}");
            Facility f = s.FacilityAt(tile);
            Assert.AreEqual(FacilityState.Operating, f.State);
            return f;
        }

        private static AdjacencySettings Adj(Settlement s) => s.Rules.Adjacency;

        [Test]
        public void Config_AdjacencyKindsExist()
        {
            // 오타가 나면 보너스가 조용히 사라지므로 설정의 시설 이름을 확인한다.
            EconomyConfig config = TestData.Economy();
            foreach (string kind in config.Adjacency.ClusterKinds) Assert.IsNotNull(config.GetLevel(kind, 1), kind);
            foreach (var pair in config.Adjacency.InputSources)
            {
                Assert.IsNotNull(SettlementRules.InputOf(pair.Key), $"{pair.Key}는 가공 시설");
                Assert.AreEqual(SettlementRules.InputOf(pair.Key), SettlementRules.GoodsOf(pair.Value), $"{pair.Value}가 {pair.Key}의 원료를 만든다");
            }
        }

        [Test]
        public void Cluster_SameKindNeighbors_AddOutputPerTile()
        {
            Settlement s = NewSettlement();
            Facility a = Place(s, BuildingKinds.Farm, Center);
            Assert.AreEqual(0, s.ClusterBonus(a.Kind, a.Tile), 1e-12, "혼자면 보너스 없음");
            Place(s, BuildingKinds.Farm, Center.Neighbor(HexDirection.East));
            Place(s, BuildingKinds.Farm, Center.Neighbor(HexDirection.West));
            Place(s, BuildingKinds.Orchard, Center.Neighbor(HexDirection.NorthEast)); // 다른 종류는 세지 않는다
            Assert.AreEqual(2 * Adj(s).ClusterOutputPerNeighbor, s.ClusterBonus(a.Kind, a.Tile), 1e-12);
            Assert.AreEqual(0, s.ClusterBonus(BuildingKinds.Mill, Center), 1e-12, "가공 시설은 군집 대상이 아님");
        }

        [Test]
        public void Cluster_IgnoresNeighborsUnderConstruction()
        {
            Settlement s = NewSettlement(r => { r.StarterStone = 100; r.FreeBuilds[BuildingKinds.Farm] = 1; });
            Facility a = Place(s, BuildingKinds.Farm, Center);
            Assert.IsTrue(s.RequestBuild(BuildingKinds.Farm, Center.Neighbor(HexDirection.East)).Success);
            Assert.AreEqual(FacilityState.Constructing, s.FacilityAt(Center.Neighbor(HexDirection.East)).State);
            Assert.AreEqual(0, s.ClusterBonus(a.Kind, a.Tile), 1e-12, "공사 중인 이웃은 세지 않는다");
        }

        [Test]
        public void InputSaving_PerSourceNeighbor_CappedAtMax()
        {
            Settlement s = NewSettlement();
            Place(s, BuildingKinds.Mill, Center);
            Place(s, BuildingKinds.Farm, Center.Neighbor(HexDirection.East));
            Assert.AreEqual(Adj(s).InputSavingPerNeighbor, s.InputSaving(BuildingKinds.Mill, Center), 1e-12);
            Assert.AreEqual(0, s.InputSaving(BuildingKinds.Winery, Center), 1e-12, "포도주 공방의 원료는 과수원");
            for (int d = 1; d < 6; d++) Place(s, BuildingKinds.Farm, Center.Neighbor((HexDirection)d));
            Assert.AreEqual(Math.Min(Adj(s).InputSavingMax, 6 * Adj(s).InputSavingPerNeighbor), s.InputSaving(BuildingKinds.Mill, Center), 1e-12);
            Assert.Less(Adj(s).InputSavingMax, 6 * Adj(s).InputSavingPerNeighbor, "6칸이면 상한에 걸린다(지금 설정)");
        }

        [Test]
        public void HouseCluster_RaisesArrivalSpeed()
        {
            Settlement s = NewSettlement();
            Facility a = Place(s, BuildingKinds.House, Center);
            double alone = s.ArrivalPerHour(a);
            Assert.AreEqual(s.ArrivalPerHour(s.HouseCapacity(a)), alone, 1e-12);
            Place(s, BuildingKinds.House, Center.Neighbor(HexDirection.East));
            Assert.AreEqual(alone * (1 + Adj(s).HouseArrivalPerNeighbor), s.ArrivalPerHour(a), 1e-12);
        }

        [Test]
        public void Spoilage_WarehouseClusterLowersRate_ByAverageNeighbors()
        {
            Settlement s = NewSettlement();
            SpoilageSettings sp = s.Rules.Spoilage;
            Assert.AreEqual(sp.FoodPerDay, s.FoodSpoilagePerDay, 1e-12, "창고가 없으면 기본 부패율");
            Place(s, BuildingKinds.Warehouse, Center);
            Assert.AreEqual(sp.FoodPerDay, s.FoodSpoilagePerDay, 1e-12, "떨어진 창고 하나는 줄이지 않는다");
            // 삼각형(서로 맞닿은 세 칸): 창고마다 이웃 2칸 → 평균 2
            Place(s, BuildingKinds.Warehouse, Center.Neighbor(HexDirection.East));
            Place(s, BuildingKinds.Warehouse, Center.Neighbor(HexDirection.NorthEast));
            Assert.AreEqual(2, s.AverageWarehouseNeighbors(), 1e-12);
            Assert.AreEqual(sp.FoodPerDay - 2 * sp.WarehouseNeighborReductionPerDay, s.FoodSpoilagePerDay, 1e-12);
        }

        [Test]
        public void Spoilage_RemovesFoodStockEachHour_StoneKeeps()
        {
            Settlement s = NewSettlement(r => r.Labor = null); // 먹지 않게(노동력 끔: 시설이 없으면 수요 0)
            double food = s.Food, stone = s.Stone;
            Assert.Greater(food, 0);
            HourRecord h = s.AdvanceHour();
            Assert.AreEqual(food * s.FoodSpoilagePerDay / 24, h.Spoiled, 1e-9, "하루 부패율의 1/24");
            Assert.AreEqual(food - h.Spoiled, s.Food, 1e-9);
            Assert.AreEqual(stone, s.Stone, 1e-9, "석재는 썩지 않는다");
        }
    }
}
