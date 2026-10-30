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
        /// <summary>인프라 배율에 더할 값(공사 중인 인프라 시설이 완공되면 늘어날 몫).</summary>
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
        /// <summary>빈 땅 배율(부동산 개발업자, 결정 BD3). 다른 입찰자는 1.</summary>
        public readonly double OpenLandMultiplier;
        /// <summary>광물 가치 + 주민 가치(배율 곱하기 전).</summary>
        public readonly double BaseValue;
        /// <summary>그중 주민 가치 = 사는 인원 × 1인 가치 × 입찰자 주민 가중치(결정 CL1).</summary>
        public readonly double PopulationValue;
        /// <summary>생활 수준 배율 = 1 + 입찰자 생활 수준 가중치 × 생활 수준 ÷ 100(결정 LS7). 대부분의 입찰자는 1.</summary>
        public readonly double LivingMultiplier;
        /// <summary>가치 V = (광물 가치 + 주민 가치) × 식량·석재·인프라·빈 땅·생활 수준 배율.</summary>
        public readonly double Value;

        public ValuationResult(string bidderId, double ore, double devOre, double food, double stone,
            double mf, double ms, double mi, double baseValue, double oreRaw = double.NaN, bool stoneFromCapacity = false, double mo = 1,
            double population = 0, double ml = 1)
        {
            PopulationValue = population;
            LivingMultiplier = ml;
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
            Value = baseValue * mf * ms * mi * mo * ml;
        }

        /// <summary>입찰가 = V × 배율(시세 등).</summary>
        public double Bid(double multiplier) => Value * multiplier;
    }

    /// <summary>
    /// 경매 가치 평가. 계수는 설정 auctionSeed·auction.
    /// - 실적: 경매 직전 평가 창(confirmedRules.auctionHistoryGameHours)의 실제 생산. 식량·석재는 저장 한도로 폐기된 생산분도 포함.
    /// - 개발 중 자산(결정 A3): 공사 중인 시설은 '완공 가정 물 배분'의 기대 생산 × 평가 창 × 진행률 × auction.developmentAssetRatio,
    ///   평가 창 안에 완공(또는 개량 완료)한 시설은 실적이 없던 시간 × 현재 기대 생산 × 비율을 더한다.
    ///   공사 중인 인프라 시설은 완공되면 늘어날 인프라 배율 × 진행률 × 비율(<see cref="InfraTable"/>, 결정 IB1·IB2).
    /// - 가동 숙성(경매 직전 몰아짓기 방지): 생산 시설의 실적은 완공 직후 auction.maturityStartRatio에서 시작해
    ///   auction.maturityFullHours 동안 100%까지 오른다(시간마다 HourRecord.*Valued로 기록). 개발 중 자산에도 시작 비율을 곱한다.
    /// - 석재 배율(다 지은 뒤 채석장 철거 방지): 평가 창 평균과 가동 중 채석장의 생산 능력 중 큰 값.
    /// - 입찰자 성향(결정 A2): 광물 가중치, 배율 가중치(1을 넘는 부분에 곱함), 공사 중 시설 종류별 개발 가중치.
    /// - 주민(결정 CL1·CL2): 사는 인원 × 1인 가치 × 입찰자 주민 가중치를 광물 가치에 더하고, 주택은 거주율만큼 인프라 배율에 들어간다.
    /// - 생활 수준(결정 LS7): 입찰자 생활 수준 가중치만큼 가치에 1 ± 비율을 곱한다(이주 사업가·부동산 개발업자).
    /// </summary>
    public static class SettlementValuation
    {
        /// <summary>
        /// 인프라 배율을 주는 시설 종류와 설정 auctionSeed.infra의 값. Once면 몇 개를 지어도 한 번만 센다.
        /// 운영 중 합계와 공사 중 보너스가 이 표 하나를 함께 쓴다 — 새 인프라 시설은 여기에 한 줄만 더한다.
        /// </summary>
        private static readonly (string Kind, bool Once, Func<InfraMultipliers, double> Bonus)[] InfraTable =
        {
            (BuildingKinds.Well, true, i => i.Well),
            (BuildingKinds.AuctionHall, true, i => i.AuctionHall),
            (BuildingKinds.CanalEdge, false, i => i.PerOperationalCanal),
            (BuildingKinds.Warehouse, false, i => i.PerOperationalWarehouse),
            (BuildingKinds.Hospital, false, i => i.PerOperationalHospital),
            (BuildingKinds.WaterTank, false, i => i.PerOperationalWaterTank),
            (BuildingKinds.Market, false, i => i.PerOperationalMarket),
            (BuildingKinds.House, false, i => i.PerOperationalHouse), // 운영 중 몫은 거주율만큼(InfraCount)
        };

        /// <summary>인프라 배율에 넣는 운영 중 시설 수. 주택은 거주율(사는 인원 ÷ 정원)의 합(결정 CL2), Once면 최대 1.</summary>
        private static double InfraCount(Settlement s, string kind, bool once)
        {
            if (kind == BuildingKinds.House)
                return s.OperatingOf(kind).Sum(h => s.HouseCapacity(h) > 0 ? Math.Min(1, h.Residents / s.HouseCapacity(h)) : 0);
            int operating = s.OperatingOf(kind).Count();
            return once ? Math.Min(1, operating) : operating;
        }

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
                double capacity = s.OperatingOf(BuildingKinds.Quarry).Sum(q => s.Rules.MaxOutput(q.Kind, s.LevelOf(q)));
                if (capacity > stone) { stone = capacity; stoneFromCapacity = true; }
            }
            double mfRaw = 1 + (seed.FoodMaxMultiplier - 1) * Math.Min(1, food / seed.FoodProductionAverageForFullBonus);
            double msRaw = 1 + (seed.StoneMaxMultiplier - 1) * Math.Min(1, stone / seed.StoneProductionAverageForFullBonus);

            // 인프라: 운영 중 시설 + 공사 중 인프라의 개발 중 자산. 미완공·폐쇄 시설 자체는 보너스 없음.
            InfraMultipliers infra = seed.Infra;
            double miRaw = 1;
            foreach ((string kind, bool once, Func<InfraMultipliers, double> bonus) in InfraTable)
                miRaw += InfraCount(s, kind, once) * bonus(infra);
            miRaw = Math.Min(infra.Maximum, miRaw + devInfra);

            double mf = 1 + (mfRaw - 1) * bidder.FoodMultiplierWeight;
            double ms = 1 + (msRaw - 1) * bidder.StoneMultiplierWeight;
            double mi = 1 + (miRaw - 1) * bidder.InfraWeight;
            // 주민 가치(결정 CL1): 사는 인원 × 1인 가치 × 입찰자 가중치를 광물 기반 가치에 더한다(배율은 둘 다에 곱해짐).
            double population = s.Residents * seed.ValuePerResident * bidder.PopulationWeight;
            double baseValue = seed.OreProduction24hValueFactor * (ore + devOre) * bidder.OreWeight + population;
            double mo = 1 + bidder.OpenLandWeight * (OpenLandShare(s) - seed.OpenLandShareCenter);
            double ml = Math.Max(0, 1 + bidder.LivingStandardWeight * s.LivingStandard / 100.0); // 생활 수준(결정 LS7)
            return new ValuationResult(bidder.Id, ore, devOre, food, stone, mf, ms, mi, baseValue, oreRaw, stoneFromCapacity, Math.Max(0, mo), population, ml);
        }

        /// <summary>
        /// 개발 가능한 빈 땅 비율 = 시설·예약이 없는 사막 타일 / 사막 타일 전체(0~1). 부동산 개발업자·건설업자가 본다
        /// (기획서 8.4 '개발 가능 부지', Docs/decision_log_auto.md BD3).
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
                    progress = Progress(job, auctionHour);
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
                    double bonus = InfraGainOnCompletion(s, f, progress, auctionHour, infra);
                    if (bonus > 0) list.Add(new DevelopmentAsset(f.Id, f.Kind, 0, 0, 0, bonus * progress * ratio));
                }

                ResourceKind resource = SettlementRules.ResourceOf(f.Kind);
                if (resource == ResourceKind.None || resource == ResourceKind.Goods) continue; // 기호품·향신료는 식량 배율에 넣지 않는다(결정 FD7)
                if (assumed == null) assumed = s.SolveWaterAssumingJobsComplete();

                BuildingLevel lv = s.Rules.Level(f.Kind, s.AssumedLevelOf(f));
                double rate = (lv.WaterDemand ?? 0) > 0 ? assumed.RateOf(WaterIds.Of(f)) : 1.0;
                // 완공 가정 물 배분의 기대 생산 × 실적이 없던 시간 × 진행률 × 비율. 막 지은 시설이므로 숙성 시작 비율을 곱한다.
                double output = s.Rules.MaxOutput(f.Kind, lv);
                double perHour = resource == ResourceKind.Food ? s.Rules.NetFoodValue(f.Kind, output, lv) : output; // 식량은 순 식량 가치(결정 FD7)
                double amount = s.Rules.Efficiency(rate, s.ExpectedWorkforce(f, job != null)) * missingHours * progress * ratio * s.Rules.MaturityStartRatio
                                * perHour;
                list.Add(new DevelopmentAsset(f.Id, f.Kind,
                    resource == ResourceKind.Ore ? amount : 0, resource == ResourceKind.Food ? amount : 0, resource == ResourceKind.Stone ? amount : 0, 0));
            }
            return list;
        }

        /// <summary>auctionHour 시점의 공사 진행률(0~1).</summary>
        private static double Progress(ConstructionJob job, long auctionHour) =>
            job.Hours > 0 ? Math.Max(0, Math.Min(1, (auctionHour - job.StartedHour) / (double)job.Hours)) : 1;

        /// <summary>
        /// 공사 중인 시설 f가 완공되면 늘어날 인프라 배율(진행률·비율 곱하기 전). 인프라 시설이 아니면 0.
        /// 한 번만 세는 종류(우물·경매장)는 같은 종류가 이미 운영 중이면 0이고, 여러 개를 짓는 중이면
        /// 진행률이 가장 큰 하나(같으면 번호가 작은 것)만 센다 — 착공만 해 두고 가치를 올리는 요령을 막는다(결정 IB2).
        /// </summary>
        private static double InfraGainOnCompletion(Settlement s, Facility f, double progress, long auctionHour, InfraMultipliers infra)
        {
            int row = Array.FindIndex(InfraTable, r => r.Kind == f.Kind);
            if (row < 0) return 0;
            double bonus = InfraTable[row].Bonus(infra);
            if (!InfraTable[row].Once) return bonus;
            if (s.OperatingOf(f.Kind).Any()) return 0;
            bool leads = s.Jobs
                .Where(j => j.Type == JobType.Build && j.State == JobState.InProgress && j.Kind == f.Kind && j.FacilityId != f.Id)
                .All(j =>
                {
                    double other = Progress(j, auctionHour);
                    return other < progress || (other == progress && j.FacilityId > f.Id);
                });
            return leads ? bonus : 0;
        }
    }
}
