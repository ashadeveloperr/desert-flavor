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
                   $"{facilities}|{string.Join(",", c.Modifiers.Select(m => m.Id))}|{c.Auctions.Phase}";
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
        public void SaveThenReplay_KeepsCommandMinutes()
        {
            // 결정 JQ4: 명령은 시간·분으로 기록되고, 그 분에 착공·완공한 결과가 불러오기 뒤에도 같다(저장도 분까지).
            var campaign = new Campaign(_config, _maps, seed: 13);
            Settlement s = campaign.StartNextSettlement();
            double clock = 0;
            s.TimeSource = () => clock;
            clock = 0.25;
            Assert.IsEmpty(campaign.CurrentMap.RequestSampleLayout(s)); // 0시 15분
            for (int i = 0; i < 9; i++) s.AdvanceHour();
            clock = s.CurrentHour + 40 / 60.0;
            s.BuyStone(5);                                               // 9시 40분(석재가 생겨 착공하는 작업도 그 분)
            for (int i = 0; i < 4; i++) s.AdvanceHour();
            clock = s.CurrentHour + 50 / 60.0;
            s.AdvanceTo(clock);                                          // 13시 50분에 저장
            Assert.AreEqual(50, s.Minute);
            Assert.IsTrue(campaign.CommandLog.Any(c => c.Minute == 15) && campaign.CommandLog.Any(c => c.Minute == 40), "명령에 분이 남는다");

            SaveData save = SaveGame.Capture(campaign, "test");
            SaveGame.ReplayResult restored = SaveGame.Replay(_config, _maps, null, SaveGameJson.Deserialize(SaveGameJson.Serialize(save)));

            CollectionAssert.IsEmpty(restored.Problems);
            Settlement r = restored.Campaign.Current;
            Assert.AreEqual(Signature(campaign), Signature(restored.Campaign));
            Assert.AreEqual(s.Minute, r.Minute, "저장한 분까지 되살림");
            Assert.AreEqual(string.Join(",", s.Jobs.Select(j => $"{j.Id}:{j.State}:{j.StartTime:0.0000}:{j.EndTime:0.0000}")),
                string.Join(",", r.Jobs.Select(j => $"{j.Id}:{j.State}:{j.StartTime:0.0000}:{j.EndTime:0.0000}")), "착공·완공 시각이 같다");
        }

        [Test]
        public void SaveAfterSale_RestoresDestinationStateAndModifiers()
        {
            var campaign = new Campaign(_config, _maps, seed: 5);
            Settlement first = campaign.StartNextSettlement();
            Assert.IsEmpty(campaign.CurrentMap.RequestSampleLayout(first));
            first.BuyStone(5);
            for (int i = 0; i < 1000 && !campaign.Auctions.IsOpen && !campaign.Auctions.IsFinished; i++) first.AdvanceHour();
            campaign.Auctions.Sell(0);

            SaveGame.ReplayResult restored = SaveGame.Replay(_config, _maps, null, SaveGameJson.Deserialize(SaveGameJson.Serialize(SaveGame.Capture(campaign))));
            Assert.AreEqual(CampaignState.ChoosingDestination, restored.Campaign.State);
            Assert.AreEqual(campaign.Cash, restored.Campaign.Cash, 1e-9);
            CollectionAssert.AreEqual(campaign.Modifiers.Select(m => m.Id), restored.Campaign.Modifiers.Select(m => m.Id), "받은 영구 모디파이어도 같다");
        }

        [Test]
        public void OldVersionSave_IsRejectedWithAMessage()
        {
            // 결정 SH1: 장비 명령이 든 예전 저장 파일(v1)은 다시 실행할 수 없다.
            var c = new Campaign(_config, _maps, seed: 1);
            c.StartNextSettlement();
            SaveData old = SaveGame.Capture(c);
            old.Version = 1;
            var e = Assert.Throws<InvalidOperationException>(() => SaveGame.Replay(_config, _maps, null, old));
            StringAssert.Contains("예전", e.Message);
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
