using System;
using System.Linq;
using DesertSettlement.Core.Config;

namespace DesertSettlement.Core.Economy
{
    /// <summary>정착지 기준 가치 계산 결과.</summary>
    public readonly struct ValuationResult
    {
        public readonly double OreLast24h;
        public readonly double FoodAveragePerHour;
        public readonly double StoneAveragePerHour;
        public readonly double FoodMultiplier;
        public readonly double StoneMultiplier;
        public readonly double InfraMultiplier;
        public readonly double BaseValue;
        /// <summary>기준 가치 V = 광물 기반 가치 × 식량·석재·인프라 배율.</summary>
        public readonly double Value;

        public ValuationResult(double ore, double food, double stone, double mf, double ms, double mi, double baseValue)
        {
            OreLast24h = ore;
            FoodAveragePerHour = food;
            StoneAveragePerHour = stone;
            FoodMultiplier = mf;
            StoneMultiplier = ms;
            InfraMultiplier = mi;
            BaseValue = baseValue;
            Value = baseValue * mf * ms * mi;
        }

        /// <summary>입찰가 = V × 입찰 배율. 입찰자 시스템 전까지는 설정의 fixedTestBidMultiplier(1.05).</summary>
        public double Bid(double multiplier) => Value * multiplier;
    }

    /// <summary>
    /// 경매 기준 가치. 확정: 경매 직전 24게임시간의 실제 생산 실적(저장 한도로 폐기된 양 제외).
    /// 계수(광물 단가 4.5, 식량·석재 만점 기준, 인프라 보너스)는 설정 auctionSeed의 잠정값.
    /// 입찰자·경매 진행은 다음 단계(4)에서 구현한다.
    /// </summary>
    public static class SettlementValuation
    {
        /// <summary>auctionHour가 시작되기 직전 24시간 기록으로 가치를 계산한다. 기록이 24시간 미만이면 예외.</summary>
        public static ValuationResult Evaluate(Settlement s, long auctionHour)
        {
            AuctionSeed seed = s.Rules.Config.AuctionSeed;
            int window = s.Rules.Config.ConfirmedRules.AuctionHistoryGameHours;
            var records = s.History.Where(r => r.Hour >= auctionHour - window && r.Hour < auctionHour).ToList();
            if (records.Count < window) throw new InvalidOperationException($"직전 {window}시간 기록이 부족합니다({records.Count}).");

            double ore = records.Sum(r => r.OreProduced);
            double food = records.Sum(r => r.FoodValid) / window;
            double stone = records.Sum(r => r.StoneValid) / window;
            double mf = 1 + (seed.FoodMaxMultiplier - 1) * Math.Min(1, food / seed.FoodProductionAverageForFullBonus);
            double ms = 1 + (seed.StoneMaxMultiplier - 1) * Math.Min(1, stone / seed.StoneProductionAverageForFullBonus);

            // 인프라: 미완공·폐쇄 시설은 보너스 없음.
            InfraMultipliers infra = seed.Infra;
            double mi = 1
                + (s.OperatingOf(BuildingKinds.Well).Any() ? infra.Well : 0)
                + (s.OperatingOf(BuildingKinds.AuctionHall).Any() ? infra.AuctionHall : 0)
                + s.OperatingOf(BuildingKinds.CanalEdge).Count() * infra.PerOperationalCanal
                + s.OperatingOf(BuildingKinds.Warehouse).Count() * infra.PerOperationalWarehouse;
            mi = Math.Min(infra.Maximum, mi);

            return new ValuationResult(ore, food, stone, mf, ms, mi, seed.OreProduction24hValueFactor * ore);
        }
    }
}
