using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Runtime;
using DesertSettlement.Runtime.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DesertSettlement.PlayMode.Tests
{
    /// <summary>
    /// 화면 캡처(2026-10-23 전체 재검토): 게임을 단계별로 진행하며 카메라 화면 + 정식 UI를 PNG로 저장한다.
    /// 에디터를 열지 않고 결과물 모양을 확인하는 용도라 평소 테스트에서는 돌지 않는다([Explicit]).
    /// 실행: <c>bash Tools/unity-tests.sh PlayMode ScreenCaptureTests</c> → <c>.unity-testcopy/Captures/*.png</c>
    /// (캡처 동안만 UI 캔버스를 카메라 모드로 바꾼다. 시설 머리 위 글자(IMGUI)는 찍히지 않는다.)
    /// </summary>
    [Explicit("화면 캡처 전용: 이름을 지정해 실행")]
    public class ScreenCaptureTests
    {
        private const int Width = 1920, Height = 1080;
        private const float ReferenceWidth = 1920, ReferenceHeight = 1080;
        private DebugSessionBuilder.Result _result;
        private Light _sun;

        private static string DataPath => Path.Combine(Application.dataPath, "Data");
        private static string OutDir => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Captures");

        [TearDown]
        public void TearDown()
        {
            if (_sun != null) Object.Destroy(_sun.gameObject);
            if (_result == null) return;
            Object.Destroy(_result.Session.gameObject);
            Object.Destroy(_result.CameraRig.gameObject);
            Object.Destroy(_result.VirtualCamera);
            _result = null;
        }

        private IEnumerator Capture(string name, int width = Width, int height = Height)
        {
            yield return null;
            yield return null;
            Camera cam = _result.MainCamera != null ? _result.MainCamera : Camera.main;
            Canvas canvas = _result.Session.transform.Find("Game UI")?.GetComponent<Canvas>();
            CanvasScaler scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
            var rt = new RenderTexture(width, height, 24);
            RenderMode mode = canvas != null ? canvas.renderMode : RenderMode.ScreenSpaceOverlay;
            CanvasScaler.ScaleMode scaleMode = scaler != null ? scaler.uiScaleMode : CanvasScaler.ScaleMode.ConstantPixelSize;
            cam.targetTexture = rt;
            if (canvas != null)
            {
                // 배치 모드의 가상 화면 크기로 배율을 잡으면 글자가 작게 그려진 뒤 늘어나 번진다 → 캡처 해상도로 실제 게임과 같은 배율을 계산해 고정한다
                // (ScaleWithScreenSize, 폭·높이 0.5 로그 평균. 1920×1080이면 1, 16:10 1920×1200이면 약 1.054).
                if (scaler != null) { scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; scaler.scaleFactor = ScaleFactor(width, height); }
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = cam.nearClipPlane + 0.5f;
            }
            yield return null; // 캔버스 크기·레이아웃을 새 해상도로
            GameUi ui = _result.Session.GetComponent<GameUi>();
            if (ui != null) ui.RefreshNow(); // 새 캔버스 폭으로 좁은 화면 축소(LY3)를 다시 계산
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory(OutDir);
            File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), tex.EncodeToPNG());

            cam.targetTexture = null;
            if (canvas != null) canvas.renderMode = mode;
            if (scaler != null) scaler.uiScaleMode = scaleMode;
            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(rt);
            Debug.Log("[Capture] " + Path.Combine(OutDir, name + ".png"));
        }

        /// <summary>CanvasScaler ScaleWithScreenSize(기준 1920×1080, 폭·높이 0.5)와 같은 배율.</summary>
        private static float ScaleFactor(int width, int height) =>
            Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(width / ReferenceWidth, 2f), Mathf.Log(height / ReferenceHeight, 2f), 0.5f));

        [UnityTest]
        public IEnumerator CaptureMainScreens()
        {
            HudMode.Legacy = false;
            // 빈 테스트 장면에는 태양이 없다(실제 SampleScene에는 있음). 없으면 주변광만 비쳐 색이 회색으로 찍힌다.
            if (Object.FindObjectsByType<Light>(FindObjectsSortMode.None).All(l => l.type != LightType.Directional))
            {
                _sun = new GameObject("Capture Sun").AddComponent<Light>();
                _sun.type = LightType.Directional;
                _sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
            _result = DebugSessionBuilder.Build(
                new TextAsset(File.ReadAllText(Path.Combine(DataPath, "Config", "economy_balance_v0.8_config.json"))),
                Directory.GetFiles(Path.Combine(DataPath, "Maps"), "*.json").OrderBy(p => p, System.StringComparer.Ordinal)
                    .Select(p => new TextAsset(File.ReadAllText(p))).ToList(),
                new TextAsset(File.ReadAllText(Path.Combine(DataPath, "Config", "procedural_map_config.json"))));
            GameSession session = _result.Session;
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Capture("01_start_paused");
            // 건설 가능 타일 강조(2026-10-27 이슈 #7): 농장 도구.
            var input = session.GetComponent<BuildInputController>();
            input.SelectBuild(BuildingKinds.Farm);
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Capture("01b_build_farm_overlay");
            input.SelectTool(BuildTool.Select);

            Settlement s = session.Settlement;
            session.RequestSampleLayout();
            session.Clock.AdvanceGameHours(5);
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Capture("02_construction_scaffolds");

            session.Clock.AdvanceGameHours(40);
            Facility farm = s.Facilities.FirstOrDefault(f => f.Kind == BuildingKinds.Farm);
            if (farm != null) _result.Session.GetComponent<BuildInputController>().SelectedFacilityId = farm.Id;
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Capture("03_operating_inspector");
            // 화면 비율별 배치(2026-10-27 이슈 #5): 16:10은 그대로, 5:4는 위쪽 띠·도구 막대가 통째로 줄어든다(LY3).
            yield return Capture("03b_16x10", 1920, 1200);
            yield return Capture("03c_5x4", 1280, 1024);
            // 밤(2026-10-27 이슈 #13): 달빛·환경광 최소값·시설 등불. 첫 경매(69h) 전인 58h 22:00.
            session.Clock.AdvanceGameHours((22 - session.Clock.HourOfDay + 24) % 24);
            _result.Session.GetComponent<BuildInputController>().SelectedFacilityId = -1;
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Capture("03d_night_lanterns");

            // 개량해 레벨 부품 보기(석재가 모자라면 사서 채운다).
            foreach (Facility f in s.Facilities.Where(f => f.Kind == BuildingKinds.Farm || f.Kind == BuildingKinds.Well).ToList())
            {
                if (s.Cash < 250) break; // 자금이 바닥나면 긴급 매각이 열린다
                if (s.Stone < 30) s.BuyStone(10);
                s.RequestUpgrade(f.Id);
            }
            session.Clock.AdvanceGameHours(30);
            _result.Session.GetComponent<BuildInputController>().SelectedFacilityId = -1;
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Capture("04_upgraded_greening");

            for (int i = 0; i < 1000 && session.AcceptsWorldInput; i++) session.Clock.AdvanceGameHours(1);
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Capture("05_auction_modal");

            var bids = session.Auctions.CurrentAuction?.Bids;
            if (bids != null && bids.Count > 0)
            {
                session.SellTo(bids.Select((b, i) => (b, i)).OrderByDescending(x => x.b.Cash).First().i);
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Capture("06_destination");
                // 행선지 카드를 눌러 상점으로(2026-10-27 이슈 #6).
                Button pick = _result.Session.GetComponentsInChildren<Button>().FirstOrDefault(b => b.GetComponentInChildren<Text>()?.text == "이 지역으로");
                Assert.IsNotNull(pick, "행선지 창에 후보 카드가 있어야 함");
                pick.onClick.Invoke();
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Capture("06b_shop");
                session.GoToNextSettlement();
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Capture("07_next_settlement_paused");
            }
            Assert.IsTrue(File.Exists(Path.Combine(OutDir, "01_start_paused.png")));
        }
    }
}
