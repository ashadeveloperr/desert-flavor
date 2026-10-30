using System;
using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Water;

namespace DesertSettlement.Core.Economy
{
    /// <summary>
    /// 생활 수준(−100~100, 결정 LS1~LS7)과 향신료(결정 SP1~SP3). 노동력이나 생활 수준 설정이 없으면 0에 머문다.
    /// - 매시간 먹고 마신 재화로 목표를 정한다: 등급 점수(<c>goods.*.standardPoints</c>)를 먹은 양으로 평균 × 상위 재화 종류 수 배율 × 향신료.
    ///   상위 재화를 먹은 만큼 향신료가 함께 들고, 채운 비율만큼 목표에 (1 + spiceBonus)까지 곱한다.
    /// - 식량 충족률·주택 물 공급률 중 낮은 쪽이 모자라면 목표는 음수(상위 재화 보너스 없음). 시작 유예 중에는 물 부족을 세지 않는다(결정 BQ3).
    ///   아무 일 없으면 목표 0.
    /// - 지금 값은 목표를 향해 시간당 risePerHour(오를 때)·fallPerHour(내릴 때)씩 움직인다.
    /// - 효과: 이주 속도(<see cref="LeaveFactor"/>·<see cref="ReturnFactor"/>), 생산 효율(<see cref="LivingProductivity"/>), 경매 가치(입찰자 livingStandardWeight).
    /// 정착지 전체 설명은 Settlement.cs.
    /// </summary>
    public sealed partial class Settlement
    {
        /// <summary>지금 생활 수준(−100~100).</summary>
        public double LivingStandard { get; private set; }

        /// <summary>지난 정산에서 계산한 목표 생활 수준.</summary>
        public double LivingTarget { get; private set; }

        /// <summary>목표를 정한 근거(화면 표시): 등급 점수 평균·상위 재화 종류 수·종류 배율·향신료 충족률·부족 충족률.</summary>
        public LivingBreakdown LastLiving { get; private set; } = new LivingBreakdown();

        private LivingSettings Living => Rules.LaborOn ? Rules.Config.Living : null;

        /// <summary>생활 수준 비율(−1~1).</summary>
        private double LivingShare => Math.Max(-1, Math.Min(1, LivingStandard / 100.0));

        /// <summary>떠나는 속도 배율: +100이면 leaveFactorAtMax(0.5), −100이면 그 역수(2), 사이는 선형.</summary>
        public double LeaveFactor
        {
            get
            {
                LivingSettings l = Living;
                if (l == null || l.LeaveFactorAtMax <= 0) return 1;
                double s = LivingShare;
                return s >= 0 ? 1 + (l.LeaveFactorAtMax - 1) * s : 1 + (1 / l.LeaveFactorAtMax - 1) * -s;
            }
        }

        /// <summary>돌아오는 속도 배율: 떠나는 속도와 반대(+100이면 2, −100이면 0.5).</summary>
        public double ReturnFactor
        {
            get
            {
                LivingSettings l = Living;
                if (l == null || l.LeaveFactorAtMax <= 0) return 1;
                double s = LivingShare;
                return s >= 0 ? 1 + (1 / l.LeaveFactorAtMax - 1) * s : 1 + (l.LeaveFactorAtMax - 1) * -s;
            }
        }

        /// <summary>생산 효율 배율: 1 + productionAtMax × 생활 수준 ÷ 100(±productionAtMax).</summary>
        public double LivingProductivity => Living == null ? 1 : 1 + Living.ProductionAtMax * LivingShare;

        /// <summary>주민이 느끼는 주택 물 공급률의 주민 가중 평균(주민이 없으면 1, 시작 유예 중이면 1).</summary>
        private double ResidentWaterRate(WaterAllocation water)
        {
            double people = 0, served = 0;
            foreach (Facility h in _facilities.Values.Where(f => IsHouse(f) && HasResidents(f)))
            {
                double rate = FeltWaterRate(h, water);
                people += h.Residents;
                served += h.Residents * rate;
            }
            return people > 1e-9 ? served / people : 1;
        }

        /// <summary>
        /// 정산 끝(먹고 마신 뒤)에 향신료를 쓰고 목표를 정해 생활 수준을 움직인다. consumed = 재화별 먹고 마신 몫(이번 시간).
        /// </summary>
        private void UpdateLiving(double[] consumed, double foodRatio, WaterAllocation water)
        {
            LivingSettings l = Living;
            if (l == null) return;
            var b = new LivingBreakdown { Supply = Math.Min(foodRatio, ResidentWaterRate(water)) };
            double total = 0, points = 0, upper = 0;
            foreach (Goods g in GoodsCatalog.All)
            {
                double eaten = consumed[(int)g];
                if (eaten <= 1e-12) continue;
                double p = SettingsOf(g).StandardPoints;
                total += eaten;
                points += eaten * p;
                if (p > 0) upper += eaten;
            }
            b.AveragePoints = total > 1e-12 ? points / total : 0;
            b.UpperKinds = GoodsCatalog.All.Count(g => SettingsOf(g).StandardPoints > 0 && total > 1e-12 && consumed[(int)g] / total >= l.MinShareToCount);

            // 향신료: 상위 재화를 먹은 만큼 함께 든다(결정 SP2). 모자라면 있는 만큼, 채운 비율이 보너스.
            double spiceNeed = upper * l.SpicePerUpperGoods;
            double spiceUsed = Math.Min(spiceNeed, Stock(Goods.Spice));
            AddGoods(Goods.Spice, -spiceUsed);
            consumed[(int)Goods.Spice] += spiceUsed;
            b.SpiceCoverage = spiceNeed > 1e-12 ? spiceUsed / spiceNeed : 0;

            List<double> m = l.DiversityMultipliers;
            b.DiversityMultiplier = b.UpperKinds <= 0 || m == null || m.Count == 0 ? 0 : m[Math.Min(b.UpperKinds, m.Count) - 1];
            if (b.Supply < 1 - 1e-9)
            {
                double span = Math.Max(1e-9, 1 - l.ShortageFloor);
                LivingTarget = -100 * Math.Min(1, (1 - b.Supply) / span);
            }
            else
            {
                LivingTarget = Math.Min(100, b.AveragePoints * b.DiversityMultiplier * (1 + l.SpiceBonus * b.SpiceCoverage));
            }
            LastLiving = b;
            if (LivingStandard < LivingTarget) LivingStandard = Math.Min(LivingTarget, LivingStandard + l.RisePerHour);
            else LivingStandard = Math.Max(LivingTarget, LivingStandard - l.FallPerHour);
        }
    }

    /// <summary>생활 수준 목표를 정한 근거(화면 표시용).</summary>
    public sealed class LivingBreakdown
    {
        /// <summary>먹고 마신 재화의 등급 점수 평균(0~100).</summary>
        public double AveragePoints;
        /// <summary>이번 시간 먹은 상위 재화 종류 수와 그 배율.</summary>
        public int UpperKinds;
        public double DiversityMultiplier;
        /// <summary>향신료 충족률(0~1).</summary>
        public double SpiceCoverage;
        /// <summary>식량 충족률·주택 물 공급률 중 낮은 쪽(1 = 부족 없음).</summary>
        public double Supply = 1;
    }
}
