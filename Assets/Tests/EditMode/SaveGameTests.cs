using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Progression;
using NUnit.Framework;
using UnityEngine;

namespace DesertSettlement.Core.Tests
{
    /// <summary>저장·불러오기(2026-10-23 로드맵 2-①). 명령 기록을 같은 시드로 다시 실행하면 같은 상태가 된다. Docs/decision_log_auto.md S1~S5.</summary>
    public class SaveGameTests
    {
        private static EconomyConfig _config;
        private static List<MapDefinition> _maps;

        [OneTimeSetUp]
        public void Load()
        {
            _config = TestData.Economy();
            _maps = TestData.Maps();
        }

        private static string Signature(Campaign c)
        {
            Settlement s = c.Current;
            string facilities = string.Join(";", s.Facilities.OrderBy(f => f.Id).Select(f => $"{f.Id}{f.Kind}{f.Level}{f.State}"));
            return $"{c.SettlementNumber}|{c.State}|{s.CurrentHour}|{s.Cash:0.000000}|{s.Food:0.0000}|{s.Stone:0.0000}|{s.FoodCoefficient:0.000000}|" +
                   $"{facilities}|{string.Join(",", c.OwnedEquipment.OrderBy(e => e).Select(e => e + c.EquipmentLevel(e)))}|{c.Auctions.Phase}";
        }

        [Test]
        public void SaveThenReplay_RestoresTheSameCampaign()
        {
            var campaign = new Campaign(_config, _maps, seed: 11);
            Settlement first = campaign.StartNextSettlement();
            Assert.IsEmpty(campaign.CurrentMap.RequestSampleLayout(first));
            first.BuyStone(5);
            for (int i = 0; i < 1000 && !campaign.Auctions.IsOpen && !campaign.Auctions.IsFinished; i++) first.AdvanceHour();
            Assert.IsTrue(campaign.Auctions.IsOpen);
            campaign.Auctions.Hold(); // 한 번 보류
            for (int i = 0; i < 1000 && !campaign.Auctions.IsOpen && !campaign.Auctions.IsFinished; i++) first.AdvanceHour();
            campaign.Auctions.Sell(0);
            Assert.IsTrue(campaign.BuyEquipment("wellPump").Success);

            Settlement second = campaign.StartNextSettlement();
            Assert.IsEmpty(campaign.CurrentMap.RequestSampleLayout(second));
            second.AdvanceHours(20);
            Facility farm = second.Facilities.First(f => f.Kind == BuildingKinds.Farm && f.IsOperating);
            Assert.IsTrue(second.SetClosed(farm.Id, true).Success);
            second.AdvanceHours(7);

            SaveData save = SaveGame.Capture(campaign, "test");
            string json = SaveGameJson.Serialize(save);
            SaveGame.ReplayResult restored = SaveGame.Replay(_config, _maps, null, SaveGameJson.Deserialize(json));

            CollectionAssert.IsEmpty(restored.Problems);
            Assert.AreEqual(Signature(campaign), Signature(restored.Campaign));
            Assert.AreEqual(campaign.CommandLog.Count, restored.Campaign.CommandLog.Count, "복원한 캠페인도 같은 기록을 이어 간다");
        }

        [Test]
        public void SaveInShop_RestoresShoppingState()
        {
            var campaign = new Campaign(_config, _maps, seed: 5);
            Settlement first = campaign.StartNextSettlement();
            Assert.IsEmpty(campaign.CurrentMap.RequestSampleLayout(first));
            first.BuyStone(5);
            for (int i = 0; i < 1000 && !campaign.Auctions.IsOpen && !campaign.Auctions.IsFinished; i++) first.AdvanceHour();
            campaign.Auctions.Sell(0);

            SaveGame.ReplayResult restored = SaveGame.Replay(_config, _maps, null, SaveGameJson.Deserialize(SaveGameJson.Serialize(SaveGame.Capture(campaign))));
            Assert.AreEqual(CampaignState.Shopping, restored.Campaign.State);
            Assert.AreEqual(campaign.Cash, restored.Campaign.Cash, 1e-9);
        }

        [Test]
        public void EveryCommandType_IsHandledByReplay()
        {
            // 결정 MT2: GameCommand에 종류 상수를 더하고 SaveGame.Apply에 case를 빠뜨리면 여기서 걸린다(불러오기에서 상태가 어긋나는 것을 막음).
            // 종류 목록은 반사로 읽으므로 새 상수도 저절로 검사된다. 명령이 실패해도 되고, '알 수 없는 명령'만 아니면 된다.
            List<string> types = typeof(GameCommand).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string)).Select(f => (string)f.GetRawConstantValue()).ToList();
            Assert.Greater(types.Count, 15);

            int settlement = SaveGame.Replay(_config, _maps, null, WithStart()).Campaign.SettlementNumber;
            foreach (string type in types.Append("notACommand"))
            {
                // 정착지 안에서 같은 정착지 번호로 부르므로 Apply의 종류 switch까지 반드시 간다.
                SaveData data = WithStart(new GameCommand { Settlement = settlement, Hour = 0, Type = type, Kind = BuildingKinds.Farm });
                List<string> problems = SaveGame.Replay(_config, _maps, null, data).Problems;
                bool unknown = problems.Any(m => m.Contains("알 수 없는 명령"));
                Assert.AreEqual(type == "notACommand", unknown, $"{type}: {string.Join(" / ", problems)}");
            }
        }

        private static SaveData WithStart(params GameCommand[] then) => new SaveData
        {
            Seed = 1, SettlementNumber = 1,
            Commands = new[] { new GameCommand { Type = GameCommand.Start } }.Concat(then).ToList(),
        };

        [Test]
        public void NewerVersion_IsRejected()
        {
            var data = new SaveData { Version = SaveData.CurrentVersion + 1 };
            Assert.Throws<InvalidOperationException>(() => SaveGame.Replay(_config, _maps, null, data));
        }
    }
}
