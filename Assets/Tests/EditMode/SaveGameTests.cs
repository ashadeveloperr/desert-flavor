using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            string data = Path.Combine(Application.dataPath, "Data");
            _config = EconomyConfigLoader.Parse(File.ReadAllText(Path.Combine(data, "Config", "economy_balance_v0.8_config.json")));
            _maps = Directory.GetFiles(Path.Combine(data, "Maps"), "*.json").OrderBy(p => p, StringComparer.Ordinal)
                .Select(p => MapLoader.Parse(File.ReadAllText(p))).ToList();
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
        public void NewerVersion_IsRejected()
        {
            var data = new SaveData { Version = SaveData.CurrentVersion + 1 };
            Assert.Throws<InvalidOperationException>(() => SaveGame.Replay(_config, _maps, null, data));
        }
    }
}
