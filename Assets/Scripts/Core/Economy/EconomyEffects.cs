using System;
using System.Collections.Generic;
using System.Globalization;

namespace DesertSettlement.Core.Economy
{
    /// <summary>
    /// 정착지 경제에 거는 효과 묶음. 입찰자 모디파이어(결정 BM1~)와 지역 모디파이어(약점·강점)가 같은 형식을 쓴다.
    /// JSON에서는 바꾸고 싶은 항목만 적는다(빠진 항목은 '효과 없음' 기본값).
    /// 여러 효과는 <see cref="Combine"/>으로 합친다: 배율은 곱하고(인접 보너스·생활 수준 등 다른 배율과도 곱한다 — 결정 BM3,
    /// 합연산으로 비용이 0에 가까워지지 않게), 더하는 값은 더하고, 효율 상한은 가장 낮은 값.
    /// 속성을 더하면 <see cref="Combine"/>·<see cref="Scaled"/>·<see cref="Describe"/>와 SettlementRules.ApplyEffects에도 넣는다
    /// (빠뜨리면 ModifierEquipmentTests.Effects_EveryPropertySurvivesCombineScaledDescribeAndApply가 실패). 예시 값은 설정 JSON의 bidderModifiers와 절차 맵 설정의 지역 모디파이어.
    /// </summary>
    public sealed class EconomyEffects
    {
        /// <summary>건설·개량에 드는 석재 배율(무른 지반, 건설업자).</summary>
        public double StoneCostMultiplier { get; set; } = 1;
        /// <summary>생산 시설 효율(물 공급률 × 일손)의 상한. 1 = 제한 없음(고지대 약점이 낮춤).</summary>
        public double EfficiencyCap { get; set; } = 1;
        /// <summary>효율 상한에 더하는 값(최대 1). 지금 설정에서는 쓰지 않는다.</summary>
        public double EfficiencyCapBonus { get; set; }
        /// <summary>효율 상한에 곱하는 배율(최대 1, 범용 모디파이어).</summary>
        public double EfficiencyCapMultiplier { get; set; } = 1;
        /// <summary>우물 방출량 배율(마른 대수층, 수자원 사업자).</summary>
        public double WellSupplyMultiplier { get; set; } = 1;
        /// <summary>수로 거리 손실 배율(모래바람, 수자원 사업자).</summary>
        public double CanalLossMultiplier { get; set; } = 1;
        /// <summary>경작지·과수원 생산 배율(염분 토양, 비옥한 오아시스).</summary>
        public double FoodOutputMultiplier { get; set; } = 1;
        /// <summary>경작지(곡물) 생산 배율.</summary>
        public double GrainOutputMultiplier { get; set; } = 1;
        /// <summary>과수원·목축지·향신료 농장 생산 배율.</summary>
        public double FarmGoodsOutputMultiplier { get; set; } = 1;
        /// <summary>채석장 석재 생산 배율.</summary>
        public double StoneOutputMultiplier { get; set; } = 1;
        /// <summary>광산 광물 생산 배율.</summary>
        public double OreOutputMultiplier { get; set; } = 1;
        /// <summary>가공 시설(제분소·양조장·포도주 공방) 생산 배율.</summary>
        public double ProcessingOutputMultiplier { get; set; } = 1;
        /// <summary>가공에 드는 원료 배율(목축지 포함, 연계 보너스와 곱).</summary>
        public double ProcessingInputMultiplier { get; set; } = 1;
        /// <summary>모든 생산 시설의 생산 배율.</summary>
        public double OutputMultiplier { get; set; } = 1;
        /// <summary>농업 시설(경작지·과수원·목축지·향신료 농장) 물 수요 배율.</summary>
        public double FarmWaterDemandMultiplier { get; set; } = 1;
        /// <summary>식량·석재 외부 구매 단가 배율.</summary>
        public double PurchasePriceMultiplier { get; set; } = 1;
        /// <summary>시장 판매가 배율(손 판매·자동 판매·매각 때 재고 매도).</summary>
        public double SellPriceMultiplier { get; set; } = 1;
        /// <summary>시설 유지비 배율.</summary>
        public double UpkeepMultiplier { get; set; } = 1;
        /// <summary>건설·개량·철거 시간 배율.</summary>
        public double ConstructionTimeMultiplier { get; set; } = 1;
        /// <summary>동시 작업 슬롯 추가 수.</summary>
        public int ExtraConstructionSlots { get; set; }
        /// <summary>기본 식량·석재 저장 한도에 더하는 값. 지금 설정에서는 쓰지 않는다.</summary>
        public double ExtraStorage { get; set; }
        /// <summary>식품 창고·석재 저장 한도 전체 배율.</summary>
        public double StorageMultiplier { get; set; } = 1;
        /// <summary>창고가 늘려 주는 한도 배율.</summary>
        public double WarehouseStorageMultiplier { get; set; } = 1;
        /// <summary>물탱크 용량 배율.</summary>
        public double WaterTankCapacityMultiplier { get; set; } = 1;
        /// <summary>식품 부패율 배율(창고 밀집과 곱).</summary>
        public double SpoilageMultiplier { get; set; } = 1;
        /// <summary>주민 유입 속도 배율(생활 수준·주택 밀집과 곱).</summary>
        public double ArrivalMultiplier { get; set; } = 1;
        /// <summary>주민(·기본 인부) 1인당 식량 배율.</summary>
        public double FoodPerResidentMultiplier { get; set; } = 1;
        /// <summary>처음부터 면역인 주민 비율(결정 EV5). 지금 설정에서는 쓰지 않는다.</summary>
        public double EpidemicImmuneShare { get; set; }
        /// <summary>전염병 전파율 배율.</summary>
        public double EpidemicTransmissionMultiplier { get; set; } = 1;
        /// <summary>재해 피해 배율(결정 D3·D5·D6): 1 = 그대로, 0.7 = 피해 70%.</summary>
        public double SandstormDamageMultiplier { get; set; } = 1;
        public double LocustDamageMultiplier { get; set; } = 1;
        public double BlightDamageMultiplier { get; set; } = 1;
        /// <summary>정착지에 들어갈 때 가져가는 자금 배율(캠페인이 쓴다, 범용 모디파이어).</summary>
        public double StartingCashMultiplier { get; set; } = 1;

        public static EconomyEffects Combine(IEnumerable<EconomyEffects> effects)
        {
            var r = new EconomyEffects();
            if (effects == null) return r;
            foreach (EconomyEffects e in effects)
            {
                if (e == null) continue;
                r.StoneCostMultiplier *= e.StoneCostMultiplier;
                r.EfficiencyCap = Math.Min(r.EfficiencyCap, e.EfficiencyCap);
                r.EfficiencyCapBonus += e.EfficiencyCapBonus;
                r.EfficiencyCapMultiplier *= e.EfficiencyCapMultiplier;
                r.WellSupplyMultiplier *= e.WellSupplyMultiplier;
                r.CanalLossMultiplier *= e.CanalLossMultiplier;
                r.FoodOutputMultiplier *= e.FoodOutputMultiplier;
                r.GrainOutputMultiplier *= e.GrainOutputMultiplier;
                r.FarmGoodsOutputMultiplier *= e.FarmGoodsOutputMultiplier;
                r.StoneOutputMultiplier *= e.StoneOutputMultiplier;
                r.OreOutputMultiplier *= e.OreOutputMultiplier;
                r.ProcessingOutputMultiplier *= e.ProcessingOutputMultiplier;
                r.ProcessingInputMultiplier *= e.ProcessingInputMultiplier;
                r.OutputMultiplier *= e.OutputMultiplier;
                r.FarmWaterDemandMultiplier *= e.FarmWaterDemandMultiplier;
                r.PurchasePriceMultiplier *= e.PurchasePriceMultiplier;
                r.SellPriceMultiplier *= e.SellPriceMultiplier;
                r.UpkeepMultiplier *= e.UpkeepMultiplier;
                r.ConstructionTimeMultiplier *= e.ConstructionTimeMultiplier;
                r.ExtraConstructionSlots += e.ExtraConstructionSlots;
                r.ExtraStorage += e.ExtraStorage;
                r.StorageMultiplier *= e.StorageMultiplier;
                r.WarehouseStorageMultiplier *= e.WarehouseStorageMultiplier;
                r.WaterTankCapacityMultiplier *= e.WaterTankCapacityMultiplier;
                r.SpoilageMultiplier *= e.SpoilageMultiplier;
                r.ArrivalMultiplier *= e.ArrivalMultiplier;
                r.FoodPerResidentMultiplier *= e.FoodPerResidentMultiplier;
                r.EpidemicImmuneShare += e.EpidemicImmuneShare;
                r.EpidemicTransmissionMultiplier *= e.EpidemicTransmissionMultiplier;
                r.SandstormDamageMultiplier *= e.SandstormDamageMultiplier;
                r.LocustDamageMultiplier *= e.LocustDamageMultiplier;
                r.BlightDamageMultiplier *= e.BlightDamageMultiplier;
                r.StartingCashMultiplier *= e.StartingCashMultiplier;
            }
            return r;
        }

        /// <summary>
        /// 효과 세기를 s배로 바꾼 사본(후반 약점 강화 <see cref="ScaledWeakness"/>가 쓴다). 배율은 1에서 벗어난 만큼을 s배(1.1 → s=2면 1.2),
        /// 더하는 값은 s배. 효율 상한(약점 전용)과 동시 작업 슬롯(정수)은 바꾸지 않는다.
        /// </summary>
        public EconomyEffects Scaled(double s)
        {
            double M(double m) => 1 + (m - 1) * s;
            return new EconomyEffects
            {
                StoneCostMultiplier = M(StoneCostMultiplier), EfficiencyCap = EfficiencyCap, EfficiencyCapBonus = EfficiencyCapBonus * s,
                EfficiencyCapMultiplier = M(EfficiencyCapMultiplier),
                WellSupplyMultiplier = M(WellSupplyMultiplier), CanalLossMultiplier = M(CanalLossMultiplier),
                FoodOutputMultiplier = M(FoodOutputMultiplier), GrainOutputMultiplier = M(GrainOutputMultiplier), FarmGoodsOutputMultiplier = M(FarmGoodsOutputMultiplier),
                StoneOutputMultiplier = M(StoneOutputMultiplier), OreOutputMultiplier = M(OreOutputMultiplier),
                ProcessingOutputMultiplier = M(ProcessingOutputMultiplier), ProcessingInputMultiplier = M(ProcessingInputMultiplier), OutputMultiplier = M(OutputMultiplier),
                FarmWaterDemandMultiplier = M(FarmWaterDemandMultiplier),
                PurchasePriceMultiplier = M(PurchasePriceMultiplier), SellPriceMultiplier = M(SellPriceMultiplier), UpkeepMultiplier = M(UpkeepMultiplier),
                ConstructionTimeMultiplier = M(ConstructionTimeMultiplier),
                ExtraConstructionSlots = ExtraConstructionSlots, ExtraStorage = ExtraStorage * s,
                StorageMultiplier = M(StorageMultiplier), WarehouseStorageMultiplier = M(WarehouseStorageMultiplier), WaterTankCapacityMultiplier = M(WaterTankCapacityMultiplier),
                SpoilageMultiplier = M(SpoilageMultiplier), ArrivalMultiplier = M(ArrivalMultiplier), FoodPerResidentMultiplier = M(FoodPerResidentMultiplier),
                EpidemicImmuneShare = EpidemicImmuneShare * s, EpidemicTransmissionMultiplier = M(EpidemicTransmissionMultiplier),
                SandstormDamageMultiplier = M(SandstormDamageMultiplier), LocustDamageMultiplier = M(LocustDamageMultiplier), BlightDamageMultiplier = M(BlightDamageMultiplier),
                StartingCashMultiplier = M(StartingCashMultiplier),
            };
        }

        /// <summary>
        /// 약점 세기를 s배로(후반 가속, 결정 LD2). <see cref="Scaled"/>와 같고, 효율 상한도 1에서 모자란 만큼을 s배(0.9 → s=2면 0.8).
        /// 줄어드는 배율(우물·생산·효율 상한)은 <see cref="WeaknessFloor"/>(0.3) 아래로 내려가지 않는다(예: 염분 토양 농장 0.75가 세져도 생산 0이 되지 않게).
        /// </summary>
        public EconomyEffects ScaledWeakness(double s)
        {
            EconomyEffects r = Scaled(s);
            double F(double scaled, double original) => original < 1 ? Math.Max(WeaknessFloor, scaled) : scaled;
            r.EfficiencyCap = F(1 - (1 - EfficiencyCap) * s, EfficiencyCap);
            r.WellSupplyMultiplier = F(r.WellSupplyMultiplier, WellSupplyMultiplier);
            r.FoodOutputMultiplier = F(r.FoodOutputMultiplier, FoodOutputMultiplier);
            r.StoneOutputMultiplier = F(r.StoneOutputMultiplier, StoneOutputMultiplier);
            r.OreOutputMultiplier = F(r.OreOutputMultiplier, OreOutputMultiplier);
            return r;
        }

        /// <summary>세진 약점의 줄어드는 배율 하한(LD2).</summary>
        public const double WeaknessFloor = 0.3;

        /// <summary>최종 효율 상한((상한 + 보너스) × 배율, 최대 1).</summary>
        public double EffectiveEfficiencyCap => Math.Min(1, (EfficiencyCap + EfficiencyCapBonus) * EfficiencyCapMultiplier);

        /// <summary>UI용 한국어 요약(효과 없는 항목은 생략). 예: "건설 석재 +25%, 우물 방출 −20%".</summary>
        public string Describe()
        {
            var parts = new List<string>();
            void Ratio(string name, double m)
            {
                if (Math.Abs(m - 1) < 1e-9) return;
                double pct = (m - 1) * 100;
                parts.Add(string.Format(CultureInfo.InvariantCulture, "{0} {1}{2:0.#}%", name, pct > 0 ? "+" : "−", Math.Abs(pct)));
            }
            Ratio("건설 석재", StoneCostMultiplier);
            if (EfficiencyCap < 1 - 1e-9) parts.Add(string.Format(CultureInfo.InvariantCulture, "생산 효율 최대 {0:0}%", EfficiencyCap * 100));
            if (EfficiencyCapBonus > 1e-9) parts.Add(string.Format(CultureInfo.InvariantCulture, "효율 상한 +{0:0}%p", EfficiencyCapBonus * 100));
            Ratio("효율 상한", EfficiencyCapMultiplier);
            Ratio("우물 방출", WellSupplyMultiplier);
            Ratio("수로 손실", CanalLossMultiplier);
            Ratio("농장 생산", FoodOutputMultiplier);
            Ratio("곡물 생산", GrainOutputMultiplier);
            Ratio("과일·고기·향신료 생산", FarmGoodsOutputMultiplier);
            Ratio("채석장 생산", StoneOutputMultiplier);
            Ratio("광물 생산", OreOutputMultiplier);
            Ratio("가공 생산", ProcessingOutputMultiplier);
            Ratio("가공 원료", ProcessingInputMultiplier);
            Ratio("모든 생산", OutputMultiplier);
            Ratio("농업 물 수요", FarmWaterDemandMultiplier);
            Ratio("구매 단가", PurchasePriceMultiplier);
            Ratio("판매가", SellPriceMultiplier);
            Ratio("유지비", UpkeepMultiplier);
            Ratio("공사 시간", ConstructionTimeMultiplier);
            if (ExtraConstructionSlots != 0) parts.Add($"건설 슬롯 +{ExtraConstructionSlots}");
            if (ExtraStorage > 1e-9) parts.Add(string.Format(CultureInfo.InvariantCulture, "저장 한도 +{0:0}", ExtraStorage));
            Ratio("저장 한도", StorageMultiplier);
            Ratio("창고 한도", WarehouseStorageMultiplier);
            Ratio("물탱크 용량", WaterTankCapacityMultiplier);
            Ratio("식품 부패", SpoilageMultiplier);
            Ratio("주민 유입", ArrivalMultiplier);
            Ratio("1인당 식량", FoodPerResidentMultiplier);
            if (EpidemicImmuneShare > 1e-9) parts.Add(string.Format(CultureInfo.InvariantCulture, "전염병 면역 주민 {0:0}%", EpidemicImmuneShare * 100));
            Ratio("전염병 감염", EpidemicTransmissionMultiplier);
            Ratio("모래폭풍 피해", SandstormDamageMultiplier);
            Ratio("메뚜기떼 피해", LocustDamageMultiplier);
            Ratio("탄저병 피해", BlightDamageMultiplier);
            Ratio("시작 자금", StartingCashMultiplier);
            return parts.Count == 0 ? "효과 없음" : string.Join(", ", parts);
        }
    }
}
