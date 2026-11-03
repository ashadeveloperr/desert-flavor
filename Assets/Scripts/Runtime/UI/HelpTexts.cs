using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;
using static DesertSettlement.Runtime.UI.UiKit;

namespace DesertSettlement.Runtime.UI
{
    /// <summary>
    /// 도구 막대·위쪽 자원 카드의 마우스 툴팁 문구와 시설 아이콘(결정 HT3·HT4).
    /// 설명 문구는 여기 한 곳, 수치는 설정 JSON 레벨표에서 읽어 붙인다(문구에 숫자를 베끼지 않는다).
    /// ⚠ 새 시설 종류는 <see cref="Facilities"/>에 한 줄 — 빠뜨리면 <c>HelpTextsTests</c>가 실패한다.
    /// </summary>
    public static class HelpTexts
    {
        /// <summary>시설 한 종류의 아이콘·역할·생산물의 쓰임(없으면 null).</summary>
        public sealed class Entry
        {
            public Icons.Kind Icon;
            public string Role;
            public string Use;
        }

        /// <summary>시설 종류(<see cref="BuildingKinds"/>)별 안내.</summary>
        public static readonly IReadOnlyDictionary<string, Entry> Facilities = new Dictionary<string, Entry>
        {
            [BuildingKinds.Well] = new Entry { Icon = Icons.Kind.Well,
                Role = "물을 뽑아 우물 꼭짓점에 닿은 수로로 내보낸다. 우물과 꼭짓점을 맞댄 시설은 수로 없이 바로 받는다.",
                Use = "물 → 주택·경작지·과수원·목축지·허브 농장·광산·가공 시설" },
            [BuildingKinds.CanalEdge] = new Entry { Icon = Icons.Kind.Canal,
                Role = "육각형 변에 놓는 물길. 우물의 물을 시설 꼭짓점까지 나른다. 끌어서 여러 칸을 한 번에 놓는다.",
                Use = "폐쇄하면 그 변으로 물이 흐르지 않는다" },
            [BuildingKinds.WaterTank] = new Entry { Icon = Icons.Kind.WaterTank,
                Role = "쓰고 남은 물을 모았다가, 물이 모자랄 때 하류(우물에서 시설로 가는 길이 탱크를 지나는 곳)로 내보낸다.",
                Use = "물 공급이 들쭉날쭉할 때 완충" },
            [BuildingKinds.Farm] = new Entry { Icon = Icons.Kind.Food,
                Role = "물과 일손으로 곡물을 기른다. 메뚜기떼·작물 탄저병에 피해를 입는다.",
                Use = "곡물 → 주민 식량, 목축지·제분소·양조장의 원료" },
            [BuildingKinds.Orchard] = new Entry { Icon = Icons.Kind.Fruit,
                Role = "물과 일손으로 과일을 기른다. 작물 재해에 피해를 입는다.",
                Use = "과일 → 주민 식량(생활 수준 ↑), 포도주 공방의 원료" },
            [BuildingKinds.Pasture] = new Entry { Icon = Icons.Kind.Meat,
                Role = "곡물을 먹여 물과 일손으로 고기를 만든다. 경작지 옆이면 곡물이 덜 든다.",
                Use = "고기 → 식량 효율이 높고 생활 수준을 크게 올린다" },
            [BuildingKinds.HerbFarm] = new Entry { Icon = Icons.Kind.Herb,
                Role = "산지(장애물) 타일에만 짓는다. 물과 일손으로 향신료를 기른다.",
                Use = "향신료 → 생활 수준 보너스를 키운다" },
            [BuildingKinds.Quarry] = new Entry { Icon = Icons.Kind.Stone,
                Role = "암반에서 석재를 캔다. 채석장끼리 붙이면 생산이 는다.",
                Use = "석재 → 모든 건설·개량의 재료, 시장에서 팔 수 있다" },
            [BuildingKinds.Mine] = new Entry { Icon = Icons.Kind.Ore,
                Role = "광맥에서 물과 일손으로 광물을 캔다. 광산끼리 붙이면 생산이 는다.",
                Use = "광물 → 경매에서 입찰자가 정착지 값을 매기는 핵심 실적" },
            [BuildingKinds.Mill] = new Entry { Icon = Icons.Kind.Bread,
                Role = "곡물로 빵을 굽는다. 경작지 옆이면 곡물이 덜 든다.",
                Use = "빵 → 곡물보다 식량 효율이 높고 생활 수준을 올린다" },
            [BuildingKinds.Brewery] = new Entry { Icon = Icons.Kind.Beer,
                Role = "곡물로 맥주를 빚는다. 경작지 옆이면 곡물이 덜 든다.",
                Use = "맥주(기호품) → 생활 수준을 올린다" },
            [BuildingKinds.Winery] = new Entry { Icon = Icons.Kind.Wine,
                Role = "과일로 와인을 빚는다. 과수원 옆이면 과일이 덜 든다.",
                Use = "와인(기호품) → 생활 수준을 올린다" },
            [BuildingKinds.House] = new Entry { Icon = Icons.Kind.House,
                Role = "주민이 산다. 주민은 수로 물과 식량을 쓰고 시설 운영·공사에 자동 배치된다. 주택끼리 붙이면 빨리 찬다.",
                Use = "일손 → 우물·농장·채석장·광산·가공 시설·공사. 물·식량이 모자라면 주민이 떠난다" },
            [BuildingKinds.Warehouse] = new Entry { Icon = Icons.Kind.Warehouse,
                Role = "식품 창고와 석재 한도를 늘린다. 창고끼리 붙이면 식품이 덜 썩는다.",
                Use = "한도를 넘는 식품·석재는 시장이 있으면 자동 판매, 없으면 버려진다" },
            [BuildingKinds.Market] = new Entry { Icon = Icons.Kind.Market,
                Role = "있어야 식품·석재를 팔 수 있고, 상인 웃돈 없이 산다. 여러 개면 가격 회복이 빨라진다.",
                Use = "창고 한도를 넘는 식품·석재를 저절로 판다" },
            [BuildingKinds.Hospital] = new Entry { Icon = Icons.Kind.Hospital,
                Role = "전염병 환자를 병상 수만큼 입원시켜 집보다 훨씬 빨리 회복시킨다.",
                Use = "경매 가치에도 조금 보탬이 된다" },
            [BuildingKinds.AuctionHall] = new Entry { Icon = Icons.Kind.Gavel,
                Role = "경매에 오는 입찰자를 한 명 늘린다. 경매 자체는 경매장이 없어도 열린다.",
                Use = "입찰자가 많을수록 좋은 값·모디파이어를 고를 수 있다" },
        };

        /// <summary>관리 도구 아이콘·설명.</summary>
        public static (Icons.Kind Icon, string Help) Tool(BuildTool tool)
        {
            switch (tool)
            {
                case BuildTool.Upgrade: return (Icons.Kind.Upgrade, "시설을 다음 단계로 개량한다. 석재와 공사 시간이 들고, 공사 중에는 시설이 멈춘다.");
                case BuildTool.Demolish: return (Icons.Kind.Demolish, "시설을 철거한다. 석재 일부를 돌려받는다.");
                case BuildTool.ToggleClose: return (Icons.Kind.Close, "시설을 폐쇄하거나 다시 연다. 폐쇄하면 운영이 멈추고 유지비가 줄어든다.");
                default: return (Icons.Kind.Pointer, "시설을 눌러 오른쪽에 정보와 물길을 본다. 오른쪽 클릭·Esc로 언제든 이 도구로 돌아온다.");
            }
        }

        /// <summary>도구 막대 시설 버튼의 툴팁: 이름·짓는 곳, 역할, 생산물의 쓰임, 1단계 수치.</summary>
        public static string FacilityTip(Settlement s, string kind)
        {
            FacilityType type = FacilityCatalog.Get(kind);
            Facilities.TryGetValue(kind, out Entry e);
            var lines = new List<string>
            {
                Paint(FacilityCatalog.NameOf(kind), Theme.Accent) + "  " + Paint($"{type?.Group} · 짓는 곳: {PlaceName(type)}", Theme.Muted),
            };
            if (e != null)
            {
                lines.Add(e.Role);
                lines.Add(Paint(e.Use, Theme.Good));
            }
            BuildingLevel lv = s.Rules.Config.GetLevel(kind, 1);
            if (lv != null)
            {
                int free = s.FreeBuildsRemaining(kind);
                string cost = free > 0 ? Paint($"무료 완공 {free}번 남음", Theme.Good) : $"석재 {(lv.Stone ?? 0):0} · {s.Rules.JobHours(lv.Hours)}시간";
                lines.Add(Paint("1단계: ", Theme.Muted) + cost + $" · 유지비 {(lv.Upkeep ?? 0):0.00}/h");
                string stats = string.Join(" · ", Stats(lv));
                if (stats.Length > 0) lines.Add(Paint(stats, Theme.Muted));
            }
            return string.Join("\n", lines);
        }

        private static string PlaceName(FacilityType type)
        {
            if (type == null) return "";
            if (type.OnEdge) return "육각형 변";
            return string.Join("·", type.Terrain.Select(t =>
                t == TerrainType.WaterSource ? "수원" : t == TerrainType.OreVein ? "광맥" : t == TerrainType.Bedrock ? "암반" : t == TerrainType.Obstacle ? "산지" : "사막"));
        }

        /// <summary>레벨표에 있는 값만 짧게(물·생산·사람·저장).</summary>
        private static IEnumerable<string> Stats(BuildingLevel lv)
        {
            if (lv.WaterSupply > 0) yield return $"물 공급 {lv.WaterSupply:0}/h";
            if (lv.WaterDemand > 0) yield return $"물 {lv.WaterDemand:0}/h";
            double output = (lv.FoodProduction ?? 0) + (lv.GoodsProduction ?? 0) + (lv.StoneProduction ?? 0) + (lv.OreProduction ?? 0);
            if (output > 0) yield return $"생산 {output:0.#}/h";
            if (lv.Workers > 0) yield return $"운영 {FacilityInfo.People(lv.Workers.Value)}명";
            if (lv.BuildWorkers > 0) yield return $"공사 {FacilityInfo.People(lv.BuildWorkers.Value)}명";
            if (lv.Residents > 0) yield return $"주민 정원 {FacilityInfo.People(lv.Residents.Value)}명";
            if (lv.Beds > 0) yield return $"병상 {lv.Beds:0}";
            if (lv.Capacity > 0) yield return $"저수 {lv.Capacity:0}";
            if (lv.FoodStorageBonus > 0) yield return $"식품 창고 +{lv.FoodStorageBonus:0}";
            if (lv.StoneStorageBonus > 0) yield return $"석재 한도 +{lv.StoneStorageBonus:0}";
        }

        // ── 위쪽 자원 카드(결정 HT4) ──

        /// <summary>빈 줄을 뺀 여러 줄 문구.</summary>
        private static string Lines(params string[] lines) => string.Join("\n", lines.Where(l => !string.IsNullOrEmpty(l)));

        public static string CashTip(Settlement s) => Lines(
            Paint("자금 · 유지비", Theme.Accent),
            "운영 중인 시설마다 매시간 유지비가 빠져나간다(폐쇄하면 줄어듦). 식품·석재 구매에도 쓴다. 건설에는 석재만 든다.",
            "수입: 경매 매각, 시장 판매(넘침 자동 판매 포함).",
            Paint("자금이 바닥난 채 오래 버티면 파산한다. 아랫줄 = 시간당 유지비 · 지금 속도로 버틸 시간", Theme.Warn),
            s.Rules.UpkeepMultiplier > 1.005 ? Paint($"이 지역 운영비 ×{s.Rules.UpkeepMultiplier:0.00}", Theme.Muted) : "");

        public static string FoodTip(Settlement s)
        {
            string pantry = FacilityInfo.PantryLine(s);
            return Lines(
                Paint("식품 창고", Theme.Accent),
                "곡물·과일·빵·고기·맥주·와인·향신료를 함께 담는다. 큰 숫자 = 담긴 양 / 한도(창고를 지으면 늘어남).",
                "주민이 매시간 먹는다. 모자라면 굶고, 계속 굶으면 주민이 떠난다. 상위 식품을 먹을수록 생활 수준이 오른다.",
                $"식품은 조금씩 썩는다(지금 {s.FoodSpoilagePerDay:0.##%}/일, 창고끼리 붙이면 줄어듦). 한도를 넘으면 시장이 있을 때 자동 판매, 없으면 버림.",
                Paint("아랫줄 = 시간당 순 식량(생산 − 먹은 양 − 썩은 양)", Theme.Muted),
                pantry.Length > 0 ? Paint("지금: " + pantry, Theme.Good) : "");
        }

        public static string StoneTip(Settlement s) => Lines(
            Paint("석재", Theme.Accent),
            "건설·개량에 드는 유일한 재료. 채석장에서 나오고 시장에서 사고팔 수 있다.",
            "큰 숫자 = 가진 양 / 한도(창고를 지으면 늘어남). 한도를 넘으면 시장이 있을 때 자동 판매, 없으면 버림.",
            "대기 중인 공사는 석재가 모이는 대로 착공한다.",
            Paint("아랫줄 = 시간당 생산, 공사가 석재를 기다리면 필요한 양", Theme.Muted));

        public static string PeopleTip(Settlement s)
        {
            if (!s.Rules.LaborOn)
                return Paint("식량 계수", Theme.Accent) + "\n식량이 모자라면 낮아지고, 낮을수록 모든 생산이 줄어든다.";
            LaborAssignment labor = s.LastLabor;
            return Lines(
                Paint("주민 · 일손", Theme.Accent),
                "큰 숫자 = 주민 / 주택 정원. 주택을 지으면 주민이 천천히 들어온다(생활 수준이 높고 주택이 붙어 있을수록 빠름).",
                "주민은 수로 물과 식량을 쓰고, 시설 운영과 공사에 자동으로 배치된다. 시설 정보창의 우선 고용으로 순서를 바꾼다.",
                "물이나 식량이 모자라면 주민이 떠난다. 전염병에 걸린 주민은 일하지 못한다.",
                Paint($"아랫줄 일손 = 배치된 사람 / 필요한 사람 (지금 {labor.Assigned:0}/{labor.Needed:0})", Theme.Muted));
        }

        public static string OreTip(Settlement s) => Lines(
            Paint("광물", Theme.Accent),
            "광맥 위 광산에서 캔다(물·일손 필요). 쌓아 두거나 파는 자원이 아니라, 경매에서 입찰자가 정착지 값을 매기는 핵심 실적이다.",
            Paint("큰 숫자 = 지난 1시간 광물 생산", Theme.Muted));
    }
}
