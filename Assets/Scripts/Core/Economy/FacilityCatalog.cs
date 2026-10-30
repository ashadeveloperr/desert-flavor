using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;

namespace DesertSettlement.Core.Config
{
    /// <summary>
    /// 시설 종류 이름(설정 JSON <c>buildingLevels</c>의 키). 코드에서 특정 시설을 가리킬 때 쓴다.
    /// 2026-10-24: EconomyConfig.cs에서 이 파일로 옮겼다. 시설 목록 자체는 아래 <see cref="FacilityCatalog"/>.
    /// </summary>
    public static class BuildingKinds
    {
        public const string Well = "well";
        public const string Farm = "farm";
        public const string Quarry = "quarry";
        public const string Mine = "mine";
        public const string AuctionHall = "auctionHall";
        public const string Warehouse = "warehouse";
        /// <summary>시장(2026-10-27 이슈 #18): 있어야 식량·석재를 팔 수 있고 상인 할증 없이 산다. 여러 개면 가격 회복이 빨라진다.</summary>
        public const string Market = "market";
        /// <summary>병원(2026-10-23): 인근 1칸 건물의 전염병 피해를 절반으로.</summary>
        public const string Hospital = "hospital";
        public const string WaterTank = "waterTank";
        public const string CanalEdge = "canalEdge";
        /// <summary>주택(결정 LB1~): 주민이 산다. 주민은 수로 물과 식량을 쓰고 우물·농장·채석장·광산과 공사에 배치된다.</summary>
        public const string House = "house";

        /// <summary>모든 시설 종류(카탈로그 순서).</summary>
        public static IReadOnlyList<string> All => FacilityCatalog.All.Select(t => t.Kind).ToList();
    }
}

namespace DesertSettlement.Core.Economy
{
    /// <summary>시설 한 종류의 정의(카탈로그 한 줄).</summary>
    public sealed class FacilityType
    {
        /// <summary>종류 이름 = 설정 JSON <c>buildingLevels</c>의 키.</summary>
        public string Kind { get; }
        /// <summary>화면에 보이는 한국어 이름.</summary>
        public string Name { get; }
        /// <summary>도구 막대 분류(물·생산·기반시설).</summary>
        public string Group { get; }
        /// <summary>지을 수 있는 지형. 변에 짓는 시설(수로)은 비어 있다.</summary>
        public TerrainType[] Terrain { get; }
        /// <summary>타일이 아니라 변에 짓는다(수로).</summary>
        public bool OnEdge { get; }
        /// <summary>화면 기본 색(0~1 RGB). Runtime의 SettlementView 인스펙터에서 덮어쓸 수 있다.</summary>
        public (float R, float G, float B) Color { get; }

        public FacilityType(string kind, string name, string group, (float, float, float) color, bool onEdge = false, params TerrainType[] terrain)
        {
            Kind = kind;
            Name = name;
            Group = group;
            Color = color;
            OnEdge = onEdge;
            Terrain = terrain;
        }
    }

    /// <summary>
    /// 시설 정의를 한곳에(2026-10-24 검토 개선 5, Docs/decision_log_auto.md CL1~CL3): 이름·도구 막대 분류·지을 수 있는 지형·기본 색.
    /// 지형 규칙(<see cref="SettlementRules.AllowedTerrain"/>), 도구 막대, 한국어 이름표, 시설 색이 모두 이 목록을 읽는다.
    /// <para><b>새 시설 추가</b>: ① 설정 JSON <c>buildingLevels</c>에 레벨표, ② 아래 목록에 한 줄,
    /// ③ (선택) 모양 <c>Runtime/FacilityModels.cs</c>(없으면 상자), ④ 특별한 효과가 있으면 그 규칙 코드(예: 가치 평가 배율).</para>
    /// </summary>
    public static class FacilityCatalog
    {
        public const string Water = "물", Production = "생산", Infrastructure = "기반시설";

        /// <summary>도구 막대 분류 순서.</summary>
        public static readonly string[] Groups = { Water, Production, Infrastructure };

        /// <summary>모든 시설. 순서 = 도구 막대 버튼 순서.</summary>
        public static readonly IReadOnlyList<FacilityType> All = new[]
        {
            new FacilityType(BuildingKinds.Well, "우물", Water, (0.2f, 0.45f, 0.95f), false, TerrainType.WaterSource),
            new FacilityType(BuildingKinds.CanalEdge, "수로", Water, (0.38f, 0.31f, 0.23f), onEdge: true),
            new FacilityType(BuildingKinds.WaterTank, "물탱크", Water, (0.45f, 0.75f, 0.95f), false, TerrainType.Desert),
            new FacilityType(BuildingKinds.Farm, "농장", Production, (0.35f, 0.75f, 0.3f), false, TerrainType.Desert),
            new FacilityType(BuildingKinds.Quarry, "채석장", Production, (0.75f, 0.72f, 0.68f), false, TerrainType.Bedrock),
            new FacilityType(BuildingKinds.Mine, "광산", Production, (0.45f, 0.3f, 0.25f), false, TerrainType.OreVein),
            new FacilityType(BuildingKinds.House, "주택", Infrastructure, (0.9f, 0.8f, 0.62f), false, TerrainType.Desert),
            new FacilityType(BuildingKinds.Warehouse, "창고", Infrastructure, (0.7f, 0.5f, 0.3f), false, TerrainType.Desert),
            new FacilityType(BuildingKinds.Market, "시장", Infrastructure, (0.85f, 0.45f, 0.35f), false, TerrainType.Desert),
            new FacilityType(BuildingKinds.Hospital, "병원", Infrastructure, (0.95f, 0.95f, 0.95f), false, TerrainType.Desert),
            new FacilityType(BuildingKinds.AuctionHall, "경매장", Infrastructure, (0.95f, 0.75f, 0.2f), false, TerrainType.Desert),
        };

        private static readonly Dictionary<string, FacilityType> ByKind = All.ToDictionary(t => t.Kind);

        /// <summary>종류의 정의. 목록에 없으면 null.</summary>
        public static FacilityType Get(string kind) => kind != null && ByKind.TryGetValue(kind, out FacilityType t) ? t : null;

        /// <summary>한국어 이름(목록에 없으면 종류 이름 그대로).</summary>
        public static string NameOf(string kind) => Get(kind)?.Name ?? kind;

        /// <summary>분류에 속한 시설(목록 순서).</summary>
        public static IEnumerable<FacilityType> InGroup(string group) => All.Where(t => t.Group == group);
    }
}
