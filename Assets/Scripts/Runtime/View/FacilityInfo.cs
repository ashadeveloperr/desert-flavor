using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;
using DesertSettlement.Core.Water;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 타일·시설 정보를 사람이 읽는 한국어 문장으로 만든다. 툴팁(UI/TooltipView), 정보 패널(UI/InspectorView),
    /// 머리 위 글자가 같은 문구를 쓰도록 한곳에 모았다. 표현을 바꾸려면 여기만 고치면 된다.
    /// </summary>
    public static class FacilityInfo
    {
        /// <summary>시설 한국어 이름(시설 목록 <see cref="FacilityCatalog"/>에서, 2026-10-24 CL1).</summary>
        public static string KindName(string kind) => FacilityCatalog.NameOf(kind);

        public static string TerrainName(TerrainType t)
        {
            switch (t)
            {
                case TerrainType.WaterSource: return "수원 (우물)";
                case TerrainType.OreVein: return "광맥 (광산)";
                case TerrainType.Bedrock: return "암반 (채석장)";
                case TerrainType.Obstacle: return "장애물 (건설 불가)";
                default: return "사막 (농장·창고·경매장)";
            }
        }

        public static string StateName(FacilityState state)
        {
            switch (state)
            {
                case FacilityState.Constructing: return "건설 중";
                case FacilityState.Operating: return "운영 중";
                case FacilityState.Upgrading: return "개량 중";
                case FacilityState.Closed: return "폐쇄";
                case FacilityState.Demolishing: return "철거 중";
                default: return state.ToString();
            }
        }

        public static string ResourceName(ResourceKind r)
        {
            switch (r)
            {
                case ResourceKind.Food: return "식량";
                case ResourceKind.Stone: return "석재";
                case ResourceKind.Ore: return "광물";
                default: return "";
            }
        }

        /// <summary>물 배분 공급원 id를 사람이 읽는 이름으로("우물 #3", "물탱크 #7").</summary>
        public static string SourceName(Settlement s, string sourceId)
        {
            return $"{(WaterIds.IsTank(sourceId) ? "물탱크" : "우물")} #{WaterIds.NumberOf(sourceId)}";
        }

        /// <summary>모래폭풍·메뚜기떼·탄저병의 지금 효과 한 줄(두 화면 공통).</summary>
        /// <summary>모래폭풍·메뚜기떼·탄저병의 지금 효과(장비 반영, 결정 D1~D6).</summary>
        public static string TimedDisasterText(Settlement s, DisasterEvent e)
        {
            DisasterSettings c = s.Disasters.Settings;
            SettlementRules r = s.Rules;
            switch (e.TypeId)
            {
                case DisasterManager.Sandstorm:
                    return $"수로 손실 ×{s.Disasters.CanalLossFactor:0.##}, 공사 속도 {s.Disasters.ConstructionSpeed:P0}";
                case DisasterManager.Locusts:
                    return $"농장 생산 {1 - (1 - c.Locusts.FarmProduction) * r.LocustDamageMultiplier:P0} (시작 때 식량 재고 −{c.Locusts.FoodStockLoss * r.LocustDamageMultiplier:P0})";
                case DisasterManager.Blight:
                    return $"농장 생산 {1 - (1 - c.Blight.FarmProduction) * r.BlightDamageMultiplier:P0}";
                default:
                    return "";
            }
        }

        /// <summary>투입 석재와 철거 환급(정보창·예전 화면 공통).</summary>
        public static string InvestedText(Settlement s, Facility f) =>
            $"투입 석재 {f.StoneInvested:0} (철거 시 {f.StoneInvested * s.Rules.DemolishRefundRatio:0} 환급)";

        public static string ShortageText(WaterShortageReason reason)
        {
            switch (reason)
            {
                case WaterShortageReason.Unreachable: return "수로 연결 없음";
                case WaterShortageReason.CanalCapacity: return "수로 용량 부족";
                case WaterShortageReason.SourceSupply: return "우물 물 부족";
                default: return "";
            }
        }

        /// <summary>다음 정착지 혜택 문구. 예: " + 다음 정착지 완공된 농장 1개". 없으면 빈 문자열.</summary>
        public static string PerkText(double foodBonus, IReadOnlyDictionary<string, int> freeBuilds)
        {
            var parts = new List<string>();
            if (foodBonus > 0) parts.Add($"시작 식량 +{foodBonus:0}");
            if (freeBuilds != null)
            {
                foreach (KeyValuePair<string, int> kv in freeBuilds.Where(kv => kv.Value > 0)) parts.Add($"완공된 {KindName(kv.Key)} {kv.Value}개");
            }
            return parts.Count == 0 ? "" : " + 다음 정착지 " + string.Join(", ", parts);
        }

        /// <summary>생산 줄. 예: "생산: 식량 3.20/h (최대 4.00 · 물 80% · F 100%)". 생산 시설이 아니면 null.</summary>
        public static string ProductionLine(Settlement s, Facility f)
        {
            FacilityOutput o = s.OutputOf(f);
            if (!o.IsProducer) return null;
            return $"생산: {ResourceName(o.Resource)} {o.ProducedPerHour:0.00}/h (최대 {o.MaxPerHour:0.00} · 물 {o.WaterRate:P0} · F {o.FoodCoefficient:P0})";
        }

        /// <summary>전염병 중인 생산 시설의 피해 줄(병원 인근이면 표시). 아니면 null.</summary>
        public static string EpidemicLine(Settlement s, Facility f)
        {
            DisasterManager d = s.Disasters;
            if (d == null || !d.Epidemic.Active) return null;
            double factor = d.ProductionFactor(f);
            if (factor >= 0.9999) return null;
            return $"전염병: 생산 −{1 - factor:P0}{(d.IsNearHospital(f) ? " (병원 인근 — 피해 절반)" : "")}";
        }

        /// <summary>농장이 자기 소비보다 적게 생산할 때의 경고. 아니면 null.</summary>
        public static string FarmDeficitText(Settlement s, Facility f)
        {
            FacilityOutput o = s.OutputOf(f);
            return o.FarmBelowOwnUpkeep ? $"식량 생산 {o.ProducedPerHour:0.00} < 소비 {o.FoodUsePerHour:0.00} (이 농장만으로 적자)" : null;
        }

        /// <summary>
        /// 공급원(우물·물탱크)의 물이 간 길을 경로마다 한 줄로(2026-10-26 WD2, 이슈 #2: 예전에는 '물길 n개' 개수만).
        /// 가까운 경로부터 최대 MaxRouteLines줄, 나머지는 '외 n개'. 물탱크 충전은 시설 목록에 없어 빠진다.
        /// </summary>
        private static void AddSentRoutes(List<string> lines, Settlement s, WaterAllocation water, string sourceId, string label)
        {
            var routes = WaterRoutes.DeliveriesFrom(water, sourceId);
            if (routes.Count == 0) { lines.Add($"  {label}의 물길 없음 (물을 받는 시설 없음)"); return; }
            lines.Add($"  {label}의 물길 {routes.Count}개 · 합계 {routes.Sum(x => x.Flow.Delivered):0.0}/h (맵에 하늘색 띠)");
            foreach (var (consumerId, flow) in routes.Take(MaxRouteLines))
            {
                Facility to = WaterIds.TryFacilityId(consumerId, out int id) ? s.GetFacility(id) : null;
                string name = to != null ? $"{KindName(to.Kind)} #{to.Id}" : consumerId;
                string how = flow.Distance == 0 ? "옆 꼭짓점에서 바로" : $"수로 {flow.Distance / 2.0:0.#}칸";
                lines.Add($"    → {name}: {how} → {flow.Delivered:0.0}/h");
            }
            if (routes.Count > MaxRouteLines) lines.Add($"    … 외 {routes.Count - MaxRouteLines}개");
        }

        private const int MaxRouteLines = 6;

        /// <summary>시설 상세(여러 줄). progress는 공사 진행률(0~1).</summary>
        public static List<string> Describe(Settlement s, Facility f, float progress)
        {
            BuildingLevel lv = s.LevelOf(f);
            var lines = new List<string> { $"{KindName(f.Kind)} L{f.Level} — {StateName(f.State)}" };

            if (f.State == FacilityState.Constructing || f.State == FacilityState.Upgrading || f.State == FacilityState.Demolishing)
            {
                ConstructionJob job = s.Jobs.FirstOrDefault(j => j.FacilityId == f.Id && j.State == JobState.InProgress);
                lines.Add($"공사 {progress:P0}" + (job != null ? $" (완료 {job.DeadlineHour}시간째)" : ""));
            }

            string production = ProductionLine(s, f);
            if (production != null) lines.Add(production);
            string epidemic = EpidemicLine(s, f);
            if (epidemic != null) lines.Add(epidemic);

            WaterAllocation water = s.LastWater;
            if ((lv.WaterDemand ?? 0) > 0 && water != null && water.Consumers.TryGetValue(WaterIds.Of(f), out WaterConsumerResult w))
            {
                lines.Add($"물: {w.Delivered:0.0} / {w.Demand:0} ({w.Rate:P0}), 우물에서 거리 {w.PriorityDistance}");
                if (w.Shortage != WaterShortageReason.None) lines.Add("물 부족 이유: " + ShortageText(w.Shortage) + (w.Shortage == WaterShortageReason.CanalCapacity ? " (맵에 빨간 띠)" : ""));
                // 물길(2026-10-23): 어느 공급원에서 수로 몇 칸을 지나 얼마 받았는지. 맵에는 하늘색 띠로 표시된다.
                foreach (WaterIntakeFlow flow in w.Intakes)
                    lines.Add(flow.Distance == 0
                        ? $"  물길: {SourceName(s, flow.SourceId)} 옆 꼭짓점에서 바로 → {flow.Delivered:0.0}/h (이어 주는 수로 1개, 하늘색 띠)"
                        : $"  물길: {SourceName(s, flow.SourceId)}에서 수로 {flow.Distance / 2.0:0.#}칸 → {flow.Delivered:0.0}/h");
            }
            if (lv.WaterSupply.HasValue && water != null && water.SourceReleased.TryGetValue(WaterIds.Of(f), out double released))
            {
                double supply = (lv.WaterSupply ?? 0) * s.Rules.WellSupplyMultiplier;
                string bonus = s.Rules.WellSupplyMultiplier > 1.0001 ? $", 장비 ×{s.Rules.WellSupplyMultiplier:0.00}" : "";
                lines.Add($"방출: {released:0.0} / {supply:0} (식량 계수 영향 없음{bonus})");
                AddSentRoutes(lines, s, water, WaterIds.Of(f), "이 우물");
            }
            if (f.Kind == BuildingKinds.CanalEdge)
            {
                double load = 0, direct = 0;
                if (water != null) water.EdgeLoads.TryGetValue(f.Edge.Value, out load);
                if (water != null) water.DirectIntakeLoads.TryGetValue(f.Edge.Value, out direct);
                lines.Add($"흐름: {load:0.0} / 용량 {lv.CapacityPerHour:0}");
                // WD1(2026-10-26): 우물 옆 시설이 공유 꼭짓점에서 바로 받는 물은 변을 지나지 않아 흐름에 안 잡히지만, 이 수로가 그 꼭짓점을 이어 준다.
                if (direct > 1e-6) lines.Add($"  + 우물 옆 직접 취수 {direct:0.0}/h (이 수로가 꼭짓점을 이어 줌, 용량은 안 씀 — 없애면 끊김)");
            }
            if (f.Kind == BuildingKinds.CanalEdge && f.State == FacilityState.Closed) lines.Add("닫힌 수로: 이 변으로 물이 지나가지 않음 (폐쇄 해제로 다시 열기)");
            if (f.Kind == BuildingKinds.Warehouse) lines.Add($"저장 한도 +식량 {lv.FoodStorageBonus:0} / +석재 {lv.StoneStorageBonus:0}");
            if (f.Kind == BuildingKinds.Hospital) lines.Add("병원: 이 칸과 이웃 6칸 건물의 전염병 피해 절반");
            if (f.Kind == BuildingKinds.Market && s.Rules.Trade != null)
                lines.Add($"시장: 식량·석재 판매, 상인 할증 없이 구매 · 시장 {s.MarketCount}개 가격 회복 ×{s.Rules.Trade.RecoveryFactor(s.MarketCount):0.##}(시간당 {s.Rules.Trade.RecoveryPerHour * s.Rules.Trade.RecoveryFactor(s.MarketCount):P0})");
            if (f.Kind == BuildingKinds.WaterTank)
            {
                lines.Add($"저장 {f.StoredWater:0} / {s.TankCapacityOf(f):0} (충전·방출 최대 {lv.ChargePerHour:0}/{lv.DischargePerHour:0} /h)");
                switch (f.TankMode)
                {
                    case WaterTankMode.Charging: lines.Add($"지난 시간: 충전 +{f.TankFlowLastHour:0.0} (시설이 쓰고 남은 물)"); break;
                    case WaterTankMode.Discharging: lines.Add($"지난 시간: 방출 {-f.TankFlowLastHour:0.0} (하류의 모자란 시설에)"); break;
                    default: lines.Add("지난 시간: 대기 (가득 참·남는 물 없음·하류 부족 없음)"); break;
                }
                lines.Add("하류 = 우물에서 오는 물길이 이 탱크를 지나는 시설. 상류 수로를 닫아도 하류로 방출");
                if (f.TankMode == WaterTankMode.Discharging && water != null) AddSentRoutes(lines, s, water, WaterIds.Tank(f.Id), "방출");
            }

            FacilityOutput o = s.OutputOf(f);
            string upkeep = f.State == FacilityState.Closed || f.State == FacilityState.Upgrading
                ? $"유지비 {(lv.Upkeep ?? 0) * s.Rules.ClosedUpkeepRatio:0.00}/h (폐쇄·개량 중)"
                : $"유지비 {lv.Upkeep ?? 0:0.00}/h";
            lines.Add($"식량 소비 {o.FoodUsePerHour:0.00}/h · {upkeep}");

            string deficit = FarmDeficitText(s, f);
            if (deficit != null) lines.Add("⚠ " + deficit);
            return lines;
        }
    }
}
