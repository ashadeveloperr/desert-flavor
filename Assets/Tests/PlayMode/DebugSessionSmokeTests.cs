using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Progression;
using DesertSettlement.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DesertSettlement.PlayMode.Tests
{
    /// <summary>
    /// 실제 플레이 모드에서 디버그 세션 한 벌이 에러 없이 돌아가는지 확인하는 스모크 테스트.
    /// (콘솔에 에러 로그가 찍히면 Unity Test Framework가 테스트를 실패시킨다.)
    /// </summary>
    public class DebugSessionSmokeTests
    {
        private DebugSessionBuilder.Result _result;

        private static string DataPath => Path.Combine(Application.dataPath, "Data");

        private static TextAsset LoadConfig() =>
            new TextAsset(File.ReadAllText(Path.Combine(DataPath, "Config", "economy_balance_v0.8_config.json")));

        private static List<TextAsset> LoadMaps() =>
            Directory.GetFiles(Path.Combine(DataPath, "Maps"), "*.json").OrderBy(p => p, System.StringComparer.Ordinal)
                .Select(p => new TextAsset(File.ReadAllText(p))).ToList();

        private GameSession Build()
        {
            _result = DebugSessionBuilder.Build(LoadConfig(), LoadMaps());
            return _result.Session;
        }

        [TearDown]
        public void TearDown()
        {
            if (_result == null) return;
            Object.Destroy(_result.Session.gameObject);
            Object.Destroy(_result.CameraRig.gameObject);
            Object.Destroy(_result.VirtualCamera);
            _result = null;
        }

        /// <summary>경매가 열리거나 정착지가 끝날 때까지 시계를 빨리 돌린다.</summary>
        private static void FastForwardToAuction(GameSession session)
        {
            for (int i = 0; i < 1000 && session.AcceptsWorldInput; i++) session.Clock.AdvanceGameHours(1);
        }

        [UnityTest]
        public IEnumerator Session_StartsAtNoon_AndDrawsTheMap()
        {
            GameSession session = Build();
            yield return null;
            yield return null;

            Assert.IsTrue(session.IsReady);
            Assert.AreEqual(12, (int)session.Clock.HourOfDay);
            Assert.AreEqual("map_01_oasis_basin", session.Campaign.CurrentMap.Id);
            Transform tiles = session.transform.Find("Tiles");
            Assert.IsNotNull(tiles);
            Assert.AreEqual(session.Map.TileCount, tiles.childCount, "타일마다 육각형 하나");
        }

        [UnityTest]
        public IEnumerator SampleLayout_RunsThroughClock_UntilAuctionPausesTheGame()
        {
            GameSession session = Build();
            yield return null;
            Settlement s = session.Settlement;

            Assert.IsEmpty(session.RequestSampleLayout(), "샘플 배치가 모두 예약돼야 한다");
            session.Clock.AdvanceGameHours(60);
            Assert.AreEqual(60, s.CurrentHour);
            Assert.AreEqual(37, s.AuctionHallCompletedHour);
            Assert.AreEqual(8, s.Facilities.Count(f => f.IsOperating), "우물·농장·채석장·광산·경매장 + 수로 3");

            yield return null;
            yield return null;
            Assert.AreEqual(s.Facilities.Count, session.transform.Find("Facilities").childCount, "시설마다 표시 오브젝트 하나");

            FastForwardToAuction(session);
            Assert.IsTrue(session.Auctions.IsOpen, "133h 정기 경매");
            Assert.AreEqual(133, s.CurrentHour);
            session.Clock.AdvanceGameHours(10);
            Assert.AreEqual(133, s.CurrentHour, "경매 중에는 정산하지 않는다");

            // 몇 프레임 실시간으로 돌려도 에러가 없어야 한다(경매 패널·HUD·입력·카메라·조명).
            for (int i = 0; i < 10; i++) yield return null;
            Assert.IsTrue(session.Clock.IsPaused, "경매 중 자동 일시정지");
        }

        [UnityTest]
        public IEnumerator TwoSettlements_SoldInARow_FullLoop()
        {
            GameSession session = Build();
            yield return null;

            for (int n = 1; n <= 2; n++)
            {
                Assert.AreEqual(n, session.Campaign.SettlementNumber);
                Assert.IsEmpty(session.RequestSampleLayout(), session.Campaign.CurrentMap.Id);
                FastForwardToAuction(session);
                Assert.IsTrue(session.Auctions.IsOpen, $"정착지 {n} 경매");
                var bids = session.Auctions.CurrentAuction.Bids;
                int best = bids.Select((b, i) => (b, i)).OrderByDescending(x => x.b.Cash).First().i;
                session.SellTo(best);
                Assert.AreEqual(CampaignState.Shopping, session.Campaign.State);
                yield return null;

                if (n == 1)
                {
                    Assert.IsTrue(session.BuyEquipment("wellPump").Success);
                    session.GoToNextSettlement();
                    yield return null;
                    yield return null;
                    Assert.AreEqual("map_02_sand_canyon", session.Campaign.CurrentMap.Id);
                    Assert.AreEqual(1.2, session.Settlement.Rules.WellSupplyMultiplier, 1e-9);
                    Assert.AreEqual(session.Map.TileCount, session.transform.Find("Tiles").childCount, "새 맵으로 다시 그림");
                    Assert.AreEqual(0, session.transform.Find("Facilities").childCount, "이전 정착지 시설 표시 제거");
                    Assert.IsFalse(session.Clock.IsPaused);
                    Assert.AreEqual(12, (int)session.Clock.HourOfDay, "새 정착지도 정오에 시작");
                }
            }

            Assert.AreEqual(2, session.Campaign.Sales.Count, "MVP 목표: 두 정착지 연속 매각");
            Assert.Greater(session.Campaign.Cash, 1000);
        }

        [UnityTest]
        public IEnumerator DemolishingAFacility_RemovesItsViewObject()
        {
            GameSession session = Build();
            yield return null;
            Settlement s = session.Settlement;
            session.RequestSampleLayout();
            session.Clock.AdvanceGameHours(40);

            Facility farm = s.Facilities.First(f => f.Kind == "farm");
            Assert.IsTrue(s.RequestDemolish(farm.Id).Success);
            session.Clock.AdvanceGameHours(5);
            Assert.IsNull(s.GetFacility(farm.Id));

            yield return null;
            Assert.AreEqual(s.Facilities.Count, session.transform.Find("Facilities").childCount);
        }
    }
}
