using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 플레이 가능한 디버그 세션 한 벌을 코드로 만든다. 에디터 메뉴(디버그 세션 만들기)와 PlayMode 테스트가 함께 쓴다.
    /// 만드는 것: GameSession(+ 정착지 뷰·Gizmo 뷰·입력·HUD·시계 HUD·낮밤 조명), 카메라 리그 + Cinemachine 카메라,
    /// Main Camera의 CinemachineBrain. 이미 있는 Main Camera·Directional Light는 재사용한다.
    /// </summary>
    public static class DebugSessionBuilder
    {
        public sealed class Result
        {
            public GameSession Session;
            public StrategyCameraRig CameraRig;
            /// <summary>Cinemachine 카메라 오브젝트(다른 어셈블리가 Cinemachine을 참조하지 않아도 되도록 GameObject로 둔다).</summary>
            public GameObject VirtualCamera;
            public Camera MainCamera;
        }

        /// <param name="proceduralMapConfig">절차 맵 설정(null이면 고정 맵만 순환).</param>
        public static Result Build(TextAsset economyConfig, System.Collections.Generic.IEnumerable<TextAsset> maps, TextAsset proceduralMapConfig = null)
        {
            var sessionObject = new GameObject("GameSession");
            sessionObject.SetActive(false); // 설정을 넣은 뒤 Awake가 돌도록
            var session = sessionObject.AddComponent<GameSession>();
            session.SetConfig(economyConfig);
            if (maps != null) session.SetMaps(maps);
            session.SetProceduralMapConfig(proceduralMapConfig);
            EnsureComponents(session);

            Camera main = Camera.main;
            if (main == null)
            {
                var camObject = new GameObject("Main Camera") { tag = "MainCamera" };
                main = camObject.AddComponent<Camera>();
                camObject.AddComponent<AudioListener>();
            }
            if (main.GetComponent<CinemachineBrain>() == null) main.gameObject.AddComponent<CinemachineBrain>();

            var rigObject = new GameObject("CameraRig");
            var rig = rigObject.AddComponent<StrategyCameraRig>();

            var vcamObject = new GameObject("CM TopDown");
            var vcam = vcamObject.AddComponent<CinemachineCamera>();
            vcam.Follow = rigObject.transform;
            vcam.LookAt = rigObject.transform;
            vcam.Lens.FieldOfView = 40f;
            var follow = vcamObject.AddComponent<CinemachineFollow>();
            TrackerSettings tracker = follow.TrackerSettings;
            tracker.PositionDamping = Vector3.zero; // 감쇠가 있으면 이동 중 시선이 흔들린다(StrategyCameraRig 참고)
            follow.TrackerSettings = tracker;
            follow.FollowOffset = new Vector3(0f, 8.4f, -7f);
            vcamObject.AddComponent<CinemachineHardLookAt>();
            rig.Bind(session, vcam);

            sessionObject.SetActive(true);
            return new Result { Session = session, CameraRig = rig, VirtualCamera = vcamObject, MainCamera = main };
        }

        /// <summary>
        /// 세션 오브젝트에 필요한 컴포넌트 중 빠진 것을 붙인다. 예전에 만든 씬에 새 컴포넌트를 추가할 때도 쓴다.
        /// 붙인 컴포넌트 수를 돌려준다.
        /// </summary>
        public static int EnsureComponents(GameSession session)
        {
            GameObject go = session.gameObject;
            int added = 0;
            T Ensure<T>() where T : Component
            {
                T c = go.GetComponent<T>();
                if (c != null) return c;
                added++;
                return go.AddComponent<T>();
            }

            Ensure<SettlementView>();
            Ensure<HexMapDebugView>();
            Ensure<BuildInputController>();
            Ensure<SettlementHud>();
            Ensure<HoverTooltip>();
            Ensure<AuctionPanel>();
            Ensure<GameClockHud>();
            Ensure<WaterPathOverlay>();
            Ensure<UI.GameUi>();
            if (go.GetComponent<DayNightLighting>() == null)
            {
                Ensure<DayNightLighting>().Bind(session, FindSun());
            }
            return added;
        }

        private static Light FindSun()
        {
            if (RenderSettings.sun != null) return RenderSettings.sun;
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional) return light;
            }
            Debug.LogWarning("[DebugSessionBuilder] Directional Light가 없어 낮/밤 조명이 동작하지 않습니다.");
            return null;
        }
    }
}
