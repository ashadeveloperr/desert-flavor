using System;
using System.Collections.Generic;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Hex;
using DesertSettlement.Core.Water;

namespace DesertSettlement.Core.Economy
{
    /// <summary>
    /// 정착지 경제에 쓰는 규칙 값 모음. 대부분 설정 JSON에서 오고(<see cref="FromConfig"/>),
    /// JSON에 아직 없는 구현 단계 값은 여기 기본값으로 둔다(주석에 상태 표시).
    /// </summary>
    public sealed class SettlementRules
    {
        public EconomyConfig Config { get; }

        // ── 확정 규칙 (confirmedRules) ──
        public int ConstructionSlots { get; set; }
        /// <summary>착공 후 취소 시 투입 석재 환급 비율. [확정] 0.5</summary>
        public double CancelRefundRatio { get; set; }

        // ── 프로토타입 값 (prototype) ──
        public double StartingCash { get; set; }
        public double StarterStone { get; set; }
        public double StarterFood { get; set; }
        public double BaseFoodStorage { get; set; }
        public double BaseStoneStorage { get; set; }
        public double FoodDeficitFullIntensityRatio { get; set; }
        public double FoodDeclinePerDayMax { get; set; }
        public double FoodRecoveryPerDay { get; set; }
        public double ClosedUpkeepRatio { get; set; }
        /// <summary>MVP 레벨 상한(L2). L3 데이터는 남아 있어도 개량 불가.</summary>
        public int MaxFacilityLevel { get; set; }

        // ── 구현 단계 값 (JSON에 없음) ──
        /// <summary>철거 완료 시 투입 석재(건설+개량) 환급 비율. [확정 2026-10-22] 0.5</summary>
        public double DemolishRefundRatio { get; set; } = 0.5;
        /// <summary>철거 공사 시간 = 해당 시설 L1 건설 시간 × 이 값(올림, 최소 1). [잠정 임시값]</summary>
        public double DemolishHoursRatio { get; set; } = 0.5;
        /// <summary>광물 생산 배율(지형 품질·장비). 장비 시스템 전까지 1.</summary>
        public double OreMultiplier { get; set; } = 1.0;

        /// <summary>
        /// 시설 종류별로 지을 수 있는 지형. [확정 2026-10-22]
        /// 우물=수원, 광산=광맥, 채석장=암반, 농장·창고·경매장=사막, 장애물=불가. 수로·수문은 변에 짓으므로 지형 무관.
        /// </summary>
        public Dictionary<string, TerrainType[]> AllowedTerrain { get; } = new Dictionary<string, TerrainType[]>
        {
            [BuildingKinds.Well] = new[] { TerrainType.WaterSource },
            [BuildingKinds.Mine] = new[] { TerrainType.OreVein },
            [BuildingKinds.Quarry] = new[] { TerrainType.Bedrock },
            [BuildingKinds.Farm] = new[] { TerrainType.Desert },
            [BuildingKinds.Warehouse] = new[] { TerrainType.Desert },
            [BuildingKinds.AuctionHall] = new[] { TerrainType.Desert },
        };

        /// <summary>MVP에서 지을 수 없는 시설(물탱크는 MVP 이후, W7).</summary>
        public HashSet<string> DisabledKinds { get; } = new HashSet<string> { BuildingKinds.WaterTank };

        /// <summary>수동 폐쇄할 수 없는 시설(MVP: 창고).</summary>
        public HashSet<string> CannotClose { get; } = new HashSet<string> { BuildingKinds.Warehouse };

        public WaterNetworkSettings Water { get; set; }

        private SettlementRules(EconomyConfig config)
        {
            Config = config;
        }

        public static SettlementRules FromConfig(EconomyConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            ConfirmedRules c = config.ConfirmedRules;
            PrototypeSettings p = config.Prototype;
            return new SettlementRules(config)
            {
                ConstructionSlots = c.ConstructionSlots,
                CancelRefundRatio = c.PostStartCancelStoneRefundRatio,
                StartingCash = p.StartingCash,
                StarterStone = p.SiteStarterStone,
                StarterFood = p.SiteStarterFood,
                BaseFoodStorage = p.StartingFoodStorage,
                BaseStoneStorage = p.StartingStoneStorage,
                FoodDeficitFullIntensityRatio = p.FoodDeficitFullIntensityRatio,
                FoodDeclinePerDayMax = p.FoodDeclinePerDayMax,
                FoodRecoveryPerDay = p.FoodRecoveryPerDay,
                ClosedUpkeepRatio = p.ClosedFacilityUpkeepRatio,
                MaxFacilityLevel = p.InitialMvpMaxFacilityLevel > 0 ? p.InitialMvpMaxFacilityLevel : int.MaxValue,
                Water = WaterNetworkSettings.FromConfig(config),
            };
        }

        public static bool IsEdgeKind(string kind) => kind == BuildingKinds.CanalEdge || kind == BuildingKinds.CanalGate;

        /// <summary>이 시설 종류가 가질 수 있는 최고 레벨(설정 데이터와 MVP 상한 중 작은 값).</summary>
        public int MaxLevelOf(string kind) => Math.Min(MaxFacilityLevel, Config.MaxLevel(kind));

        public BuildingLevel Level(string kind, int level) =>
            Config.GetLevel(kind, level) ?? throw new ArgumentException($"설정에 {kind} L{level}이 없습니다.");
    }
}
