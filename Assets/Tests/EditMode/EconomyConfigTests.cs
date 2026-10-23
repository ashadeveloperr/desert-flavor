using System.IO;
using DesertSettlement.Core.Config;
using NUnit.Framework;
using UnityEngine;

namespace DesertSettlement.Core.Tests
{
    public class EconomyConfigTests
    {
        private static string ConfigPath =>
            Path.Combine(Application.dataPath, "Data", "Config", "economy_balance_v0.8_config.json");

        private static string ReadRealConfig() => File.ReadAllText(ConfigPath);

        [Test]
        public void RealConfigFile_LoadsAndPassesValidation()
        {
            EconomyConfig config = EconomyConfigLoader.Parse(ReadRealConfig());

            Assert.AreEqual(EconomyConfigLoader.SupportedSchema, config.Schema);
            Assert.AreEqual(180.0, config.ConfirmedRules.GameDayRealSecondsAt1x); // 2026-10-23 흐름 조정 FL1(원본 90)
            Assert.AreEqual(0.5, config.ConfirmedRules.ConstructionHoursMultiplier);
            Assert.AreEqual(2, config.ConfirmedRules.ConstructionSlots);
            Assert.AreEqual(0.5, config.ConfirmedRules.PostStartCancelStoneRefundRatio);
            Assert.AreEqual(1, config.Prototype.EconomicTickGameHours);
            Assert.AreEqual(0.05, config.Prototype.CanalLossPerWholeEdge);
        }

        [Test]
        public void RealConfigFile_BuildingLevelsMapToTypedFields()
        {
            EconomyConfig config = EconomyConfigLoader.Parse(ReadRealConfig());

            BuildingLevel wellL1 = config.GetLevel(BuildingKinds.Well, 1);
            Assert.AreEqual(100.0, wellL1.WaterSupply);
            Assert.AreEqual(8.0, wellL1.Stone);
            Assert.IsNull(wellL1.WaterDemand, "우물은 물 수요 필드가 없다");

            Assert.AreEqual(3, config.MaxLevel(BuildingKinds.Mine));
            Assert.AreEqual(25.0, config.GetLevel(BuildingKinds.Mine, 3).OreProduction);
            Assert.AreEqual(100.0, config.GetLevel(BuildingKinds.CanalEdge, 1).CapacityPerHour);
            Assert.IsNotNull(config.GetLevel(BuildingKinds.AuctionHall, 2).UpgradeEffectProposal);
            Assert.IsNull(config.GetLevel(BuildingKinds.Hospital, 2), "없는 레벨은 null");
            Assert.AreEqual(1.2, config.AuctionSeed.Infra.Maximum);
            Assert.AreEqual(1.03, config.Purchase.Stone.IncrementMultiplier);
        }

        [Test]
        public void WrongSchema_IsRejectedWithReadableMessage()
        {
            string json = ReadRealConfig().Replace(EconomyConfigLoader.SupportedSchema, "desert-settlement-economy-v0.7");
            var e = Assert.Throws<ConfigValidationException>(() => EconomyConfigLoader.Parse(json));
            StringAssert.Contains("schema", e.Message);
        }

        [Test]
        public void MissingRequiredLevelField_IsReported()
        {
            const string json = @"{
              ""schema"": ""desert-settlement-economy-v0.8"",
              ""confirmedRules"": { ""gameDayRealSecondsAt1x"": 90, ""constructionSlots"": 2, ""postStartCancelStoneRefundRatio"": 0.5,
                                   ""auctionHistoryGameHours"": 24, ""auctionRepeatsGameHours"": 96 },
              ""prototype"": { ""economicTickGameHours"": 1 },
              ""buildingLevels"": { ""well"": [ { ""stone"": 8, ""foodUse"": 0.25, ""upkeep"": 0.6 } ] }
            }";
            var e = Assert.Throws<ConfigValidationException>(() => EconomyConfigLoader.Parse(json));
            CollectionAssert.Contains(e.Problems, "buildingLevels.well[L1].hours가 없습니다.");
            CollectionAssert.Contains(e.Problems, "buildingLevels.farm가 없습니다.");
        }

        [Test]
        public void BrokenJson_IsRejected()
        {
            Assert.Throws<ConfigValidationException>(() => EconomyConfigLoader.Parse("{ not json"));
            Assert.Throws<ConfigValidationException>(() => EconomyConfigLoader.Parse(""));
        }
    }
}
