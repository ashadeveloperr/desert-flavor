using System;
using DesertSettlement.Core.Canal;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Hex;
using DesertSettlement.Core.Timing;
using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 한 정착지의 게임 상태를 만들고 들고 있는 진입점. 씬에 하나만 둔다.
    /// 인스펙터에서 맵 반경·타일 크기·시간 설정을 바꿀 수 있다.
    /// 다른 컴포넌트(디버그 뷰, 조명, HUD)는 이 컴포넌트의 Map/Topology/Canals/Clock을 읽기만 한다.
    /// 메뉴 Tools > Desert Settlement > 디버그 세션 만들기 로 씬에 추가할 수 있다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameSession : MonoBehaviour
    {
        [Header("데이터")]
        [Tooltip("Assets/Data/Config/economy_balance_v0.8_config.json")]
        [SerializeField] private TextAsset economyConfig;

        [Header("맵")]
        [Range(HexMap.MinRadius, HexMap.MaxRadius)]
        [SerializeField] private int mapRadius = 3;
        [Tooltip("타일 중심~꼭짓점 거리(월드 단위)")]
        [Min(0.1f)]
        [SerializeField] private float tileSize = 1f;

        [Header("시간")]
        [SerializeField] private GameTimeSettings timeSettings = new GameTimeSettings();
        [SerializeField] private bool logClockEvents = true;

        [Header("디버그")]
        [Tooltip("시작할 때 확인용 수로를 몇 개 설치한다")]
        [SerializeField] private bool installSampleCanals = true;

        public EconomyConfig Config { get; private set; }
        public HexMap Map { get; private set; }
        public HexTopology Topology { get; private set; }
        public HexLayout Layout { get; private set; }
        public CanalGraph Canals { get; private set; }
        public GameClock Clock { get; private set; }
        public bool IsReady { get; private set; }

        public int MapRadius => mapRadius;
        public float TileSize => tileSize;

        private void Awake()
        {
            Initialize();
        }

        private void Update()
        {
            if (IsReady) Clock.Advance(Time.unscaledDeltaTime);
        }

        /// <summary>설정을 읽고 맵·수로망·시계를 새로 만든다. 실패하면 콘솔에 이유를 남기고 IsReady = false.</summary>
        public void Initialize()
        {
            IsReady = false;
            try
            {
                if (economyConfig == null) throw new InvalidOperationException("economyConfig(TextAsset)가 지정되지 않았습니다.");

                Config = EconomyConfigLoader.Parse(economyConfig.text);
                Map = new HexMap(mapRadius);
                Topology = new HexTopology(Map);
                Layout = new HexLayout(tileSize);
                Canals = new CanalGraph(Topology);
                Clock = new GameClock(timeSettings, Config.ConfirmedRules.GameDayRealSecondsAt1x);

                if (logClockEvents)
                {
                    Clock.DayStarted += t => Debug.Log($"[GameClock] {t.Day}일차 시작");
                    Clock.PhaseChanged += t => Debug.Log($"[GameClock] {t} — {(t.Phase == DayPhase.Day ? "낮" : "밤")}이 되었습니다");
                }

                if (installSampleCanals) InstallSampleCanals();
                IsReady = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameSession] 초기화 실패: {e.Message}", this);
            }
        }

        /// <summary>Core의 평면 좌표(X, Z)를 이 오브젝트 기준 월드 좌표로 바꾼다.</summary>
        public Vector3 ToWorld(HexPoint point) => transform.TransformPoint(new Vector3((float)point.X, 0f, (float)point.Z));

        // 중앙 타일에서 동쪽 맵 경계까지 타일 윗변을 따라가는 지그재그 수로 + 중앙 타일 동쪽 변 분기.
        // 동쪽 첫 타일의 수로 하나에는 닫힌 수문을 달고, 맨 끝은 경계 변까지 잇는다.
        private void InstallSampleCanals()
        {
            HexCoord tile = HexCoord.Zero;
            Canals.TryInstall(EdgeId.Of(tile, HexDirection.East));
            for (int i = 0; i <= mapRadius; i++)
            {
                Canals.TryInstall(EdgeId.Of(tile, HexDirection.NorthWest));
                Canals.TryInstall(EdgeId.Of(tile, HexDirection.NorthEast));
                if (i < mapRadius) tile = tile.Neighbor(HexDirection.East);
            }
            Canals.TryInstall(EdgeId.Of(tile, HexDirection.East)); // 경계 변

            EdgeId gated = EdgeId.Of(HexCoord.Zero.Neighbor(HexDirection.East), HexDirection.NorthEast);
            Canals.TrySetGate(gated, true);
            Canals.TrySetGateOpen(gated, false);
        }
    }
}
