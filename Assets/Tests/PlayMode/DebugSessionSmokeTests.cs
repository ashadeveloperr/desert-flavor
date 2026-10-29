using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;
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
            Assert.IsTrue(session.Clock.IsPaused, "첫 정착지도 일시정지로 시작(CK2)");
            Assert.AreEqual("map_01_oasis_basin", session.Campaign.CurrentMap.Id);
            Transform tiles = session.transform.Find("Tiles");
            Assert.IsNotNull(tiles);
            Assert.AreEqual(session.Map.TileCount, tiles.childCount, "타일마다 육각형 하나");
        }

        [UnityTest]
        public IEnumerator BuildTool_HighlightsExactlyTheBuildableTiles()
        {
            // 2026-10-27 이슈 #7: 시설 건설 도구를 고르면 지을 수 있는 타일만 강조, 선택 도구로 돌아가면 사라짐.
            GameSession session = Build();
            yield return null;
            var input = session.GetComponent<BuildInputController>();
            Transform overlay = session.transform.Find("Buildable Overlay");
            Assert.IsNotNull(overlay);
            Settlement s = session.Settlement;

            input.SelectBuild(BuildingKinds.Farm);
            yield return null;
            Assert.IsTrue(overlay.gameObject.activeSelf);
            int expected = s.Map.Tiles.Count(t => s.CanBuild(BuildingKinds.Farm, t.Coord).Success);
            Assert.Greater(expected, 0);
            Assert.AreEqual(expected, overlay.Cast<Transform>().Count(c => c.gameObject.activeSelf), "농장을 지을 수 있는 타일 수만큼");

            input.SelectBuild(BuildingKinds.Well);
            yield return null;
            Assert.AreEqual(s.Map.Tiles.Count(t => s.CanBuild(BuildingKinds.Well, t.Coord).Success),
                overlay.Cast<Transform>().Count(c => c.gameObject.activeSelf), "도구를 바꾸면 그 시설 기준");

            input.SelectTool(BuildTool.Canal);
            yield return null;
            Assert.IsFalse(overlay.gameObject.activeSelf, "수로 도구에서는 강조 없음");
        }

        [UnityTest]
        public IEnumerator BuildCommand_StartsAndGrowsImmediately_NotAtTheNextHour()
        {
            // 2026-10-27 이슈 #15: 명령 순간 착공(JQ2)한 공사는 화면 진행률도 그때부터 차오르고 완공 때 100%에 닿는다.
            GameSession session = Build();
            yield return null;
            Settlement s = session.Settlement;
            session.Clock.AdvanceGameHours(0.5); // 12:30
            HexCoord tile = s.Map.Tiles.Select(t => t.Coord).First(c => s.CanBuild(BuildingKinds.Farm, c).Success);
            Assert.IsTrue(s.RequestBuild(BuildingKinds.Farm, tile).Success);
            ConstructionJob job = s.Jobs.Single();
            Assert.AreEqual(JobState.InProgress, job.State, "명령 순간 착공");
            Facility farm = s.GetFacility(job.FacilityId);
            yield return null;
            var view = session.GetComponent<SettlementView>();
            Assert.AreEqual(0.5f / job.Hours, view.JobProgress(s, farm), 1e-3f, "12:30 명령 = 그 시간의 절반만큼 진행");

            session.Clock.AdvanceGameHours(0.25);
            Assert.AreEqual(0.75f / job.Hours, view.JobProgress(s, farm), 1e-3f, "다음 정각을 기다리지 않고 차오름");
            session.Clock.AdvanceGameHours(job.Hours - 0.75 - 0.01);
            Assert.AreEqual(FacilityState.Constructing, farm.State);
            Assert.Greater(view.JobProgress(s, farm), 0.99f, "완공 직전 거의 100%");
            session.Clock.AdvanceGameHours(0.02);
            Assert.AreEqual(FacilityState.Operating, farm.State, "공사 시간이 지나면 완공");
        }

        [UnityTest]
        public IEnumerator DesertBackdrop_OutlinesTheMap_AndRisesOutside()
        {
            // 2026-10-27 이슈 #17: 맵 가장자리 변마다 경계선, 맵 아래는 타일보다 낮은 모래, 바깥은 언덕.
            GameSession session = Build();
            yield return null;
            Transform backdrop = session.transform.Find("Desert Backdrop");
            Assert.IsNotNull(backdrop);
            int outerSides = session.Map.Tiles.Sum(t =>
                System.Enum.GetValues(typeof(HexDirection)).Cast<HexDirection>().Count(d => !session.Map.Contains(t.Coord.Neighbor(d))));
            Mesh border = backdrop.Find("Map Border").GetComponent<MeshFilter>().sharedMesh;
            Assert.AreEqual(outerSides * 4, border.triangles.Length / 3, "가장자리 변 하나에 부드러운 띠 하나(삼각형 4개)");

            Mesh dunes = backdrop.Find("Dunes").GetComponent<MeshFilter>().sharedMesh;
            Vector3[] v = dunes.vertices;
            float mapRadius = session.Map.Tiles.Max(t =>
            {
                HexPoint p = session.Layout.TileCenter(t.Coord);
                return Mathf.Sqrt((float)(p.X * p.X + p.Z * p.Z));
            });
            Assert.IsTrue(v.Where(p => new Vector2(p.x, p.z).magnitude < mapRadius).All(p => p.y < 0f), "맵 아래 모래는 타일보다 낮다");
            Assert.Greater(v.Max(p => p.y), session.TileSize * 0.5f, "바깥에는 언덕이 솟는다");
            Assert.IsTrue(RenderSettings.fog, "먼 곳 흐림(안개)");
            Assert.Greater(RenderSettings.fogStartDistance, Vector3.Distance(Camera.main.transform.position, session.transform.position),
                "안개는 맵을 덮지 않는다");
        }

        [UnityTest]
        public IEnumerator Lanterns_GlowAtNight_ExceptClosedFacilities()
        {
            // 2026-10-27 이슈 #13: 등불은 밤에만 켜지고(19~20시 켜짐, 4~5시 꺼짐) 폐쇄된 시설만 꺼진다.
            GameSession session = Build();
            yield return null;
            Settlement s = session.Settlement;
            Assert.IsEmpty(session.RequestSampleLayout());
            session.Clock.AdvanceGameHours(46); // 3일차 10:00
            yield return null;
            Facility farm = s.Facilities.First(f => f.Kind == BuildingKinds.Farm);
            Assert.IsTrue(farm.IsOperating);
            FacilityModel Model(Facility f) => session.transform.Find("Facilities").GetComponentsInChildren<FacilityModel>()
                .First(m => (m.transform.position - session.ToWorld(session.Layout.TileCenter(f.Tile))).sqrMagnitude < 1e-4f);
            Assert.IsFalse(Model(farm).LanternLightOn, "낮에는 꺼짐");

            session.Clock.AdvanceGameHours(12); // 22:00
            yield return null;
            Assert.IsTrue(Model(farm).LanternLightOn, "밤에 운영 중이면 켜짐");
            Assert.IsTrue(s.SetClosed(farm.Id, true).Success);
            yield return null;
            Assert.IsFalse(Model(farm).LanternLightOn, "폐쇄하면 꺼짐");
            Assert.AreEqual(1f, DayNightLighting.LanternFactor(22, 5, 20, 1f));
            Assert.AreEqual(0.5f, DayNightLighting.LanternFactor(19.5, 5, 20, 1f), 1e-4f, "19~20시 서서히 켜짐");
            Assert.AreEqual(0.5f, DayNightLighting.LanternFactor(4.5, 5, 20, 1f), 1e-4f, "남기는 시간 0이면 4~5시 서서히 꺼짐");
            Assert.AreEqual(0f, DayNightLighting.LanternFactor(12, 5, 20, 1f));
            // 2026-10-27 이슈 #16: 낮 시작 뒤 1시간 더 → 5~6시 서서히 꺼짐.
            Assert.AreEqual(1f, DayNightLighting.LanternFactor(4.5, 5, 20, 1f, 1f), "5시 전에는 다 켜짐");
            Assert.AreEqual(0.5f, DayNightLighting.LanternFactor(5.5, 5, 20, 1f, 1f), 1e-4f, "5~6시 서서히 꺼짐");
            Assert.AreEqual(0f, DayNightLighting.LanternFactor(6.01, 5, 20, 1f, 1f), "6시에 다 꺼짐");
            Assert.AreEqual(0.5f, DayNightLighting.LanternFactor(19.5, 5, 20, 1f, 1f), 1e-4f, "켜지는 시각은 그대로");
        }

        [Test]
        public void LanternBreath_DiffersPerFacility_AndStaysInRange()
        {
            // 2026-10-27 이슈 #16: 시설마다 5~8초 중 다른 주기·시작점으로 70~100%, 그 위에 ±3% 잔떨림.
            float Breath(int id, double t, float flicker = 0f) => SettlementView.LanternBreath(id, t, 5f, 8f, 0.7f, flicker);
            float lo = 1f, hi = 0f;
            for (double t = 0; t < 60; t += 0.05)
            {
                float b = Breath(1, t, 0.03f);
                lo = Mathf.Min(lo, b);
                hi = Mathf.Max(hi, b);
            }
            Assert.GreaterOrEqual(lo, 0.7f * 0.97f - 1e-4f);
            Assert.LessOrEqual(hi, 1.03f + 1e-4f);
            Assert.Less(lo, 0.75f, "충분히 어두워지고");
            Assert.Greater(hi, 0.97f, "충분히 밝아진다");

            // 잔떨림을 빼면 주기 안에 같은 값으로 돌아온다: 주기를 찾아 5~8초인지, 시설마다 다른지 본다.
            float Period(int id)
            {
                float b0 = Breath(id, 0);
                for (double t = 4.9; t < 8.2; t += 0.001)
                {
                    float d = Mathf.Abs(Breath(id, t) - b0) + Mathf.Abs(Breath(id, t + 0.3) - Breath(id, 0.3));
                    if (d < 1e-3f) return (float)t;
                }
                return -1f;
            }
            float[] periods = Enumerable.Range(1, 6).Select(Period).ToArray();
            foreach (float p in periods) Assert.That(p, Is.InRange(5f, 8.01f), "주기 5~8초");
            Assert.Greater(periods.Distinct().Count(), 3, "시설마다 다른 주기");
            Assert.AreNotEqual(Breath(1, 0), Breath(2, 0), "시작점도 다름");
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
            Assert.AreEqual(21, s.AuctionHallCompletedHour, "건설 시간 ×0.5(2026-10-23 FL2, 원본 37h) + 명령 순간 착공(2026-10-26 JQ2, 이전 22h)");
            Assert.AreEqual(8, s.Facilities.Count(f => f.IsOperating), "우물·농장·채석장·광산·경매장 + 수로 3");

            yield return null;
            yield return null;
            Assert.AreEqual(s.Facilities.Count, session.transform.Find("Facilities").childCount, "시설마다 표시 오브젝트 하나");
            foreach (Transform shown in session.transform.Find("Facilities"))
            {
                if (shown.name.StartsWith("Canal")) continue;
                FacilityModel model = shown.GetComponent<FacilityModel>();
                Assert.IsNotNull(model, shown.name);
                Assert.GreaterOrEqual(model.PartCount, 3, $"{shown.name}: 도형을 조합한 모양(M1)");
                Assert.Less(model.VisiblePartCount, model.PartCount, $"{shown.name}: 비계·레벨 부품은 L1 운영 중에 숨는다(SG1·SG2)");
            }
            int building = s.Facilities.Count(f => !f.Edge.HasValue && (f.State == FacilityState.Constructing || f.State == FacilityState.Upgrading));
            Assert.AreEqual(building, session.transform.Find("Facilities").GetComponentsInChildren<FacilityModel>().Count(m => m.ScaffoldVisible),
                "비계는 공사·개량 중인 시설에만(SG1)");
            SettlementView view = session.GetComponent<SettlementView>();
            var terrainColors = System.Enum.GetValues(typeof(TerrainType)).Cast<TerrainType>().Select(t => (Color32)view.TerrainColor(t)).ToList();
            Assert.IsTrue(session.transform.Find("Tiles").GetComponentsInChildren<MeshRenderer>()
                    .Any(r => !terrainColors.Contains((Color32)r.sharedMaterial.color)),
                "물이 흐르는 수로 옆 사막이 푸르게 바뀐다(SG4): 지형 색이 아닌 타일이 있어야 한다");

            FastForwardToAuction(session);
            Assert.IsTrue(session.Auctions.IsOpen, "69h 정기 경매(경매장 21h + 48h, 2026-10-26 AU1)");
            Assert.AreEqual(69, s.CurrentHour);
            session.Clock.AdvanceGameHours(10);
            Assert.AreEqual(69, s.CurrentHour, "경매 중에는 정산하지 않는다");

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
                    session.Clock.SelectSpeed(2); // 4배속이었어도
                    session.GoToNextSettlement();
                    yield return null;
                    yield return null;
                    Assert.AreEqual("map_02_sand_canyon", session.Campaign.CurrentMap.Id);
                    Assert.AreEqual(1.1, session.Settlement.Rules.WellSupplyMultiplier, 1e-9);
                    Assert.AreEqual(session.Map.TileCount, session.transform.Find("Tiles").childCount, "새 맵으로 다시 그림");
                    Assert.AreEqual(0, session.transform.Find("Facilities").childCount, "이전 정착지 시설 표시 제거");
                    Assert.IsTrue(session.Clock.IsPaused, "새 정착지는 일시정지로 시작(CK2)");
                    Assert.AreEqual(0, session.Clock.SpeedIndex, "새 정착지는 1배속(CK2)");
                    Assert.AreEqual(12, (int)session.Clock.HourOfDay, "새 정착지도 정오에 시작");
                }
            }

            Assert.AreEqual(2, session.Campaign.Sales.Count, "MVP 목표: 두 정착지 연속 매각");
            Assert.Greater(session.Campaign.Cash, 1000);
        }

        [UnityTest]
        public IEnumerator NewUi_ShowsBars_AndOpensAuctionWindow()
        {
            // 정식 UI(결정 UI1~UI5).
            GameSession session = Build();
            yield return new WaitForSecondsRealtime(0.3f);
            Transform ui = session.transform.Find("Game UI");
            Assert.IsNotNull(ui, "정식 UI 캔버스");
            Assert.IsTrue(ui.gameObject.activeSelf);
            Assert.IsNotNull(ui.Find("Top Bar"));
            Assert.IsNotNull(ui.Find("Build Bar"));
            Assert.IsFalse(ui.Find("Modal Shade").gameObject.activeSelf, "경매 전에는 가운데 창 없음");
            Assert.IsNotNull(ui.Find("Tooltip"), "새 툴팁(UI8)");

            session.RequestSampleLayout();
            FastForwardToAuction(session);
            Assert.IsTrue(session.Auctions.IsOpen);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.IsTrue(ui.Find("Modal Shade").gameObject.activeSelf, "경매가 열리면 가운데 창");
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
