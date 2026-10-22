using System;
using System.Collections.Generic;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;
using DesertSettlement.Core.Timing;
using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>인스펙터에서 편집하는 지형 한 칸.</summary>
    [Serializable]
    public struct TerrainEntry
    {
        public int q;
        public int r;
        public TerrainType terrain;

        public TerrainEntry(int q, int r, TerrainType terrain)
        {
            this.q = q;
            this.r = r;
            this.terrain = terrain;
        }
    }

    /// <summary>
    /// 한 정착지의 게임 상태를 만들고 들고 있는 진입점. 씬에 하나만 둔다.
    /// 설정 JSON → 맵(지형 목록 적용) → <see cref="Settlement"/> → <see cref="GameClock"/>을 만들고,
    /// 시계의 정각마다 Settlement.AdvanceHour()로 경제를 정산한다.
    /// 다른 컴포넌트(뷰, 입력, HUD, 조명)는 이 컴포넌트의 속성을 읽고 Settlement의 명령을 호출한다.
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
        [Tooltip("사막이 아닌 타일. 고정 맵 2종(A1)이 정해지기 전까지 쓰는 임시 테스트 지형")]
        [SerializeField] private List<TerrainEntry> terrain = DefaultTestTerrain();

        [Header("시간")]
        [SerializeField] private GameTimeSettings timeSettings = new GameTimeSettings();
        [SerializeField] private bool logClockEvents = true;

        public EconomyConfig Config { get; private set; }
        public HexMap Map { get; private set; }
        public HexLayout Layout { get; private set; }
        public Settlement Settlement { get; private set; }
        public HexTopology Topology => Settlement?.Topology;
        public GameClock Clock { get; private set; }
        public bool IsReady { get; private set; }

        public int MapRadius => mapRadius;
        public float TileSize => tileSize;
        public IReadOnlyList<TerrainEntry> TerrainEntries => terrain;

        /// <summary>Initialize가 성공할 때마다(새 정착지) 발생.</summary>
        public event Action Initialized;

        private void Awake()
        {
            Initialize();
        }

        private void Update()
        {
            if (IsReady) Clock.Advance(Time.unscaledDeltaTime);
        }

        /// <summary>코드에서 세션을 만들 때(테스트·빌더) 설정 파일을 지정한다. Awake 전에 호출해야 한다.</summary>
        public void SetConfig(TextAsset config) => economyConfig = config;

        /// <summary>설정을 읽고 맵·정착지·시계를 새로 만든다. 실패하면 콘솔에 이유를 남기고 IsReady = false.</summary>
        public void Initialize()
        {
            IsReady = false;
            try
            {
                if (economyConfig == null) throw new InvalidOperationException("economyConfig(TextAsset)가 지정되지 않았습니다.");

                Config = EconomyConfigLoader.Parse(economyConfig.text);
                Map = new HexMap(mapRadius);
                foreach (TerrainEntry entry in terrain)
                {
                    if (Map.TryGetTile(new HexCoord(entry.q, entry.r), out HexTile tile)) tile.Terrain = entry.terrain;
                }
                Layout = new HexLayout(tileSize);
                Settlement = new Settlement(Map, SettlementRules.FromConfig(Config));
                Clock = new GameClock(timeSettings, Config.ConfirmedRules.GameDayRealSecondsAt1x);
                Clock.HourPassed += _ => Settlement.AdvanceHour();

                if (logClockEvents)
                {
                    Clock.DayStarted += t => Debug.Log($"[GameClock] {t.Day}일차 시작");
                    Clock.PhaseChanged += t => Debug.Log($"[GameClock] {t} — {(t.Phase == DayPhase.Day ? "낮" : "밤")}이 되었습니다");
                }

                IsReady = true;
                Initialized?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameSession] 초기화 실패: {e.Message}", this);
            }
        }

        /// <summary>Core의 평면 좌표(X, Z)를 이 오브젝트 기준 월드 좌표로 바꾼다.</summary>
        public Vector3 ToWorld(HexPoint point, float height = 0f) =>
            transform.TransformPoint(new Vector3((float)point.X, height, (float)point.Z));

        /// <summary>월드 좌표를 Core 평면 좌표로 바꾼다(높이 무시).</summary>
        public HexPoint ToHexPoint(Vector3 world)
        {
            Vector3 local = transform.InverseTransformPoint(world);
            return new HexPoint(local.x, local.z);
        }

        /// <summary>
        /// 확인용 샘플 배치 예약: 기본 테스트 지형 기준 우물·농장·채석장·광산·경매장 + 수로 3개
        /// (원본 v0.8 직렬 배치와 같은 위치). 석재가 부족하면 대기열에서 차례로 착공된다.
        /// </summary>
        public List<string> RequestSampleLayout()
        {
            var messages = new List<string>();
            void Try(string what, CommandResult r) { if (!r.Success) messages.Add($"{what}: {r.Message}"); }

            Try("우물", Settlement.RequestBuild(BuildingKinds.Well, new HexCoord(0, 0)));
            Try("농장", Settlement.RequestBuild(BuildingKinds.Farm, new HexCoord(1, -1)));
            Try("수로1", Settlement.RequestCanal(EdgeId.Of(new HexCoord(1, -1), HexDirection.SouthEast)));
            Try("수로2", Settlement.RequestCanal(EdgeId.Of(new HexCoord(1, 0), HexDirection.NorthEast)));
            Try("채석장", Settlement.RequestBuild(BuildingKinds.Quarry, new HexCoord(2, -1)));
            Try("광산", Settlement.RequestBuild(BuildingKinds.Mine, new HexCoord(2, 0)));
            Try("수로3", Settlement.RequestCanal(EdgeId.Of(new HexCoord(2, -1), HexDirection.SouthEast)));
            Try("경매장", Settlement.RequestBuild(BuildingKinds.AuctionHall, new HexCoord(-1, 0)));
            foreach (string m in messages) Debug.LogWarning("[GameSession] 샘플 배치 " + m);
            return messages;
        }

        /// <summary>임시 테스트 지형: 수원 2·광맥 2·암반 2·장애물 3. 샘플 배치와 맞물린다.</summary>
        public static List<TerrainEntry> DefaultTestTerrain() => new List<TerrainEntry>
        {
            new TerrainEntry(0, 0, TerrainType.WaterSource),
            new TerrainEntry(-2, 2, TerrainType.WaterSource),
            new TerrainEntry(2, 0, TerrainType.OreVein),
            new TerrainEntry(-1, 3, TerrainType.OreVein),
            new TerrainEntry(2, -1, TerrainType.Bedrock),
            new TerrainEntry(-2, 0, TerrainType.Bedrock),
            new TerrainEntry(0, -3, TerrainType.Obstacle),
            new TerrainEntry(3, -2, TerrainType.Obstacle),
            new TerrainEntry(-3, 1, TerrainType.Obstacle),
        };
    }
}
