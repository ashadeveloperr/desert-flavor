using System;
using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Hex;
using DesertSettlement.Core.Water;

namespace DesertSettlement.Core.Economy
{
    /// <summary>공사 중이거나 막 완공한 시설 하나의 개발 중 자산 기여(중립 가중치, 비율 적용 후).</summary>
    public readonly struct DevelopmentAsset
    {
        public readonly int FacilityId;
        public readonly string Kind;
        /// <summary>실적 구간에 더할 광물·식량·석재 양(시간 합계).</summary>
        public readonly double Ore, Food, Stone;
        /// <summary>인프라 배율에 더할 값(공사 중 우물·수로·창고·경매장).</summary>
        public readonly double Infra;

        public DevelopmentAsset(int id, string kind, double ore, double food, double stone, double infra)
        {
            FacilityId = id;
            Kind = kind;
            Ore = ore;
            Food = food;
            Stone = stone;
            Infra = infra;
        }
    }

    /// <summary>정착지 가치 계산 결과(한 입찰자 관점).</summary>
    public readonly struct ValuationResult
    {
        public readonly string BidderId;
        /// <summary>직전 24시간 광물 실적(가동 숙성 반영, 개발 중 자산 제외).</summary>
        public readonly double OreLast24h;
        /// <summary>직전 24시간 실제 광물 생산(숙성 반영 전).</summary>
        public readonly double OreRawLast24h;
        /// <summary>석재 배율이 24h 평균 대신 가동 중 채석장 능력으로 정해졌는지.</summary>
        public readonly bool StoneFromQuarryCapacity;
        /// <summary>개발 중 자산에서 온 광물 환산량(입찰자 가중치 적용).</summary>
        public readonly double DevelopmentOre;
        public readonly double FoodAveragePerHour;
        public readonly double StoneAveragePerHour;
        public readonly double FoodMultiplier;
        public readonly double StoneMultiplier;
        public readonly double InfraMultiplier;
        /// <summary>빈 땅 배율(부동산 개발업자, 2026-10-23 로드맵 1-⑥). 다른 입찰자는 1.</summary>
        public readonly double OpenLandMultiplier;
        public readonly double BaseValue;
        /// <summary>가치 V = 광물 기반 가치 × 식량·석재·인프라·빈 땅 배율.</summary>
        public readonly double Value;

        public ValuationResult(string bidderId, double ore, double devOre, double food, double stone,
            double mf, double ms, double mi, double baseValue, double oreRaw = double.NaN, bool stoneFromCapacity = false, double mo = 1)
        {
            BidderId = bidderId;
            OreLast24h = ore;
            OreRawLast24h = double.IsNaN(oreRaw) ? ore : oreRaw;
            StoneFromQuarryCapacity = stoneFromCapacity;
            DevelopmentOre = devOre;
            FoodAveragePerHour = food;
            StoneAveragePerHour = stone;
            FoodMultiplier = mf;
            StoneMultiplier = ms;
            InfraMultiplier = mi;
            OpenLandMultiplier = mo;
            BaseValue = baseValue;
            Value = baseValue * mf * ms * mi * mo;
        }

        /// <summary>입찰가 = V × 배율(시세 등).</summary>
        public double Bid(double multiplier) => Value * multiplier;
    }

    /// <summary>
    /// 경매 가치 평가.
    /// - 확정: 경매 직전 24게임시간의 실제 생산 실적. 식량·석재는 저장 한도로 폐기된 생산분도 포함(2026-10-22 확정).
    /// - 개발 중 자산(A3 확정): 공사 중인 시설은 '완공 가정 물 배분'의 기대 생산 × 24시간 × 진행률 × 비율,
    ///   24시간 안에 완공(또는 개량 완료)한 시설은 실적이 없던 시간 × 현재 기대 생산 × 비율을 더한다.
    ///   공사 중 우물·수로·창고·경매장은 인프라 보너스 × 진행률 × 비율.
    /// - 가동 숙성(2026-10-23, 경매 직전 몰아짓기 방지): 생산 시설의 실적은 완공 직후 maturityStartRatio(0.7)로 시작해
    ///   maturityFullHours(48h) 동안 100%까지 오른다(시간마다 HourRecord.*Valued로 기록). 개발 중 자산에도 시작 비율을 곱한다.
    /// - 석재 배율(2026-10-23, 다 지은 뒤 채석장 철거 방지): 24h 평균과 가동 중 채석장의 생산 능력 중 큰 값.
    /// - 입찰자 성향(A2): 광물 가중치, 배율 가중치(1을 넘는 부분에 곱함), 공사 중 시설 종류별 개발 가중치.
    /// 계수는 설정 auctionSeed / auction의 잠정값.
    /// </summary>
    public static class SettlementValuation
    {
        /// <summary>중립 기준 가치 V(입찰자 성향 없음).</summary>
        public static ValuationResult Evaluate(Settlement s, long auctionHour) => EvaluateFor(s, auctionHour, BidderProfile.Neutral);

        /// <summary>auctionHour가 시작되기 직전 24시간 기록 + 개발 중 자산으로 입찰자 관점 가치를 계산한다.</summary>
        public static ValuationResult EvaluateFor(Settlement s, long auctionHour, BidderProfile bidder)
        {
            AuctionSeed seed = s.Rules.Config.AuctionSeed;
            int window = s.Rules.Config.ConfirmedRules.AuctionHistoryGameHours;
            List<HourRecord> records = s.History.Where(r => r.Hour >= auctionHour - window && r.Hour < auctionHour).ToList();

            // 가동 숙성 반영 실적(막 완공한 시설은 덜 쳐준다). 숙성이 꺼져 있으면 실제 생산과 같다.
            double oreRaw = records.Sum(r => r.OreProduced);
            double ore = records.Sum(r => r.OreValued);
            double foodSum = records.Sum(r => r.FoodValued);
            double stoneSum = records.Sum(r => r.StoneValued);

            double devOre = 0, devFood = 0, devStone = 0, devInfra = 0;
            foreach (DevelopmentAsset d in DevelopmentAssets(s, auctionHour))
            {
                double w = bidder.DevelopmentWeight(d.Kind);
                devOre += d.Ore * w;
                devFood += d.Food * w;
                devStone += d.Stone * w;
                devInfra += d.Infra * w;
            }

            double food = (foodSum + devFood) / window;
            double stone = (stoneSum + devStone) / window;
            // 다 지은 뒤 채석장을 철거하면 손해가 되도록: 석재 배율은 24h 평균과 '지금 가동 중인 채석장의 생산 능력' 중 큰 값.
            bool stoneFromCapacity = false;
            if (s.Rules.StoneMultiplierUsesQuarryCapacity)
            {
                double capacity = s.OperatingOf(BuildingKinds.Quarry).Sum(q => (s.LevelOf(q).StoneProduction ?? 0) * s.Rules.StoneOutputMultiplier);
                if (capacity > stone) { stone = capacity; stoneFromCapacity = true; }
            }
            double mfRaw = 1 + (seed.FoodMaxMultiplier - 1) * Math.Min(1, food / seed.FoodProductionAverageForFullBonus);
            double msRaw = 1 + (seed.StoneMaxMultiplier - 1) * Math.Min(1, stone / seed.StoneProductionAverageForFullBonus);

            // 인프라: 운영 중 시설 + 공사 중 인프라의 개발 중 자산. 미완공·폐쇄 시설 자체는 보너스 없음.
            InfraMultipliers infra = seed.Infra;
            double miRaw = 1
                + (s.OperatingOf(BuildingKinds.Well).Any() ? infra.Well : 0)
                + (s.OperatingOf(BuildingKinds.AuctionHall).Any() ? infra.AuctionHall : 0)
                + s.OperatingOf(BuildingKinds.CanalEdge).Count() * infra.PerOperationalCanal
                + s.OperatingOf(BuildingKinds.Warehouse).Count() * infra.PerOperationalWarehouse
                + s.OperatingOf(BuildingKinds.Hospital).Count() * infra.PerOperationalHospital
                + s.OperatingOf(BuildingKinds.WaterTank).Count() * infra.PerOperationalWaterTank
                + devInfra;
            miRaw = Math.Min(infra.Maximum, miRaw);

            double mf = 1 + (mfRaw - 1) * bidder.FoodMultiplierWeight;
            double ms = 1 + (msRaw - 1) * bidder.StoneMultiplierWeight;
            double mi = 1 + (miRaw - 1) * bidder.InfraWeight;
            double baseValue = seed.OreProduction24hValueFactor * (ore + devOre) * bidder.OreWeight;
            double mo = 1 + bidder.OpenLandWeight * (OpenLandShare(s) - seed.OpenLandShareCenter);
            return new ValuationResult(bidder.Id, ore, devOre, food, stone, mf, ms, mi, baseValue, oreRaw, stoneFromCapacity, Math.Max(0, mo));
        }

        /// <summary>
        /// 개발 가능한 빈 땅 비율 = 시설·예약이 없는 사막 타일 / 사막 타일 전체(0~1). 부동산 개발업자·건설업자가 본다
        /// (기획서 8.4 '개발 가능 부지', Docs/decision_log_auto.md B3).
        /// </summary>
        public static double OpenLandShare(Settlement s)
        {
            var desert = s.Map.Tiles.Where(t => t.Terrain == TerrainType.Desert).Select(t => t.Coord).ToList();
            if (desert.Count == 0) return 0;
            int used = desert.Count(c => s.FacilityAt(c) != null || s.Jobs.Any(j => j.Type == JobType.Build && !j.Edge.HasValue && j.Tile == c));
            return (desert.Count - used) / (double)desert.Count;
        }

        /// <summary>개발 중 자산 목록(중립 가중치). 비율이 0이면 비어 있다.</summary>
        public static List<DevelopmentAsset> DevelopmentAssets(Settlement s, long auctionHour)
        {
            var list = new List<DevelopmentAsset>();
            double ratio = s.Rules.DevelopmentAssetRatio;
            if (ratio <= 0) return list;

            int window = s.Rules.Config.ConfirmedRules.AuctionHistoryGameHours;
            long windowStart = auctionHour - window;
            InfraMultipliers infra = s.Rules.Config.AuctionSeed.Infra;
            WaterAllocation assumed = null;

            foreach (Facility f in s.Facilities)
            {
                ConstructionJob job = s.Jobs.FirstOrDefault(j => j.FacilityId == f.Id && j.State == JobState.InProgress && j.Type != JobType.Demolish);
                double progress;
                double missingHours;
                if (job != null)
                {
                    progress = job.Hours > 0 ? Math.Max(0, Math.Min(1, (auctionHour - job.StartedHour) / (double)job.Hours)) : 1;
                    missingHours = job.Type == JobType.Build ? window : Math.Min(window, Math.Max(0, auctionHour - job.StartedHour));
                }
                else if (f.IsOperating && f.ActiveSinceHour > windowStart)
                {
                    progress = 1;
                    missingHours = f.ActiveSinceHour - windowStart;
                }
                else
                {
                    continue;
                }

                if (job != null && job.Type == JobType.Build)
                {
                    double bonus = InfraBonus(f.Kind, infra);
                    if (bonus > 0) list.Add(new DevelopmentAsset(f.Id, f.Kind, 0, 0, 0, bonus * progress * ratio));
                }

                if (f.Kind != BuildingKinds.Farm && f.Kind != BuildingKinds.Quarry && f.Kind != BuildingKinds.Mine) continue;
                if (assumed == null) assumed = s.SolveWaterAssumingJobsComplete();

                BuildingLevel lv = s.Rules.Level(f.Kind, s.AssumedLevelOf(f));
                double rate = (lv.WaterDemand ?? 0) > 0 ? assumed.RateOf(f.Id.ToString()) : 1.0;
                double perHour = Math.Min(s.Rules.EfficiencyCap, rate * s.FoodCoefficient);
                // 개발 중 자산은 막 지은 시설이므로 숙성 시작 비율을 곱한다.
                double amount = perHour * missingHours * progress * ratio * s.Rules.MaturityStartRatio;
                switch (f.Kind)
                {
                    case BuildingKinds.Farm: list.Add(new DevelopmentAsset(f.Id, f.Kind, 0, amount * (lv.FoodProduction ?? 0) * s.Rules.FoodOutputMultiplier, 0, 0)); break;
                    case BuildingKinds.Quarry: list.Add(new DevelopmentAsset(f.Id, f.Kind, 0, 0, amount * (lv.StoneProduction ?? 0) * s.Rules.StoneOutputMultiplier, 0)); break;
                    case BuildingKinds.Mine: list.Add(new DevelopmentAsset(f.Id, f.Kind, amount * (lv.OreProduction ?? 0) * s.Rules.OreMultiplier, 0, 0, 0)); break;
                }
            }
            return list;
        }

        private static double InfraBonus(string kind, InfraMultipliers infra)
        {
            switch (kind)
            {
                case BuildingKinds.Well: return infra.Well;
                case BuildingKinds.AuctionHall: return infra.AuctionHall;
                case BuildingKinds.CanalEdge: return infra.PerOperationalCanal;
                case BuildingKinds.Warehouse: return infra.PerOperationalWarehouse;
                case BuildingKinds.Hospital: return infra.PerOperationalHospital;
                default: return 0;
            }
        }
    }
}
