using System.Collections.Generic;

namespace DesertSettlement.Core.Config
{
    // economy_balance_v0.8_config.json 의 구조를 그대로 옮긴 데이터 클래스.
    // JSON 키(camelCase)와 속성 이름(PascalCase)은 대소문자 무시로 자동 매칭된다.
    // JSON에 새 키를 추가하면 여기에 같은 이름의 속성을 추가한다. 클래스에 없는 키는 불러오기 오류다
    // (JsonKeyCheck, 결정 CF1). 설명용 키는 '_'로 시작한다.

    public sealed class EconomyConfig
    {
        public string Schema { get; set; }
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
        /// <summary>주택·노동력(결정 LB1~). 없으면 원본 v0.8: 시설이 식량을 먹고(<c>foodUse</c>) 식량 계수 F가 효율에 곱해진다.</summary>
        public LaborSettings Labor { get; set; }
        /// <summary>재화별 수치(결정 FD1~, 키 = <c>GoodsCatalog.Id</c>: grain·fruit·bread·meat·beer·wine·spice). 없으면 곡물만 있는 것처럼.</summary>
        public Dictionary<string, GoodsSettings> Goods { get; set; }
        /// <summary>생활 수준(결정 LS1~). 없거나 노동력이 꺼져 있으면 생활 수준은 0에 머문다.</summary>
        public LivingSettings Living { get; set; }

        /// <summary>시설 종류(<see cref="BuildingKinds"/>) → 레벨별 명세. 인덱스 0 = L1.</summary>
        public Dictionary<string, List<BuildingLevel>> BuildingLevels { get; set; }

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
        public int AuctionHistoryGameHours { get; set; }
        public int AuctionRepeatsGameHours { get; set; }
        public int ConstructionSlots { get; set; }
        public double PostStartCancelStoneRefundRatio { get; set; }
        public double GameDayRealSecondsAt1x { get; set; }
        /// <summary>
        /// 건설·개량·철거 시간 배율(2026-10-23 흐름 조정, Docs/decision_log_auto.md FL1~FL3). 레벨표의 시간 × 이 값(반올림, 최소 1).
        /// 1이면 원본 v0.8. 하루 길이(GameDayRealSecondsAt1x)를 늘리면서 같이 줄여 '여유 있게 많이 짓는' 흐름을 만든다.
        /// </summary>
        public double ConstructionHoursMultiplier { get; set; } = 1;
        /// <summary>
        /// 명령 순간 착공(2026-10-26 JQ2, 이슈 #8). true면 건설·개량·철거 명령(또는 슬롯·석재가 생기는 명령) 직후 바로 착공하고,
        /// 진행 중인 이 시간을 첫 1시간으로 센다(명령부터 완공까지 표시 시간 이하). false = 다음 정각에 착공(원본 v0.8).
        /// </summary>
        public bool StartJobsOnCommand { get; set; }
    }

    /// <summary>프로토타입 기본값(잠정). 플레이테스트로 조정한다.</summary>
    public sealed class PrototypeSettings
    {
        public double StartingCash { get; set; }
        public double SiteStarterStone { get; set; }
        public double SiteStarterFood { get; set; }
        public double StartingFoodStorage { get; set; }
        public double StartingStoneStorage { get; set; }
        public double FoodDeficitFullIntensityRatio { get; set; }
        public double FoodDeclinePerDayMax { get; set; }
        public double FoodRecoveryPerDay { get; set; }
        public double ClosedFacilityUpkeepRatio { get; set; }
        public double CanalLossPerWholeEdge { get; set; }
        public int InitialMvpMaxFacilityLevel { get; set; }
    }

    /// <summary>
    /// 주택·노동력(결정 LB1~). 주민은 주택에 살며 수로 물과 식량을 쓰고,
    /// 우물·농장·채석장·광산(레벨표 <c>workers</c>)과 공사(레벨표 <c>buildWorkers</c>)에 자동 배치된다.
    /// 물·식량이 모자라면 서서히 떠나고, 다시 충분해지면 더 천천히 돌아온다.
    /// </summary>
    public sealed class LaborSettings
    {
        /// <summary>주민(기본 인부 포함) 1명이 한 시간에 먹는 식량.</summary>
        public double FoodPerResidentPerHour { get; set; }
        /// <summary>주택 주민 1명이 한 시간에 쓰는 물(주택이 수로에서 취수). 기본 인부는 물을 쓰지 않는다.</summary>
        public double WaterPerResidentPerHour { get; set; }
        /// <summary>주민이 목표보다 많을 때 시간당 떠나는 수 = 정원 × 이 값.</summary>
        public double LeavePerHour { get; set; }
        /// <summary>주민이 목표보다 적을 때 시간당 돌아오는 수 = 정원 × 이 값(떠나는 속도보다 느리게).</summary>
        public double ReturnPerHour { get; set; }
        /// <summary>정착지마다 석재·인원·시간 없이 바로 완공되는 주택 수(결정: 첫 주택 1채 무료).</summary>
        public int FreeHouses { get; set; }
        /// <summary>건설 슬롯 1개당 기본 인부 수. 기본 인부는 공사에만 일하고, 물은 안 쓰고 식량은 먹으며, 떠나지 않는다(막힘 방지).</summary>
        public double CrewPerConstructionSlot { get; set; }
        /// <summary>주민 1명이 한 시간에 마시는 기호품(맥주·와인 합, 결정 FD3). 없어도 배고프지 않고 생활 수준만 덜 오른다.</summary>
        public double DrinkPerResidentPerHour { get; set; }
        /// <summary>
        /// 정착지 시작 유예(시간, 결정 BQ3): 정착지가 시작된 뒤 이 시간 동안은 주택 물 부족을 떠남·생활 수준에 세지 않는다.
        /// 첫 주택은 무료로 바로 차지만 우물·수로는 몇 시간 뒤에 이어지기 때문. 0이면 유예 없음.
        /// </summary>
        public double StartGraceHours { get; set; }
    }

    /// <summary>
    /// 생활 수준(−100~100, 결정 LS1~LS7). 매시간 목표를 계산하고 지금 값이 그쪽으로 천천히 움직인다(아무 일 없으면 목표 0).
    /// 목표 = 먹고 마신 재화의 등급 점수 평균(<c>goods.*.standardPoints</c>) × 상위 재화 종류 수 배율 × 향신료(충족률만큼 1 + <see cref="SpiceBonus"/>), 100에서 자름.
    /// 식량 충족률과 주택 물 공급률(주민 가중 평균) 중 낮은 쪽이 1보다 모자라면 목표는 음수: −100 × 모자람 ÷ (1 − <see cref="ShortageFloor"/>).
    /// </summary>
    public sealed class LivingSettings
    {
        /// <summary>목표가 더 높을 때 시간당 오르는 폭.</summary>
        public double RisePerHour { get; set; }
        /// <summary>목표가 더 낮을 때 시간당 내려가는 폭.</summary>
        public double FallPerHour { get; set; }
        /// <summary>충족률이 이 값 이하면 목표 −100(0.5 = 50%).</summary>
        public double ShortageFloor { get; set; }
        /// <summary>이번 시간 먹은 상위 재화 종류 수(1·2·3…)의 배율. 종류 수가 목록보다 많으면 마지막 값.</summary>
        public List<double> DiversityMultipliers { get; set; }
        /// <summary>향신료가 다 채워지면 목표에 곱하는 몫(0.5 = ×1.5, 결정 SP2).</summary>
        public double SpiceBonus { get; set; }
        /// <summary>상위 재화(등급 점수 > 0) 소비 1단위마다 함께 드는 향신료.</summary>
        public double SpicePerUpperGoods { get; set; }
        /// <summary>상위 재화로 셀 최소 비율(먹은 양 중 이 비율보다 적으면 종류 수에 안 넣음).</summary>
        public double MinShareToCount { get; set; }
        /// <summary>+100일 때 떠나는 속도 배율(−100이면 그 역수), 돌아오는 속도는 반대(결정 LS5).</summary>
        public double LeaveFactorAtMax { get; set; }
        /// <summary>±100일 때 생산 효율 가산(0.1 = ±10%, 결정 LS6).</summary>
        public double ProductionAtMax { get; set; }
    }

    /// <summary>
    /// 재화 하나의 수치(결정 FD1~FD8). 식량은 <see cref="FoodValue"/>·<see cref="EatWeight"/>, 기호품은 <see cref="DrinkWeight"/>로 소비된다.
    /// 재고가 있는 재화끼리 가중치 비율로 수요를 나눠 먹고(모자란 몫은 다른 재화가 채움), 실제 소비량 = 맡은 몫 ÷ 식량 효율.
    /// </summary>
    public sealed class GoodsSettings
    {
        /// <summary>1단위가 채우는 식량(곡물 = 1). 식량이 아니면 0.</summary>
        public double FoodValue { get; set; }
        /// <summary>식량 소비 가중치(재고가 있는 식량끼리의 비율).</summary>
        public double EatWeight { get; set; }
        /// <summary>기호품 소비 가중치(맥주·와인).</summary>
        public double DrinkWeight { get; set; }
        /// <summary>생활 수준 등급 점수(1등급 100·2등급 70·3등급 40, 곡물 0, 결정 LS2).</summary>
        public double StandardPoints { get; set; }
        /// <summary>식품 창고가 넘칠 때 남기는 순위(클수록 나중에 버림, 결정 FD5).</summary>
        public int KeepRank { get; set; }
        /// <summary>시장 판매 기준가(시장이 있을 때 판매만, 결정 FD6). 곡물은 <c>purchase.food</c>를 쓴다.</summary>
        public double SellPrice { get; set; }
        /// <summary>판매 압력이 이 값에 이르면 판매가 0(2차 곡선).</summary>
        public double SellLimit { get; set; }

        public static readonly GoodsSettings Default = new GoodsSettings { FoodValue = 1 };
    }

    public sealed class PurchaseSettings
    {
        public PurchasePrice Food { get; set; }
        public PurchasePrice Stone { get; set; }
        /// <summary>시장 규칙(2026-10-27 이슈 #18). 없으면 원본 v0.8: 어디서나 구매, 판매 없음, 값은 정착지 동안 회복 없음.</summary>
        public TradeSettings Trade { get; set; }
    }

    /// <summary>
    /// 외부 구매·판매 가격. 다음 1개 구매 단가 = BaseCost × IncrementMultiplier^(구매 압력). 살 때마다 압력 +1(현재 가격에 배율이 곱해짐).
    /// 판매 단가 = BaseCost × SellRatio × max(0, 1 − (판매 압력 ÷ SellLimit)²). 두 압력은 매시간 일정 비율로 0을 향해 줄어든다(<see cref="TradeSettings"/>).
    /// </summary>
    public sealed class PurchasePrice
    {
        public double BaseCost { get; set; }
        public double IncrementMultiplier { get; set; }
        /// <summary>판매 기준가 = 구매 기준가 × 이 비율(2026-10-27 사용자 결정 0.5).</summary>
        public double SellRatio { get; set; }
        /// <summary>판매 압력이 이 값에 이르면 판매가가 0(2차 곡선).</summary>
        public double SellLimit { get; set; }
    }

    /// <summary>시장(2026-10-27 이슈 #18, Docs/decision_log_auto.md MK1~).</summary>
    public sealed class TradeSettings
    {
        /// <summary>시장 1개일 때 구매·판매 압력이 매시간 줄어드는 비율(0.05 = 5%).</summary>
        public double RecoveryPerHour { get; set; }
        /// <summary>두 번째 시장이 회복 속도에 더하는 몫(0.2 → ×1.2).</summary>
        public double ExtraMarketGain { get; set; }
        /// <summary>시장을 하나 더 지을 때마다 더하는 몫에 곱하는 값(0.75 → +0.2, +0.15, +0.11 …, 상한 ×1.8).</summary>
        public double ExtraMarketDecay { get; set; }
        /// <summary>운영 중인 시장이 없을 때 떠돌이 상인 구매 가격 배율(판매는 불가).</summary>
        public double NoMarketPriceMultiplier { get; set; }

        /// <summary>운영 중인 시장 수에 따른 회복 속도 배율. 0·1개 = 1, n개 = 1 + gain × (1 − decay^(n−1)) ÷ (1 − decay).</summary>
        public double RecoveryFactor(int markets)
        {
            if (markets <= 1) return 1;
            if (ExtraMarketDecay >= 1 - 1e-12) return 1 + ExtraMarketGain * (markets - 1);
            return 1 + ExtraMarketGain * (1 - System.Math.Pow(ExtraMarketDecay, markets - 1)) / (1 - ExtraMarketDecay);
        }
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
        /// <summary>주민 1명의 가치(결정 CL1). 입찰자 <see cref="BidderProfile.PopulationWeight"/>를 곱해 광물 기반 가치에 더한다. 0이면 주민 가치 없음.</summary>
        public double ValuePerResident { get; set; }
    }

    /// <summary>경매 진행 규칙과 입찰자. 값은 모두 플레이테스트 기본값.</summary>
    public sealed class AuctionSettings
    {
        /// <summary>
        /// 경매 한 번에 오는 입찰자 수 범위(입찰자 목록에서 무작위 순서, 중복 없음).
        /// 2026-10-26 AH1부터 실제 인원 = <see cref="AttendeesWithOneAuctionHall"/> + (가동 중인 경매장 수 − 1)을 이 범위로 제한한 값
        /// (경매장 1·2·3개 = 2·3·4명, 이전 FB2는 무작위 2~3명).
        /// </summary>
        public int AttendeesMin { get; set; } = 1;
        public int AttendeesMax { get; set; } = 4;
        /// <summary>경매장 1개일 때 입찰자 수(2026-10-26 AH1). 경매장을 하나 더 지을 때마다 +1명.</summary>
        public int AttendeesWithOneAuctionHall { get; set; } = 2;
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
        /// <summary>주민 가치 가중치(결정 CL1). 주민 가치 = 사는 인원 × <c>auctionSeed.valuePerResident</c> × 이 값, 광물 기반 가치에 더한다. 이주 사업가만 크게.</summary>
        public double PopulationWeight { get; set; } = 1;
        /// <summary>생활 수준 가중치(결정 LS7): 가치 × (1 + 이 값 × 생활 수준 ÷ 100). 이주 사업가·부동산 개발업자만, 나머지 0.</summary>
        public double LivingStandardWeight { get; set; }
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
        /// <summary>가동 중 시장 하나당 기반시설 배율 가산(2026-10-27 이슈 #18, 병원·물탱크와 같은 0.01).</summary>
        public double PerOperationalMarket { get; set; }
        /// <summary>주택 하나당 기반시설 배율 가산 × 그 주택의 거주율(사는 인원 ÷ 정원, 결정 CL2).</summary>
        public double PerOperationalHouse { get; set; }
        public double Maximum { get; set; }
    }

    /// <summary>
    /// 시설 한 레벨의 명세. 모든 시설이 공통으로 갖는 값(Stone, Hours, Upkeep)은 필수,
    /// 나머지는 시설 종류에 따라 있을 때만 값이 들어간다(없으면 null).
    /// 단위: 자원·물은 게임 1시간당, Hours는 공사 게임시간.
    /// </summary>
    public sealed class BuildingLevel
    {
        public double? Stone { get; set; }
        public double? Hours { get; set; }
        /// <summary>운영 중 시설의 고정 식량 소비. 노동력(<see cref="LaborSettings"/>)이 꺼진 원본 v0.8 회귀 비교에서만 쓴다(노동력이 켜지면 주민이 먹는다).</summary>
        public double? FoodUse { get; set; }
        public double? Upkeep { get; set; }
        /// <summary>주택 정원(결정 LB1~).</summary>
        public double? Residents { get; set; }
        /// <summary>운영에 필요한 인원(우물·농장·채석장·광산). 생산 = 최대 × 물 공급률 × 배치율(배치 ÷ 필요). 없으면 사람 없이 동작.</summary>
        public double? Workers { get; set; }
        /// <summary>이 레벨로 짓는(개량하는) 공사에 필요한 인원. 철거는 L1 값. 모인 비율만큼 공사가 느려진다(0명이면 멈춤).</summary>
        public double? BuildWorkers { get; set; }
        /// <summary>병원 병상 수(결정 EV3). 전염병 환자가 병상만큼 병원에서 빨리 회복한다.</summary>
        public double? Beds { get; set; }
        /// <summary>과수원·목축지·제분소·양조장·포도주 공방·허브 농장의 시간당 재화 생산(결정 FD2). 경작지는 <see cref="FoodProduction"/>(곡물).</summary>
        public double? GoodsProduction { get; set; }
        /// <summary>가공 시설이 재화 1단위를 만드는 데 쓰는 원료(목축지·제분소·양조장 = 곡물, 포도주 공방 = 과일).</summary>
        public double? InputPerOutput { get; set; }

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
    }

    /// <summary>buildingLevels의 키. 오타 방지를 위해 문자열 대신 이 상수를 쓴다.</summary>
    /// <summary>
    /// 재해 설정(2026-10-23 확정 규칙, 수치 잠정). Docs/archive/work_items_2026-10-23.md 4번.
    /// 정착지마다 1~2번(가끔 0번), 첫 GraceHours 동안은 없음, 시작 WarningHours 전에 예고, 여러 재해가 겹칠 수 있음.
    /// </summary>
    public sealed class DisasterSettings
    {
        /// <summary>정착지 시작 뒤 재해가 없는 시간. 2026-10-26 AU2: 48 → 36(경매 주기 48h에 맞춰 재해를 겪는 정착지 비율 유지).</summary>
        public int GraceHours { get; set; } = 36;
        public int WarningHours { get; set; } = 6;
        /// <summary>유예가 끝난 뒤 이 시간 안에서 시작 시각을 고른다(첫 경매 무렵까지).</summary>
        public int ScheduleWindowHours { get; set; } = 48;
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
    /// 전염병(결정 EV1~): 주민 사이에 퍼지는 감염·회복 모형(SIR). 걸릴 수 있는 주민 S, 아픈 주민 I, 면역 R(회복했거나 방역 장비).
    /// 매시간 새 환자 = <see cref="TransmissionPerHour"/> × S × I ÷ 주민, 회복 = 병원 병상에 든 환자 × <see cref="HospitalRecoveryPerHour"/> + 집의 환자 × <see cref="HomeRecoveryPerHour"/>.
    /// 아픈 주민은 일하지 못한다. 회복하면 면역이 되고, 다음 전염병이 오면 면역의 <see cref="ImmunityCarryover"/>만 남는다.
    /// </summary>
    public sealed class EpidemicSettings
    {
        /// <summary>시작할 때 걸릴 수 있는 주민 중 아픈 비율.</summary>
        public double InitialInfectedShare { get; set; }
        /// <summary>전파율(시간당). 클수록 빨리 퍼지고 정점이 높다(재해 세기 배수를 곱함).</summary>
        public double TransmissionPerHour { get; set; }
        /// <summary>집에 있는 환자의 시간당 회복 비율(평균 앓는 시간 = 1 ÷ 이 값).</summary>
        public double HomeRecoveryPerHour { get; set; }
        /// <summary>병원 병상에 든 환자의 시간당 회복 비율(집보다 훨씬 빠르게).</summary>
        public double HospitalRecoveryPerHour { get; set; }
        /// <summary>새 전염병이 시작될 때 남는 면역 비율(같은 정착지 안, 0~1).</summary>
        public double ImmunityCarryover { get; set; }
        /// <summary>아픈 주민 비율이 이 값 아래로 내려가면 전염병이 끝난다(남은 환자는 회복한 것으로 봄).</summary>
        public double EndBelowSickShare { get; set; }
    }

    // BuildingKinds(시설 종류 이름)는 2026-10-24 Core/Economy/FacilityCatalog.cs로 옮겼다(시설 정의를 한곳에).
}
