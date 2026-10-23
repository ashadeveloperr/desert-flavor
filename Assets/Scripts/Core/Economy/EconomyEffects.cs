using System;
using System.Collections.Generic;
using System.Globalization;

namespace DesertSettlement.Core.Economy
{
    /// <summary>
    /// 정착지 경제에 거는 효과 묶음. 영구 장비와 지역 모디파이어(약점·강점)가 같은 형식을 쓴다.
    /// JSON에서는 바꾸고 싶은 항목만 적는다(빠진 항목은 '효과 없음' 기본값).
    /// 여러 효과는 <see cref="Combine"/>으로 합친다: 배율은 곱하고, 더하는 값은 더하고, 효율 상한은 가장 낮은 값.
    /// </summary>
    public sealed class EconomyEffects
    {
        /// <summary>건설·개량에 드는 석재 배율(무른 지반 1.25, 보강 자재 0.85).</summary>
        public double StoneCostMultiplier { get; set; } = 1;
        /// <summary>생산 시설(농장·채석장·광산) 효율(물 공급률 × 식량 계수)의 상한. 1 = 제한 없음(고지대 0.9).</summary>
        public double EfficiencyCap { get; set; } = 1;
        /// <summary>효율 상한에 더하는 값(산소 공급기 +0.1). 상한은 1을 넘지 않는다.</summary>
        public double EfficiencyCapBonus { get; set; }
        /// <summary>우물 방출량 배율(우물 성능 향상 1.2, 마른 대수층 0.8).</summary>
        public double WellSupplyMultiplier { get; set; } = 1;
        /// <summary>수로 거리 손실 배율(모래바람 2, 수로 방풍막 0.6).</summary>
        public double CanalLossMultiplier { get; set; } = 1;
        /// <summary>농장 식량 생산 배율.</summary>
        public double FoodOutputMultiplier { get; set; } = 1;
        /// <summary>채석장 석재 생산 배율.</summary>
        public double StoneOutputMultiplier { get; set; } = 1;
        /// <summary>광산 광물 생산 배율.</summary>
        public double OreOutputMultiplier { get; set; } = 1;
        /// <summary>식량·석재 외부 구매 단가 배율.</summary>
        public double PurchasePriceMultiplier { get; set; } = 1;
        /// <summary>시설 유지비 배율.</summary>
        public double UpkeepMultiplier { get; set; } = 1;
        /// <summary>동시 작업 슬롯 추가 수(건설 슬롯 +1).</summary>
        public int ExtraConstructionSlots { get; set; }
        /// <summary>기본 식량·석재 저장 한도에 더하는 값(대형 창고 설비 +50).</summary>
        public double ExtraStorage { get; set; }
        /// <summary>정착지 시작 전염병 저항력(방역 장비 +0.3). 전염병이 끝나 저항력이 줄어도 이 값 아래로는 안 내려간다.</summary>
        public double InitialEpidemicResistance { get; set; }

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
                r.WellSupplyMultiplier *= e.WellSupplyMultiplier;
                r.CanalLossMultiplier *= e.CanalLossMultiplier;
                r.FoodOutputMultiplier *= e.FoodOutputMultiplier;
                r.StoneOutputMultiplier *= e.StoneOutputMultiplier;
                r.OreOutputMultiplier *= e.OreOutputMultiplier;
                r.PurchasePriceMultiplier *= e.PurchasePriceMultiplier;
                r.UpkeepMultiplier *= e.UpkeepMultiplier;
                r.ExtraConstructionSlots += e.ExtraConstructionSlots;
                r.ExtraStorage += e.ExtraStorage;
                r.InitialEpidemicResistance += e.InitialEpidemicResistance;
            }
            return r;
        }

        /// <summary>최종 효율 상한(상한 + 보너스, 최대 1).</summary>
        public double EffectiveEfficiencyCap => Math.Min(1, EfficiencyCap + EfficiencyCapBonus);

        /// <summary>UI용 한국어 요약(효과 없는 항목은 생략). 예: "건설 석재 +25%, 우물 방출 −20%".</summary>
        public string Describe()
        {
            var parts = new List<string>();
            void Ratio(string name, double m)
            {
                if (Math.Abs(m - 1) < 1e-9) return;
                double pct = (m - 1) * 100;
                parts.Add(string.Format(CultureInfo.InvariantCulture, "{0} {1}{2:0}%", name, pct > 0 ? "+" : "−", Math.Abs(pct)));
            }
            Ratio("건설 석재", StoneCostMultiplier);
            if (EfficiencyCap < 1 - 1e-9) parts.Add(string.Format(CultureInfo.InvariantCulture, "생산 효율 최대 {0:0}%", EfficiencyCap * 100));
            if (EfficiencyCapBonus > 1e-9) parts.Add(string.Format(CultureInfo.InvariantCulture, "효율 상한 +{0:0}%p", EfficiencyCapBonus * 100));
            Ratio("우물 방출", WellSupplyMultiplier);
            Ratio("수로 손실", CanalLossMultiplier);
            Ratio("농장 생산", FoodOutputMultiplier);
            Ratio("채석장 생산", StoneOutputMultiplier);
            Ratio("광물 생산", OreOutputMultiplier);
            Ratio("구매 단가", PurchasePriceMultiplier);
            Ratio("유지비", UpkeepMultiplier);
            if (ExtraConstructionSlots != 0) parts.Add($"동시 작업 +{ExtraConstructionSlots}");
            if (ExtraStorage > 1e-9) parts.Add(string.Format(CultureInfo.InvariantCulture, "저장 한도 +{0:0}", ExtraStorage));
            if (InitialEpidemicResistance > 1e-9) parts.Add(string.Format(CultureInfo.InvariantCulture, "시작 전염병 저항력 +{0:0}%", InitialEpidemicResistance * 100));
            return parts.Count == 0 ? "효과 없음" : string.Join(", ", parts);
        }
    }
}
