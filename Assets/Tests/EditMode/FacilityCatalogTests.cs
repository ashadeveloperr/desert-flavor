using System.IO;
using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;
using NUnit.Framework;
using UnityEngine;

namespace DesertSettlement.Core.Tests
{
    /// <summary>시설 목록(2026-10-24 CL1~CL3)이 설정 JSON·지형 규칙과 어긋나지 않는지.</summary>
    public class FacilityCatalogTests
    {
        private static EconomyConfig LoadConfig() => EconomyConfigLoader.Parse(
            File.ReadAllText(Path.Combine(Application.dataPath, "Data", "Config", "economy_balance_v0.8_config.json")));

        [Test]
        public void Catalog_MatchesConfigBuildingLevels()
        {
            EconomyConfig config = LoadConfig();
            CollectionAssert.AreEquivalent(config.BuildingLevels.Keys, FacilityCatalog.All.Select(t => t.Kind),
                "시설 목록과 JSON buildingLevels가 같은 종류를 가져야 한다(새 시설은 둘 다에 추가)");
            Assert.AreEqual(FacilityCatalog.All.Count, FacilityCatalog.All.Select(t => t.Name).Distinct().Count(), "이름이 겹치지 않는다");
            Assert.IsTrue(FacilityCatalog.All.All(t => FacilityCatalog.Groups.Contains(t.Group)), "분류는 도구 막대 분류 중 하나");
        }

        [Test]
        public void AllowedTerrain_ComesFromCatalog_WithOriginalRules()
        {
            SettlementRules rules = SettlementRules.FromConfig(LoadConfig());
            Assert.AreEqual(new[] { TerrainType.WaterSource }, rules.AllowedTerrain[BuildingKinds.Well]);
            Assert.AreEqual(new[] { TerrainType.OreVein }, rules.AllowedTerrain[BuildingKinds.Mine]);
            Assert.AreEqual(new[] { TerrainType.Bedrock }, rules.AllowedTerrain[BuildingKinds.Quarry]);
            foreach (string kind in new[] { BuildingKinds.Farm, BuildingKinds.Warehouse, BuildingKinds.AuctionHall, BuildingKinds.Hospital, BuildingKinds.WaterTank })
                Assert.AreEqual(new[] { TerrainType.Desert }, rules.AllowedTerrain[kind], kind);
            Assert.IsFalse(rules.AllowedTerrain.ContainsKey(BuildingKinds.CanalEdge), "수로는 변에 지어 지형 규칙 없음");
            Assert.AreEqual("물탱크", FacilityCatalog.NameOf(BuildingKinds.WaterTank));
            Assert.AreEqual("unknownKind", FacilityCatalog.NameOf("unknownKind"), "목록에 없으면 종류 이름 그대로");
        }

        [Test]
        public void JobHours_AppliesConstructionMultiplier()
        {
            SettlementRules rules = SettlementRules.FromConfig(LoadConfig());
            rules.ConstructionHoursMultiplier = 0.5;
            Assert.AreEqual(5, rules.JobHours(10));
            Assert.AreEqual(5, rules.JobHours(9), "4.5 → 반올림 5");
            Assert.AreEqual(1, rules.JobHours(1), "최소 1시간");
            Assert.AreEqual(3, rules.JobHours(10, demolish: true), "철거 = 10 × 0.5 × 0.5 = 2.5 → 올림 3");
        }
    }
}
