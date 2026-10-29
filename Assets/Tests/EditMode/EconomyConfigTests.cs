using System.IO;
using DesertSettlement.Core.Config;
using NUnit.Framework;
using UnityEngine;

namespace DesertSettlement.Core.Tests
{
    public class EconomyConfigTests
    {
        private static string ConfigPath =>
            TestData.EconomyConfigPath;

        private static string ReadRealConfig() => File.ReadAllText(ConfigPath);

        [Test]
        public void RealConfigFile_LoadsAndPassesValidation()
        {
            EconomyConfig config = EconomyConfigLoader.Parse(ReadRealConfig());

            Assert.AreEqual(EconomyConfigLoader.SupportedSchema, config.Schema);
            Assert.AreEqual(180.0, config.ConfirmedRules.GameDayRealSecondsAt1x); // 결정 FL1(원본 v0.8은 90)
            Assert.AreEqual(0.5, config.ConfirmedRules.ConstructionHoursMultiplier);
            Assert.AreEqual(2, config.ConfirmedRules.ConstructionSlots);
            Assert.AreEqual(0.5, config.ConfirmedRules.PostStartCancelStoneRefundRatio);
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
              ""prototype"": { ""startingCash"": 500 },
              ""buildingLevels"": { ""well"": [ { ""stone"": 8, ""foodUse"": 0.25, ""upkeep"": 0.6 } ] }
            }";
            var e = Assert.Throws<ConfigValidationException>(() => EconomyConfigLoader.Parse(json));
            CollectionAssert.Contains(e.Problems, "buildingLevels.well[L1].hours가 없습니다.");
            CollectionAssert.Contains(e.Problems, "buildingLevels.farm가 없습니다.");
        }

        [Test]
        public void UnknownKey_IsRejectedWithItsPath_ButUnderscoreNotesAreAllowed()
        {
            // 결정 CF1: 키 오타가 조용히 기본값(0)으로 읽히지 않게 한다.
            var top = Assert.Throws<ConfigValidationException>(() => EconomyConfigLoader.Parse(ReadRealConfig().Replace("\"graceHours\"", "\"graceHour\"")));
            CollectionAssert.Contains(top.Problems, "알 수 없는 키 'disasters.graceHour' — 오타인지 확인한다(설명용 키는 '_'로 시작).");

            var nested = Assert.Throws<ConfigValidationException>(() =>
                EconomyConfigLoader.Parse(ReadRealConfig().Replace("\"capacityPerHour\": 100,", "\"capacityPerHour\": 100, \"capacityPerHr\": 1,")));
            StringAssert.Contains("'buildingLevels.canalEdge[0].capacityPerHr'", nested.Message, "배열 안의 레벨에서도 찾는다");

            StringAssert.Contains("\"_note\"", ReadRealConfig());
            Assert.DoesNotThrow(() => EconomyConfigLoader.Parse(ReadRealConfig().Replace("\"disasters\": {", "\"disasters\": { \"_memo\": \"설명\",")));
        }

        [Test]
        public void BrokenJson_IsRejected()
        {
            Assert.Throws<ConfigValidationException>(() => EconomyConfigLoader.Parse("{ not json"));
            Assert.Throws<ConfigValidationException>(() => EconomyConfigLoader.Parse(""));
        }
    }
}
