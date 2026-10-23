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
        /// <summary>경매 진행·입찰자 성향(4단계, 잠정).</summary>
        public AuctionSettings Auction { get; set; }
        /// <summary>영구 장비 목록(4단계, 잠정).</summary>
        public List<EquipmentDefinition> Equipment { get; set; }
        /// <summary>장비 강화 규칙(2026-10-23). 없으면 기본값(효과 1.0/1.3/1.5배, 비용 가격 ×2.0/×3.0).</summary>
        public EquipmentUpgradeSettings EquipmentUpgrade { get; set; }
        /// <summary>재해(2026-10-23: 전염병). 없으면 재해 없음.</summary>
        public DisasterSettings Disasters { get; set; }

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
        /// <summary>
        /// 건설·개량·철거 시간 배율(2026-10-23 흐름 조정, Docs/decision_log_auto.md FL1~FL3). 레벨표의 시간 × 이 값(반올림, 최소 1).
        /// 1이면 원본 v0.8. 하루 길이(GameDayRealSecondsAt1x)를 늘리면서 같이 줄여 '여유 있게 많이 짓는' 흐름을 만든다.
        /// </summary>
        public double ConstructionHoursMultiplier { get; set; } = 1;
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
        /// <summary>빈 땅 배율의 기준 비율(이 비율이면 배율 1). 2026-10-23 로드맵 1-⑥, 기본 0.6.</summary>
        public double OpenLandShareCenter { get; set; } = 0.6;
        /// <summary>
        /// 가동 숙성(2026-10-23, 몰아짓기 방지): 막 완공한 생산 시설의 실적은 이 비율로 시작해 MaturityFullHours 동안 100%까지 오른다.
        /// 1이면 숙성 없음(원본 v0.8). 개발 중 자산도 이 비율을 곱한다.
        /// </summary>
        public double MaturityStartRatio { get; set; } = 1;
        public double MaturityFullHours { get; set; } = 48;
        /// <summary>석재 배율이 '24h 평균'과 '가동 중 채석장의 생산 능력' 중 큰 값을 쓴다(2026-10-23, 다 지은 뒤 채석장 철거 방지).</summary>
        public bool StoneMultiplierUsesQuarryCapacity { get; set; }
        public InfraMultipliers Infra { get; set; }
        public double FixedTestBidMultiplier { get; set; }
    }

    /// <summary>경매 진행 규칙과 입찰자. 값은 모두 플레이테스트 기본값.</summary>
    public sealed class AuctionSettings
    {
        public string Status { get; set; }
        /// <summary>경매 한 번에 오는 입찰자 수 범위(입찰자 목록에서 무작위, 중복 없음).</summary>
        public int AttendeesMin { get; set; } = 1;
        public int AttendeesMax { get; set; } = 2;
        /// <summary>당일 시세 = MarketCenter × (1 ± MarketSwing 균등 무작위). 경매 당일 공개.</summary>
        public double MarketCenter { get; set; } = 1.0;
        public double MarketSwing { get; set; }
        /// <summary>긴급 매각 입찰가 감액 비율(0.2 = −20%).</summary>
        public double EmergencyPenalty { get; set; }
        /// <summary>보류 한 번마다 이후 경매 시세에 곱하는 감소율(0.1 = 보류마다 ×0.9). 2026-10-23 게임성 보강: '늘 보류+투자'가 정답이 되지 않게.</summary>
        public double HoldMarketPenalty { get; set; }
        /// <summary>개발 중 자산 가치 = 기대 생산 × 진행률 × 이 값.</summary>
        public double DevelopmentAssetRatio { get; set; }
        /// <summary>자금 &lt; 0 상태가 이 시간 이상 이어지면 파산(경매장이 있을 때).</summary>
        public int BankruptcyGraceHours { get; set; } = 24;
        public List<BidderProfile> Bidders { get; set; }
    }

    /// <summary>
    /// 입찰자 성향. 가중치 1이면 중립. 배율 가중치는 '배율이 1을 넘는 부분'에 곱한다
    /// (예: 식량 배율 1.4, 가중치 0.5 → 1.2).
    /// </summary>
    public sealed class BidderProfile
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double OreWeight { get; set; } = 1;
        public double FoodMultiplierWeight { get; set; } = 1;
        public double StoneMultiplierWeight { get; set; } = 1;
        public double InfraWeight { get; set; } = 1;
        /// <summary>빈 땅 배율 가중치(부동산 개발업자·건설업자, 2026-10-23 로드맵 1-⑥). 배율 = 1 + 가중치 × (빈 땅 비율 − 기준 비율). 0이면 영향 없음.</summary>
        public double OpenLandWeight { get; set; }
        /// <summary>공사 중 시설 종류별 개발 중 자산 가중치(없는 종류는 1).</summary>
        public Dictionary<string, double> DevelopmentWeights { get; set; }
        /// <summary>현금으로 주는 비율. 1보다 작으면 그만큼 다음 정착지 혜택으로 보상한다.</summary>
        public double CashShare { get; set; } = 1;
        /// <summary>매각 시 다음 정착지 시작 식량 추가분.</summary>
        public double NextSettlementFoodBonus { get; set; }
        /// <summary>매각 시 다음 정착지에서 석재·공사 시간 없이 바로 완공 배치할 수 있는 시설 수(종류 → 개수).</summary>
        public Dictionary<string, int> NextSettlementFreeBuilds { get; set; }

        public double DevelopmentWeight(string kind) =>
            DevelopmentWeights != null && DevelopmentWeights.TryGetValue(kind, out double w) ? w : 1.0;

        /// <summary>모든 가중치 1인 중립 평가(기준 가치 V).</summary>
        public static BidderProfile Neutral => new BidderProfile { Id = "neutral", Name = "기준" };
    }

    /// <summary>영구 장비. 정착지를 옮겨도 유지된다.</summary>
    /// <summary>영구 장비. 정착지에 들어갈 때 현장 장비 슬롯(5개)에 넣은 것만 효과가 있다.</summary>
    public sealed class EquipmentDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double Price { get; set; }
        /// <summary>상점에 보일 한 줄 설명(어떤 약점에 대응하는지 등).</summary>
        public string Description { get; set; }
        /// <summary>경제 효과(바꿀 항목만 적는다). 형식은 지역 모디파이어와 같다.</summary>
        public DesertSettlement.Core.Economy.EconomyEffects Effects { get; set; } = new DesertSettlement.Core.Economy.EconomyEffects();

        /// <summary>우물 방출량 배율(1.2 = +20%). Effects에서 읽는다.</summary>
        public double WellSupplyMultiplier => Effects?.WellSupplyMultiplier ?? 1;
        /// <summary>강화 최고 레벨(구매 = Lv1). 없으면 강화 설정의 효과 단계 수. 1이면 강화 불가(정수 효과인 추가 작업반 등).</summary>
        public int? MaxLevel { get; set; }
    }

    /// <summary>
    /// 장비 강화(2026-10-23 로드맵 1-④, 수치 잠정). Lv n의 효과 = 기본 효과 × EffectScales[n-1](<see cref="DesertSettlement.Core.Economy.EconomyEffects.Scaled"/>),
    /// Lv n → n+1 비용 = 가격 × CostMultipliers[n-1]. 근거는 Docs/decision_log_auto.md E1~E5.
    /// </summary>
    public sealed class EquipmentUpgradeSettings
    {
        public string Status { get; set; }
        // 리스트 기본값을 두지 않는다: Newtonsoft가 기본값 리스트에 JSON 값을 덧붙이는 문제(2026-10-23 재해 설정에서 겪음).
        public List<double> EffectScales { get; set; }
        public List<double> CostMultipliers { get; set; }

        public IReadOnlyList<double> EffectiveEffectScales => EffectScales ?? new List<double> { 1.0, 1.3, 1.5 };
        public IReadOnlyList<double> EffectiveCostMultipliers => CostMultipliers ?? new List<double> { 2.0, 3.0 };
    }

    public sealed class InfraMultipliers
    {
        public double Well { get; set; }
        public double AuctionHall { get; set; }
        public double PerOperationalCanal { get; set; }
        public double PerOperationalWarehouse { get; set; }
        /// <summary>가동 중 병원 하나당 기반시설 배율 가산(2026-10-23 제안 0.01).</summary>
        public double PerOperationalHospital { get; set; }
        /// <summary>가동 중 물탱크 하나당 기반시설 배율 가산(2026-10-23 로드맵 1-⑥, 수자원 사업자가 크게 봄).</summary>
        public double PerOperationalWaterTank { get; set; }
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
    /// <summary>
    /// 재해 설정(2026-10-23 확정 규칙, 수치 잠정). Docs/work_items_2026-10-23.md 4번.
    /// 정착지마다 1~2번(가끔 0번), 첫 GraceHours 동안은 없음, 시작 WarningHours 전에 예고, 여러 재해가 겹칠 수 있음.
    /// </summary>
    public sealed class DisasterSettings
    {
        public int GraceHours { get; set; } = 48;
        public int WarningHours { get; set; } = 6;
        /// <summary>유예가 끝난 뒤 이 시간 안에서 시작 시각을 고른다(첫 경매 무렵까지).</summary>
        public int ScheduleWindowHours { get; set; } = 72;
        /// <summary>정착지 하나의 재해 횟수 확률. [0]=0번, [1]=1번, [2]=2번 ….</summary>
        /// (기본값을 리스트로 두면 JSON 읽을 때 덧붙여지므로 null로 두고, 없으면 <see cref="EffectiveCountWeights"/>가 0.15·0.55·0.30을 쓴다.)
        public List<double> CountWeights { get; set; }
        public IList<double> EffectiveCountWeights => CountWeights ?? new List<double> { 0.15, 0.55, 0.30 };
        /// <summary>나올 수 있는 재해 종류와 가중치(epidemic·sandstorm·locusts·blight, 2026-10-23 4종 같은 확률).</summary>
        public List<DisasterTypeDefinition> Types { get; set; }
        public EpidemicSettings Epidemic { get; set; } = new EpidemicSettings();
        public SandstormSettings Sandstorm { get; set; } = new SandstormSettings();
        public LocustSettings Locusts { get; set; } = new LocustSettings();
        public BlightSettings Blight { get; set; } = new BlightSettings();
        /// <summary>지역 모디파이어 id → 재해 종류 id → 가중치 배율(약점 모래바람 지대 = 모래폭풍 ×3 등, 2026-10-23 D7).</summary>
        public Dictionary<string, Dictionary<string, double>> RegionWeightMultipliers { get; set; }
    }

    /// <summary>모래폭풍(2026-10-23 D1~D3): 지속 동안 수로 거리 손실 × CanalLossMultiplier, 공사 속도 × ConstructionSpeed. 수로 방풍막이 손실 증가를 줄인다.</summary>
    public sealed class SandstormSettings
    {
        public int MinHours { get; set; } = 6;
        public int MaxHours { get; set; } = 18;
        public double CanalLossMultiplier { get; set; } = 2.0;
        public double ConstructionSpeed { get; set; } = 0.5;
    }

    /// <summary>메뚜기떼(2026-10-23 D4·D5): 지속 동안 농장 생산 × FarmProduction, 시작할 때 식량 재고 −FoodStockLoss. 방충망이 피해를 줄인다.</summary>
    public sealed class LocustSettings
    {
        public int MinHours { get; set; } = 4;
        public int MaxHours { get; set; } = 12;
        public double FarmProduction { get; set; } = 0.0;
        public double FoodStockLoss { get; set; } = 0.15;
    }

    /// <summary>작물 탄저병(2026-10-23 D6): 지속 동안 농장 생산 × FarmProduction. 관개 설비가 피해를 줄인다.</summary>
    public sealed class BlightSettings
    {
        public int MinHours { get; set; } = 48;
        public int MaxHours { get; set; } = 96;
        public double FarmProduction { get; set; } = 0.5;
    }

    public sealed class DisasterTypeDefinition
    {
        /// <summary>재해 종류 id(epidemic).</summary>
        public string Id { get; set; }
        public string Name { get; set; }
        public double Weight { get; set; } = 1;
    }

    /// <summary>
    /// 전염병(사용자 정의 모델): 심각도 S가 천천히 오르고, 피해 = S × (1 − 저항력 R)만큼 생산 시설 효율이 준다.
    /// R은 피해가 클수록 빨리 오르고, R ≥ S이면 S가 줄어 0이 되면 끝난다(둘 다 100%에 붙어 끝나지 않는 일을 막으려고 ≥). 끝난 뒤 R은 천천히 줄어든다(장비가 준 시작값 아래로는 안 내려감).
    /// </summary>
    public sealed class EpidemicSettings
    {
        /// <summary>시작할 때 심각도. 2026-10-23 B3 결정: 0.35(처음 0.05는 너무 약함).</summary>
        public double InitialSeverity { get; set; } = 0.35;
        /// <summary>R &lt; S일 때 시간당 심각도 상승. B3 결정 수치로 약 128시간 지속·최대 피해 38%(방역 장비 49시간).</summary>
        public double SeverityRisePerHour { get; set; } = 0.01;
        /// <summary>R ≥ S일 때 시간당 심각도 감소. B3 결정: 0.03(정점 뒤 빨리 회복).</summary>
        public double SeverityFallPerHour { get; set; } = 0.03;
        /// <summary>전염병 중 시간당 저항력 상승 = 기본값 + 이 값 × 현재 피해. B3 결정: 0.04(처음 0.06).</summary>
        public double ResistanceBasePerHour { get; set; } = 0.002;
        public double ResistancePerDamage { get; set; } = 0.04;
        /// <summary>전염병이 끝난 뒤 시간당 저항력 감소.</summary>
        public double ResistanceDecayPerHour { get; set; } = 0.005;
        /// <summary>병원 인근 건물이 받는 피해 비율(절반 = 0.5, 결정).</summary>
        public double HospitalDamageFactor { get; set; } = 0.5;
        /// <summary>병원이 덮는 거리(1 = 병원 타일 + 이웃 6칸, 결정).</summary>
        public int HospitalRadius { get; set; } = 1;
    }

    public static class BuildingKinds
    {
        public const string Well = "well";
        public const string Farm = "farm";
        public const string Quarry = "quarry";
        public const string Mine = "mine";
        public const string AuctionHall = "auctionHall";
        public const string Warehouse = "warehouse";
        /// <summary>병원(2026-10-23): 인근 1칸 건물의 전염병 피해를 절반으로.</summary>
        public const string Hospital = "hospital";
        public const string WaterTank = "waterTank";
        public const string CanalEdge = "canalEdge";

        public static readonly string[] All =
        {
            Well, Farm, Quarry, Mine, AuctionHall, Warehouse, Hospital, WaterTank, CanalEdge,
        };
    }
}
