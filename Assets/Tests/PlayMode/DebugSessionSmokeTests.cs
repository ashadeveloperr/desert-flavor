using System.Collections;
using System.IO;
using System.Linq;
using DesertSettlement.Core.Economy;
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

        private static TextAsset LoadConfig() =>
            new TextAsset(File.ReadAllText(Path.Combine(Application.dataPath, "Data", "Config", "economy_balance_v0.8_config.json")));

        [TearDown]
        public void TearDown()
        {
            if (_result == null) return;
            Object.Destroy(_result.Session.gameObject);
            Object.Destroy(_result.CameraRig.gameObject);
            Object.Destroy(_result.VirtualCamera);
            _result = null;
        }

        [UnityTest]
        public IEnumerator Session_StartsAtNoon_AndDrawsTheMap()
        {
            _result = DebugSessionBuilder.Build(LoadConfig());
            yield return null;
            yield return null;

            GameSession session = _result.Session;
            Assert.IsTrue(session.IsReady);
            Assert.AreEqual(12, (int)session.Clock.HourOfDay);
            Transform tiles = session.transform.Find("Tiles");
            Assert.IsNotNull(tiles);
            Assert.AreEqual(session.Map.TileCount, tiles.childCount, "타일마다 육각형 하나");
        }

        [UnityTest]
        public IEnumerator SampleLayout_RunsThroughClock_AndViewFollowsFacilities()
        {
            _result = DebugSessionBuilder.Build(LoadConfig());
            yield return null;
            GameSession session = _result.Session;
            Settlement s = session.Settlement;

            Assert.IsEmpty(session.RequestSampleLayout(), "샘플 배치가 모두 예약돼야 한다");

            // 시계 정각 이벤트가 경제 정산을 구동한다.
            session.Clock.AdvanceGameHours(140);
            Assert.AreEqual(140, s.CurrentHour);
            Assert.AreEqual(37, s.AuctionHallCompletedHour, "v0.8 회귀: 경매장 37h 완공");
            Assert.AreEqual(8, s.Facilities.Count(f => f.IsOperating), "우물·농장·채석장·광산·경매장 + 수로 3");

            yield return null;
            yield return null;
            Transform facilities = session.transform.Find("Facilities");
            Assert.AreEqual(s.Facilities.Count, facilities.childCount, "시설마다 표시 오브젝트 하나");

            // 몇 프레임 실시간으로 돌려도 에러가 없어야 한다(입력·카메라·HUD·조명).
            session.Clock.SetSpeedIndex(3);
            for (int i = 0; i < 30; i++) yield return null;
            Assert.GreaterOrEqual(s.CurrentHour, 140);
        }

        [UnityTest]
        public IEnumerator DemolishingAFacility_RemovesItsViewObject()
        {
            _result = DebugSessionBuilder.Build(LoadConfig());
            yield return null;
            GameSession session = _result.Session;
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
