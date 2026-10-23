using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;
using DesertSettlement.Core.Water;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 타일·시설 정보를 사람이 읽는 한국어 문장으로 만든다. 호버 팝업(<see cref="HoverTooltip"/>),
    /// HUD 선택 패널, 머리 위 글자가 같은 문구를 쓰도록 한곳에 모았다. 표현을 바꾸려면 여기만 고치면 된다.
    /// </summary>
    public static class FacilityInfo
    {
        public static string KindName(string kind)
        {
            switch (kind)
            {
                case BuildingKinds.Well: return "우물";
                case BuildingKinds.Farm: return "농장";
                case BuildingKinds.Quarry: return "채석장";
                case BuildingKinds.Mine: return "광산";
                case BuildingKinds.AuctionHall: return "경매장";
                case BuildingKinds.Warehouse: return "창고";
                case BuildingKinds.Hospital: return "병원";
                case BuildingKinds.CanalEdge: return "수로";
                case BuildingKinds.CanalGate: return "수문";
                case BuildingKinds.WaterTank: return "물탱크";
                default: return kind;
            }
        }

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
            if ((lv.WaterDemand ?? 0) > 0 && water != null && water.Consumers.TryGetValue(f.Id.ToString(), out WaterConsumerResult w))
            {
                lines.Add($"물: {w.Delivered:0.0} / {w.Demand:0} ({w.Rate:P0}), 우물에서 거리 {w.PriorityDistance}");
                if (w.Shortage != WaterShortageReason.None) lines.Add("물 부족 이유: " + ShortageText(w.Shortage));
            }
            if (lv.WaterSupply.HasValue && water != null && water.SourceReleased.TryGetValue(f.Id.ToString(), out double released))
            {
                double supply = (lv.WaterSupply ?? 0) * s.Rules.WellSupplyMultiplier;
                string bonus = s.Rules.WellSupplyMultiplier > 1.0001 ? $", 장비 ×{s.Rules.WellSupplyMultiplier:0.00}" : "";
                lines.Add($"방출: {released:0.0} / {supply:0} (식량 계수 영향 없음{bonus})");
            }
            if (f.Kind == BuildingKinds.CanalEdge)
            {
                double load = 0;
                if (water != null) water.EdgeLoads.TryGetValue(f.Edge.Value, out load);
                lines.Add($"흐름: {load:0.0} / 용량 {lv.CapacityPerHour:0}");
            }
            if (f.Kind == BuildingKinds.CanalGate) lines.Add(f.GateOpen ? "수문: 열림" : "수문: 닫힘 (이 변으로 물이 지나가지 않음)");
            if (f.Kind == BuildingKinds.Warehouse) lines.Add($"저장 한도 +식량 {lv.FoodStorageBonus:0} / +석재 {lv.StoneStorageBonus:0}");
            if (f.Kind == BuildingKinds.Hospital) lines.Add("병원: 이 칸과 이웃 6칸 건물의 전염병 피해 절반");

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
