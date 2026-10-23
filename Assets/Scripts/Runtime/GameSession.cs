using System;
using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Auction;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;
using DesertSettlement.Core.Progression;
using DesertSettlement.Core.Timing;
using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>인스펙터에서 편집하는 지형 한 칸(맵 파일이 없을 때의 대체 지형).</summary>
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
    /// 게임 진입점. 설정 JSON과 맵 파일로 <see cref="Campaign"/>을 만들고, 지금 정착지의 <see cref="Settlement"/>와
    /// <see cref="AuctionManager"/>를 시계에 연결한다(정각마다 AdvanceHour).
    /// - 경매가 열리면 시계를 멈춘다(경매 중 자동 일시정지). 매각하면 상점, 다음 정착지로 가면 맵·시계를 새로 시작한다.
    /// - 다른 컴포넌트(뷰, 입력, HUD, 경매 패널)는 이 컴포넌트의 속성을 읽고 명령 메서드를 호출한다.
    /// 메뉴 Tools > Desert Settlement > 디버그 세션 만들기 로 씬에 추가할 수 있다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameSession : MonoBehaviour
    {
        [Header("데이터")]
        [Tooltip("Assets/Data/Config/economy_balance_v0.8_config.json")]
        [SerializeField] private TextAsset economyConfig;
        [Tooltip("Assets/Data/Maps/*.json. 정착지마다 순서대로 사용. 비어 있으면 아래 대체 지형 한 장")]
        [SerializeField] private List<TextAsset> maps = new List<TextAsset>();
        [Tooltip("Assets/Data/Config/procedural_map_config.json. 지정하면 첫 정착지 뒤로는 보유 자금 비례 난이도의 생성 맵. 비우면 위 맵 파일만 순환")]
        [SerializeField] private TextAsset proceduralMapConfig;
        [Tooltip("경매 입찰자·시세 무작위 시드(같으면 같은 결과)")]
        [SerializeField] private int campaignSeed = 20261022;

        [Header("맵 파일이 없을 때")]
        [Range(HexMap.MinRadius, HexMap.MaxRadius)]
        [SerializeField] private int mapRadius = 3;
        [SerializeField] private List<TerrainEntry> terrain = DefaultTestTerrain();

        [Header("표시")]
        [Tooltip("타일 중심~꼭짓점 거리(월드 단위)")]
        [Min(0.1f)]
        [SerializeField] private float tileSize = 1f;

        [Header("시간")]
        [SerializeField] private GameTimeSettings timeSettings = new GameTimeSettings();
        [SerializeField] private bool logClockEvents = true;

        public EconomyConfig Config { get; private set; }
        public Campaign Campaign { get; private set; }
        public Settlement Settlement => Campaign?.Current;
        public AuctionManager Auctions => Campaign?.Auctions;
        public HexMap Map => Settlement?.Map;
        public HexTopology Topology => Settlement?.Topology;
        public HexLayout Layout { get; private set; }
        public GameClock Clock { get; private set; }
        public bool IsReady { get; private set; }

        public int MapRadius => Map?.Radius ?? mapRadius;
        public float TileSize => tileSize;

        /// <summary>지금 맵에서 건설·선택 같은 월드 조작을 받아도 되는지(경매 중·상점·파산이면 false).</summary>
        public bool AcceptsWorldInput => IsReady && Campaign.State == CampaignState.InSettlement && !Auctions.IsOpen;

        /// <summary>새 정착지(첫 정착지 포함)가 준비될 때마다 발생. 뷰는 이때 맵을 다시 그린다.</summary>
        public event Action Initialized;

        private void Awake()
        {
            Initialize();
        }

        private void Update()
        {
            if (!IsReady) return;
            // 경매·상점·파산 중에는 시간이 흐르지 않는다(HUD의 재개 버튼도 무시).
            if (!AcceptsWorldInput && !Clock.IsPaused) Clock.SetPaused(true);
            Clock.Advance(Time.unscaledDeltaTime);
        }

        /// <summary>코드에서 세션을 만들 때(테스트·빌더) 설정 파일을 지정한다. Awake 전에 호출해야 한다.</summary>
        public void SetConfig(TextAsset config) => economyConfig = config;

        /// <summary>코드에서 세션을 만들 때 맵 파일 목록을 지정한다. Awake 전에 호출해야 한다.</summary>
        public void SetMaps(IEnumerable<TextAsset> mapFiles) => maps = mapFiles.ToList();

        /// <summary>코드에서 세션을 만들 때 절차 맵 설정을 지정한다(null이면 고정 맵만). Awake 전에 호출해야 한다.</summary>
        public void SetProceduralMapConfig(TextAsset config) => proceduralMapConfig = config;

        /// <summary>설정·맵을 읽고 새 캠페인의 첫 정착지를 시작한다. 실패하면 콘솔에 이유를 남기고 IsReady = false.</summary>
        public void Initialize()
        {
            IsReady = false;
            try
            {
                if (economyConfig == null) throw new InvalidOperationException("economyConfig(TextAsset)가 지정되지 않았습니다.");
                Config = EconomyConfigLoader.Parse(economyConfig.text);
                List<MapDefinition> definitions = maps.Where(m => m != null).Select(m => MapLoader.Parse(m.text)).ToList();
                if (definitions.Count == 0) definitions.Add(FallbackMap());

                Layout = new HexLayout(tileSize);
                ProceduralMapConfig procedural = proceduralMapConfig != null ? ProceduralMapConfigLoader.Parse(proceduralMapConfig.text) : null;
                Campaign = new Campaign(Config, definitions, campaignSeed, procedural);

                if (Clock == null)
                {
                    Clock = new GameClock(timeSettings, Config.ConfirmedRules.GameDayRealSecondsAt1x);
                    Clock.HourPassed += OnHourPassed;
                    if (logClockEvents)
                    {
                        Clock.DayStarted += t => Debug.Log($"[GameClock] {t.Day}일차 시작");
                        Clock.PhaseChanged += t => Debug.Log($"[GameClock] {t} — {(t.Phase == DayPhase.Day ? "낮" : "밤")}이 되었습니다");
                    }
                }

                BeginSettlement();
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameSession] 초기화 실패: {e.Message}", this);
            }
        }

        private void OnHourPassed(GameTimeStamp _)
        {
            if (AcceptsWorldInput) Settlement.AdvanceHour();
        }

        private void BeginSettlement()
        {
            if (Campaign.Current == null || Campaign.State == CampaignState.Shopping) Campaign.StartNextSettlement();
            Auctions.AuctionOpened += e => Debug.Log($"[Auction] {(e.Kind == AuctionKind.Emergency ? "긴급 매각" : "정기")} 경매 개최 — {e.Bids.Count}명 입찰, 시세 {e.Market:0.000}");
            Auctions.Bankrupted += r => Debug.LogWarning("[Auction] 파산: " + r);
            Clock.ResetForNewSettlement();
            Clock.SetPaused(false);
            IsReady = true;
            Initialized?.Invoke();
        }

        // ───────────── 경매·상점 명령(UI가 호출) ─────────────

        public void SellTo(int bidIndex)
        {
            if (Auctions != null && Auctions.IsOpen) Auctions.Sell(bidIndex);
        }

        public void HoldAuction()
        {
            if (Auctions == null || !Auctions.IsOpen) return;
            Auctions.Hold();
            Clock.SetPaused(false);
        }

        public CommandResult BuyEquipment(string id) => Campaign.BuyEquipment(id);

        /// <summary>상점에서 다음 정착지로 간다.</summary>
        public void GoToNextSettlement()
        {
            if (Campaign.State != CampaignState.Shopping) return;
            BeginSettlement();
        }

        /// <summary>파산 후(또는 아무 때나) 새 캠페인을 시작한다.</summary>
        public void RestartCampaign() => Initialize();

        /// <summary>Core의 평면 좌표(X, Z)를 이 오브젝트 기준 월드 좌표로 바꾼다.</summary>
        public Vector3 ToWorld(HexPoint point, float height = 0f) =>
            transform.TransformPoint(new Vector3((float)point.X, height, (float)point.Z));

        /// <summary>월드 좌표를 Core 평면 좌표로 바꾼다(높이 무시).</summary>
        public HexPoint ToHexPoint(Vector3 world)
        {
            Vector3 local = transform.InverseTransformPoint(world);
            return new HexPoint(local.x, local.z);
        }

        /// <summary>지금 맵 파일의 샘플 배치를 예약한다. 실패한 항목 설명을 돌려준다.</summary>
        public List<string> RequestSampleLayout()
        {
            List<string> failures = Campaign.CurrentMap.RequestSampleLayout(Settlement);
            foreach (string m in failures) Debug.LogWarning("[GameSession] 샘플 배치 " + m);
            return failures;
        }

        private MapDefinition FallbackMap()
        {
            var def = new MapDefinition { Id = "inspector", Name = "인스펙터 지형", Radius = mapRadius };
            foreach (TerrainEntry t in terrain) def.Terrain.Add(new MapTerrainEntry { Q = t.q, R = t.r, Type = t.terrain.ToString() });
            return def;
        }

        /// <summary>맵 파일이 없을 때의 대체 지형(맵 1과 같음).</summary>
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
