using System;
using System.Collections.Generic;
using System.Linq;
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
        /// <summary>건설·개량·철거 시간 배율(설정 confirmedRules.constructionHoursMultiplier, 2026-10-23 FL2). 1 = 원본 v0.8.</summary>
        public double ConstructionHoursMultiplier { get; set; } = 1;
        /// <summary>명령 순간 착공(설정 confirmedRules.startJobsOnCommand, 2026-10-26 JQ2). false = 다음 정각에 착공(원본 v0.8, 회귀 비교용).</summary>
        public bool StartJobsOnCommand { get; set; }

        /// <summary>
        /// 실제 공사 시간(시): 레벨표 시간 × <see cref="ConstructionHoursMultiplier"/>(반올림, 최소 1). 철거는 × <see cref="DemolishHoursRatio"/>(올림).
        /// 작업 큐와 화면(도구 막대·툴팁)이 같은 값을 쓰도록 한곳에 둔다(2026-10-24: 화면이 배율 전 시간을 보여 주던 문제).
        /// </summary>
        public int JobHours(double? tableHours, bool demolish = false) => demolish
            ? Math.Max(1, (int)Math.Ceiling((tableHours ?? 1) * DemolishHoursRatio * ConstructionHoursMultiplier))
            : Math.Max(1, (int)Math.Round((tableHours ?? 1) * ConstructionHoursMultiplier, MidpointRounding.AwayFromZero));
        // ── 장비·지역 모디파이어 효과 (캠페인이 <see cref="ApplyEffects"/>로 건다. 기본값 = 효과 없음) ──
        /// <summary>광물 생산 배율(풍부한 광맥·광업 강화).</summary>
        public double OreMultiplier { get; set; } = 1.0;
        /// <summary>우물 방출량 배율(우물 성능 향상·마른 대수층).</summary>
        public double WellSupplyMultiplier { get; set; } = 1.0;
        /// <summary>농장 식량 생산 배율.</summary>
        public double FoodOutputMultiplier { get; set; } = 1.0;
        /// <summary>채석장 석재 생산 배율.</summary>
        public double StoneOutputMultiplier { get; set; } = 1.0;
        /// <summary>건설·개량 석재 배율.</summary>
        public double StoneCostMultiplier { get; set; } = 1.0;
        /// <summary>생산 시설 효율(물 공급률 × 식량 계수)의 상한. 1 = 제한 없음.</summary>
        public double EfficiencyCap { get; set; } = 1.0;
        /// <summary>식량·석재 구매 단가 배율.</summary>
        public double PurchasePriceMultiplier { get; set; } = 1.0;
        /// <summary>
        /// 시장 규칙(2026-10-27 이슈 #18): 시장이 있어야 판매, 없으면 떠돌이 상인 구매(비쌈), 구매·판매 압력은 매시간 회복.
        /// null이면 원본 v0.8(어디서나 같은 값에 구매, 판매 없음, 회복 없음) — 회귀 비교용.
        /// </summary>
        public TradeSettings Trade { get; set; }
        /// <summary>
        /// 주택·노동력(결정 LB1~). null이면 원본 v0.8(시설이 식량을 먹고 식량 계수 F가 효율에 곱해짐, 주택 없음) — 회귀 비교용.
        /// 켜져 있으면 F는 1로 고정되고 효율 자리에 배치율(배치 ÷ 필요 인원)이 들어간다.
        /// </summary>
        public LaborSettings Labor { get; set; }
        /// <summary>유지비 배율.</summary>
        public double UpkeepMultiplier { get; set; } = 1.0;
        /// <summary>처음부터 면역인 주민 비율(방역 장비, 결정 EV5). 0~1.</summary>
        public double EpidemicImmuneShare { get; set; }
        /// <summary>재해 피해 배율(장비, 2026-10-23). 1 = 그대로.</summary>
        public double SandstormDamageMultiplier { get; set; } = 1.0;
        public double LocustDamageMultiplier { get; set; } = 1.0;
        public double BlightDamageMultiplier { get; set; } = 1.0;
        /// <summary>지금까지 건 효과를 합친 것(UI 표시용).</summary>
        public EconomyEffects AppliedEffects { get; private set; } = new EconomyEffects();
        /// <summary>
        /// 이 정착지에서 석재·공사 시간 없이 바로 완공 배치할 수 있는 시설 수(종류 → 개수).
        /// 농장주에게 매각한 다음 정착지에서 캠페인이 채운다(완공된 농장 1개로 시작).
        /// </summary>
        public Dictionary<string, int> FreeBuilds { get; } = new Dictionary<string, int>();
        /// <summary>개발 중 자산 가치 비율(설정 auction.developmentAssetRatio). 0이면 개발 중 자산을 평가하지 않는다.</summary>
        public double DevelopmentAssetRatio { get; set; }
        /// <summary>가동 숙성 시작 비율(설정 auctionSeed.maturityStartRatio). 1이면 숙성 없음.</summary>
        public double MaturityStartRatio { get; set; } = 1;
        /// <summary>가동 숙성이 100%가 되는 연속 가동 시간(완공 기준).</summary>
        public double MaturityFullHours { get; set; } = 48;
        /// <summary>석재 배율에 가동 중 채석장 능력을 쓸지(설정 auctionSeed.stoneMultiplierUsesQuarryCapacity).</summary>
        public bool StoneMultiplierUsesQuarryCapacity { get; set; }

        /// <summary>완공 후 hoursSinceBuilt 시간이 지난 생산 시설의 숙성 비율(0h = 시작 비율 → MaturityFullHours = 1).</summary>
        public double Maturity(double hoursSinceBuilt)
        {
            if (MaturityStartRatio >= 1 || MaturityFullHours <= 0) return 1;
            return Math.Min(1, MaturityStartRatio + (1 - MaturityStartRatio) * Math.Max(0, hoursSinceBuilt) / MaturityFullHours);
        }

        /// <summary>
        /// 시설 종류별로 지을 수 있는 지형. [확정 2026-10-22] 우물=수원, 광산=광맥, 채석장=암반, 농장·창고·경매장·병원·물탱크=사막, 장애물=불가.
        /// 수로는 변에 짓으므로 지형 무관. 값은 <see cref="FacilityCatalog"/>에서 만든다(2026-10-24 CL1). 장비·실험으로 바꿀 수 있게 사전으로 둔다.
        /// </summary>
        public Dictionary<string, TerrainType[]> AllowedTerrain { get; } =
            FacilityCatalog.All.Where(t => !t.OnEdge).ToDictionary(t => t.Kind, t => t.Terrain.ToArray());

        /// <summary>지을 수 없는 시설. 2026-10-23 로드맵 1-② 물탱크를 열어 지금은 비어 있다(전에는 물탱크, W7).</summary>
        public HashSet<string> DisabledKinds { get; } = new HashSet<string>();

        /// <summary>수동 폐쇄할 수 없는 시설. 2026-10-23 로드맵 1-③ 창고 폐쇄를 열어 지금은 비어 있다(전에는 창고).
        /// 창고는 재고가 폐쇄 뒤 저장 한도 안일 때만 닫을 수 있다(철거와 같은 조건, Docs/decision_log_auto.md U3).</summary>
        public HashSet<string> CannotClose { get; } = new HashSet<string>();

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
            var rules = new SettlementRules(config)
            {
                ConstructionSlots = c.ConstructionSlots,
                CancelRefundRatio = c.PostStartCancelStoneRefundRatio,
                ConstructionHoursMultiplier = c.ConstructionHoursMultiplier > 0 ? c.ConstructionHoursMultiplier : 1,
                StartJobsOnCommand = c.StartJobsOnCommand,
                Trade = config.Purchase?.Trade,
                Labor = config.Labor,
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
                DevelopmentAssetRatio = config.Auction?.DevelopmentAssetRatio ?? 0,
                MaturityStartRatio = config.AuctionSeed?.MaturityStartRatio ?? 1,
                MaturityFullHours = config.AuctionSeed?.MaturityFullHours ?? 48,
                StoneMultiplierUsesQuarryCapacity = config.AuctionSeed?.StoneMultiplierUsesQuarryCapacity ?? false,
                Water = WaterNetworkSettings.FromConfig(config),
            };
            // 주택은 닫지 않는다(결정 LB8): 닫으면 물이 끊겨 주민이 떠날 뿐이라 철거와 다르지 않다.
            rules.CannotClose.Add(BuildingKinds.House);
            return rules;
        }

        /// <summary>
        /// 원본 v0.8 경제(시뮬레이터의 이식본 <c>EconomySim</c>)와 같은 결과를 내는 규칙(결정 RG1). 회귀 비교 전용이고 게임에서는 쓰지 않는다:
        /// EditMode <c>SettlementTests</c>의 고정 건설 스크립트와 시뮬레이터 교차 검증이 쓴다. v0.8 뒤에 더한 규칙을 여기서 끈다 —
        /// 새 규칙을 넣으면 그 규칙을 끄는 값을 여기에 한 줄 더한다(빠뜨리면 회귀값 133h·540.05·1149.63 비교가 깨진다).
        /// </summary>
        public static SettlementRules ForV08Regression(EconomyConfig config)
        {
            SettlementRules r = FromConfig(config);
            r.StartingCash = 1000;                       // 원본 시작 자금(게임 값은 결정 FB1)
            r.MaturityStartRatio = 1;                    // 가동 숙성 없음(경매 직전 몰아짓기 방지 규칙)
            r.StoneMultiplierUsesQuarryCapacity = false; // 석재 배율에 채석장 능력을 쓰지 않음(채석장 철거 방지 규칙)
            r.ConstructionHoursMultiplier = 1;           // 원본 건설 시간(게임 값은 결정 FL2)
            r.StartJobsOnCommand = false;                // 다음 정각에 착공(게임 값은 결정 JQ2: 명령 순간)
            r.Trade = null;                              // 시장 없이 어디서나 같은 값에 구매, 판매 없음(게임 값은 결정 MK1~MK9)
            r.Labor = null;                              // 주택·노동력 없음: 시설 foodUse와 식량 계수 F(게임 값은 결정 LB1~)
            return r;
        }

        /// <summary>주택·노동력 규칙이 켜져 있는지(<see cref="Labor"/>).</summary>
        public bool LaborOn => Labor != null;

        /// <summary>건설 슬롯 수 × 슬롯당 기본 인부(공사 전용, 떠나지 않음). 노동력이 꺼져 있으면 0.</summary>
        public double BaseCrew => LaborOn ? ConstructionSlots * Labor.CrewPerConstructionSlot : 0;

        /// <summary>이 시설 종류가 운영에 사람을 쓰는지(레벨표에 <c>workers</c>가 있음). 노동력이 꺼져 있으면 false.</summary>
        public bool NeedsWorkers(string kind) => LaborOn && (Config.GetLevel(kind, 1)?.Workers ?? 0) > 0;

        /// <summary>작업 하나에 필요한 인원: 건설·개량은 목표 레벨, 철거는 L1의 <c>buildWorkers</c>. 노동력이 꺼져 있으면 0.</summary>
        public double JobWorkers(JobType type, string kind, int targetLevel) =>
            LaborOn ? Config.GetLevel(kind, type == JobType.Demolish ? 1 : targetLevel)?.BuildWorkers ?? 0 : 0;

        /// <summary>
        /// 장비·지역 모디파이어 효과를 규칙 값에 건다(여러 번 부르면 누적). 정착지를 만들기 전에 호출한다.
        /// 수로 손실은 물 배분 설정에, 작업 슬롯·저장 한도는 해당 값에 더한다.
        /// </summary>
        public void ApplyEffects(EconomyEffects e)
        {
            if (e == null) return;
            OreMultiplier *= e.OreOutputMultiplier;
            WellSupplyMultiplier *= e.WellSupplyMultiplier;
            FoodOutputMultiplier *= e.FoodOutputMultiplier;
            StoneOutputMultiplier *= e.StoneOutputMultiplier;
            StoneCostMultiplier *= e.StoneCostMultiplier;
            PurchasePriceMultiplier *= e.PurchasePriceMultiplier;
            UpkeepMultiplier *= e.UpkeepMultiplier;
            ConstructionSlots += e.ExtraConstructionSlots;
            BaseFoodStorage += e.ExtraStorage;
            BaseStoneStorage += e.ExtraStorage;
            EpidemicImmuneShare = Math.Min(1, EpidemicImmuneShare + e.EpidemicImmuneShare);
            SandstormDamageMultiplier *= e.SandstormDamageMultiplier;
            LocustDamageMultiplier *= e.LocustDamageMultiplier;
            BlightDamageMultiplier *= e.BlightDamageMultiplier;
            AppliedEffects = EconomyEffects.Combine(new[] { AppliedEffects, e });
            EfficiencyCap = AppliedEffects.EffectiveEfficiencyCap;
            if (Math.Abs(e.CanalLossMultiplier - 1) > 1e-12)
            {
                WaterNetworkSettings old = Water;
                Water = new WaterNetworkSettings
                {
                    LossPerWholeEdge = Math.Min(0.95, old.LossPerWholeEdge * e.CanalLossMultiplier),
                    CapacityOf = old.CapacityOf,
                };
            }
        }

        public static bool IsEdgeKind(string kind) => kind == BuildingKinds.CanalEdge;

        /// <summary>이 시설 종류가 가질 수 있는 최고 레벨(설정 데이터와 MVP 상한 중 작은 값).</summary>
        public int MaxLevelOf(string kind) => Math.Min(MaxFacilityLevel, Config.MaxLevel(kind));

        public BuildingLevel Level(string kind, int level) =>
            Config.GetLevel(kind, level) ?? throw new ArgumentException($"설정에 {kind} L{level}이 없습니다.");

        // ── 생산(결정 PR1): 생산 시설 목록과 생산 공식은 여기 하나다. 정산·예상치·경매 가치·재해가 모두 이것을 쓴다. ──

        /// <summary>
        /// 시설 종류가 만드는 자원: 식량 재화(경작지·과수원·목축지·제분소)는 Food, 기호품·향신료(양조장·포도주 공방·허브 농장)는 Goods,
        /// 채석장 석재·광산 광물. 생산 시설이 아니면 None. 어떤 재화인지는 <see cref="GoodsOf"/>.
        /// </summary>
        public static ResourceKind ResourceOf(string kind)
        {
            switch (kind)
            {
                case BuildingKinds.Quarry: return ResourceKind.Stone;
                case BuildingKinds.Mine: return ResourceKind.Ore;
            }
            Goods? g = GoodsOf(kind);
            if (g == null) return ResourceKind.None;
            return GoodsCatalog.IsFood(g.Value) ? ResourceKind.Food : ResourceKind.Goods;
        }

        /// <summary>시설 종류가 만드는 재화(결정 FD2). 재화 시설이 아니면 null.</summary>
        public static Goods? GoodsOf(string kind)
        {
            switch (kind)
            {
                case BuildingKinds.Farm: return Goods.Grain;
                case BuildingKinds.Orchard: return Goods.Fruit;
                case BuildingKinds.Pasture: return Goods.Meat;
                case BuildingKinds.Mill: return Goods.Bread;
                case BuildingKinds.Brewery: return Goods.Beer;
                case BuildingKinds.Winery: return Goods.Wine;
                case BuildingKinds.HerbFarm: return Goods.Spice;
                default: return null;
            }
        }

        /// <summary>가공 시설의 원료(목축지·제분소·양조장 = 곡물, 포도주 공방 = 과일). 원료가 없는 시설이면 null.</summary>
        public static Goods? InputOf(string kind)
        {
            switch (kind)
            {
                case BuildingKinds.Pasture:
                case BuildingKinds.Mill:
                case BuildingKinds.Brewery: return Goods.Grain;
                case BuildingKinds.Winery: return Goods.Fruit;
                default: return null;
            }
        }

        /// <summary>메뚜기떼·작물 탄저병 생산 피해를 받는 작물 시설(결정 FD8): 경작지·과수원·허브 농장.</summary>
        public static bool IsCrop(string kind) => kind == BuildingKinds.Farm || kind == BuildingKinds.Orchard || kind == BuildingKinds.HerbFarm;

        /// <summary>
        /// 물·일손 100%일 때 시간당 생산량 = 레벨표 생산량 × 자원별 배율(장비·지역 효과 포함). 생산 시설이 아니면 0.
        /// 경작지는 <c>foodProduction</c>(곡물), 나머지 재화 시설은 <c>goodsProduction</c>. 작물 배율(관개 장비 등)은 경작지·과수원에 걸린다.
        /// </summary>
        public double MaxOutput(string kind, BuildingLevel lv)
        {
            switch (kind)
            {
                case BuildingKinds.Farm: return (lv.FoodProduction ?? 0) * FoodOutputMultiplier;
                case BuildingKinds.Orchard: return (lv.GoodsProduction ?? 0) * FoodOutputMultiplier;
                case BuildingKinds.Quarry: return (lv.StoneProduction ?? 0) * StoneOutputMultiplier;
                case BuildingKinds.Mine: return (lv.OreProduction ?? 0) * OreMultiplier;
            }
            return GoodsOf(kind) != null ? lv.GoodsProduction ?? 0 : 0;
        }

        /// <summary>재화 1단위가 채우는 식량(설정 <c>goods.*.foodValue</c>, 곡물 = 1). 식량이 아니면 0.</summary>
        public double FoodValue(Goods g) => GoodsCatalog.IsFood(g) ? GoodsCatalog.Settings(Config, g).FoodValue : 0;

        /// <summary>
        /// 경매 식량 배율이 보는 순 식량 가치(결정 FD7): 시간당 생산 × 그 재화 식량 효율 − 쓰는 원료 × 원료 식량 효율.
        /// 기호품 시설(양조장·포도주 공방)은 원료만 빼서 음수가 될 수 있다.
        /// </summary>
        public double NetFoodValue(string kind, double output, BuildingLevel lv)
        {
            Goods? g = GoodsOf(kind);
            if (g == null) return 0;
            double value = output * FoodValue(g.Value);
            Goods? input = InputOf(kind);
            if (input != null) value -= output * (lv.InputPerOutput ?? 0) * FoodValue(input.Value);
            return value;
        }

        /// <summary>
        /// 생산 효율 = 물 공급률 × 일손, 지역·장비의 효율 상한(<see cref="EfficiencyCap"/>)을 넘지 않는다.
        /// 일손은 노동력이 켜져 있으면 시설의 배치율(배치 ÷ 필요 인원), 꺼져 있으면(원본 v0.8) 식량 계수 F.
        /// </summary>
        public double Efficiency(double waterRate, double workforce) => Math.Min(EfficiencyCap, waterRate * workforce);
    }
}
