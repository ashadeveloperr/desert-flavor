using System.Collections.Generic;

namespace DesertSettlement.Core.Config
{
    // economy_balance_v0.8_config.json 의 구조를 그대로 옮긴 데이터 클래스.
    // JSON 키(camelCase)와 속성 이름(PascalCase)은 대소문자 무시로 자동 매칭된다.
    // JSON에 새 키를 추가하면 여기에 같은 이름의 속성을 추가하면 된다. 없는 속성은 무시된다.

    public sealed class EconomyConfig
    {
        public string Schema { get; set; }
        public string Status { get; set; }
        public string Updated { get; set; }
        public ConfirmedRules ConfirmedRules { get; set; }
        public PrototypeSettings Prototype { get; set; }
        public PurchaseSettings Purchase { get; set; }
        public AuctionSeed AuctionSeed { get; set; }

        /// <summary>시설 종류(<see cref="BuildingKinds"/>) → 레벨별 명세. 인덱스 0 = L1.</summary>
        public Dictionary<string, List<BuildingLevel>> BuildingLevels { get; set; }

        /// <summary>Python 시뮬레이터 기준 결과값. 게임 로직에서 쓰지 않고 비교 검증용.</summary>
        public Dictionary<string, double> SampleBenchmarks { get; set; }

        /// <summary>시설 종류의 레벨 명세. level은 1부터. 없으면 null.</summary>
        public BuildingLevel GetLevel(string kind, int level)
        {
            if (BuildingLevels == null || !BuildingLevels.TryGetValue(kind, out List<BuildingLevel> levels)) return null;
            int index = level - 1;
            return index >= 0 && index < levels.Count ? levels[index] : null;
        }

        public int MaxLevel(string kind) =>
            BuildingLevels != null && BuildingLevels.TryGetValue(kind, out List<BuildingLevel> levels) ? levels.Count : 0;
    }

    /// <summary>사용자가 확정한 규칙. 바꾸려면 기획 확정이 먼저 필요하다.</summary>
    public sealed class ConfirmedRules
    {
        public string ConstructionCurrency { get; set; }
        public int AuctionHistoryGameHours { get; set; }
        public bool WellWaterSupplyIgnoresFoodMultiplier { get; set; }
        public bool FoodStoneInventoryRemainsAtSoldSettlement { get; set; }
        public int AuctionRepeatsGameHours { get; set; }
        public int ConstructionSlots { get; set; }
        public double PostStartCancelStoneRefundRatio { get; set; }
        public double GameDayRealSecondsAt1x { get; set; }
        public string WaterNetworkGeometry { get; set; }
        public string WaterOrder { get; set; }
    }

    /// <summary>프로토타입 기본값(잠정). 플레이테스트로 조정한다.</summary>
    public sealed class PrototypeSettings
    {
        public int EconomicTickGameHours { get; set; }
        public double StartingCash { get; set; }
        public double SiteStarterStone { get; set; }
        public double SiteStarterFood { get; set; }
        public bool StarterSuppliesAreSiteSpecific { get; set; }
        public double StartingFoodStorage { get; set; }
        public double StartingStoneStorage { get; set; }
        public double FoodDeficitFullIntensityRatio { get; set; }
        public double FoodDeclinePerDayMax { get; set; }
        public double FoodRecoveryPerDay { get; set; }
        public bool FoodRecoveryRequiresOperatingWateredFarm { get; set; }
        public bool FoodRecoveryFreezesWhenNoDemand { get; set; }
        public double ClosedFacilityUpkeepRatio { get; set; }
        public double CanalLossPerWholeEdge { get; set; }
        public int InitialMvpMaxFacilityLevel { get; set; }
    }

    public sealed class PurchaseSettings
    {
        public PurchasePrice Food { get; set; }
        public PurchasePrice Stone { get; set; }
    }

    /// <summary>외부 구매 가격. n번째 구매 단가 = BaseCost × IncrementMultiplier^(n-1) (잠정).</summary>
    public sealed class PurchasePrice
    {
        public double BaseCost { get; set; }
        public double IncrementMultiplier { get; set; }
    }

    public sealed class AuctionSeed
    {
        public double OreProduction24hValueFactor { get; set; }
        public double FoodProductionAverageForFullBonus { get; set; }
        public double FoodMaxMultiplier { get; set; }
        public double StoneProductionAverageForFullBonus { get; set; }
        public double StoneMaxMultiplier { get; set; }
        public InfraMultipliers Infra { get; set; }
        public double FixedTestBidMultiplier { get; set; }
    }

    public sealed class InfraMultipliers
    {
        public double Well { get; set; }
        public double AuctionHall { get; set; }
        public double PerOperationalCanal { get; set; }
        public double PerOperationalWarehouse { get; set; }
        public double Maximum { get; set; }
    }

    /// <summary>
    /// 시설 한 레벨의 명세. 모든 시설이 공통으로 갖는 값(Stone, Hours, FoodUse, Upkeep)은 필수,
    /// 나머지는 시설 종류에 따라 있을 때만 값이 들어간다(없으면 null).
    /// 단위: 자원·물은 게임 1시간당, Hours는 공사 게임시간.
    /// </summary>
    public sealed class BuildingLevel
    {
        public double? Stone { get; set; }
        public double? Hours { get; set; }
        public double? FoodUse { get; set; }
        public double? Upkeep { get; set; }

        public double? WaterSupply { get; set; }
        public double? WaterDemand { get; set; }
        public double? FoodProduction { get; set; }
        public double? StoneProduction { get; set; }
        public double? OreProduction { get; set; }
        public double? FoodStorageBonus { get; set; }
        public double? StoneStorageBonus { get; set; }
        public double? Capacity { get; set; }
        public double? ChargePerHour { get; set; }
        public double? DischargePerHour { get; set; }
        public double? CapacityPerHour { get; set; }
        public double? LossPerWholeEdge { get; set; }

        /// <summary>아직 확정되지 않은 개량 효과 메모(예: 경매장 L2). 로직에서 사용하지 않는다.</summary>
        public string UpgradeEffectProposal { get; set; }
    }

    /// <summary>buildingLevels의 키. 오타 방지를 위해 문자열 대신 이 상수를 쓴다.</summary>
    public static class BuildingKinds
    {
        public const string Well = "well";
        public const string Farm = "farm";
        public const string Quarry = "quarry";
        public const string Mine = "mine";
        public const string AuctionHall = "auctionHall";
        public const string Warehouse = "warehouse";
        public const string WaterTank = "waterTank";
        public const string CanalEdge = "canalEdge";
        public const string CanalGate = "canalGate";

        public static readonly string[] All =
        {
            Well, Farm, Quarry, Mine, AuctionHall, Warehouse, WaterTank, CanalEdge, CanalGate,
        };
    }
}
