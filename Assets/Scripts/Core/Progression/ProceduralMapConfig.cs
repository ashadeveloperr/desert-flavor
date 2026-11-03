using System;
using System.Collections.Generic;

namespace DesertSettlement.Core.Progression
{
    /// <summary>
    /// 절차 맵 설정 전체. 원본은 Assets/Data/Config/procedural_map_config.json(<see cref="ProceduralMapConfigLoader"/>).
    /// 아래 속성 기본값은 JSON과 같은 값이다(JSON에 빠진 항목은 기본값을 쓴다).
    /// </summary>
    public sealed class ProceduralMapConfig
    {
        public string Schema { get; set; }
        /// <summary>처음 몇 개 정착지를 고정 맵으로 할지. 1이면 첫 정착지만 고정 맵 1, 두 번째부터 생성 맵.</summary>
        public int FixedMapsBeforeGenerated { get; set; } = 1;
        public DifficultyCurve Difficulty { get; set; } = new DifficultyCurve();
        public GeneratorRules Generator { get; set; } = new GeneratorRules();
        /// <summary>지역 모디파이어(약점·강점) 목록과 출현 규칙.</summary>
        public RegionModifierSettings Modifiers { get; set; } = new RegionModifierSettings();
        /// <summary>행선지 창에서 보여 줄 다음 지역 후보 수.</summary>
        public int RegionChoices { get; set; } = 2;

        /// <summary>값이 말이 되는지 검사한다. 문제 목록(비어 있으면 정상).</summary>
        public List<string> Validate()
        {
            var p = new List<string>();
            if (FixedMapsBeforeGenerated < 0) p.Add("fixedMapsBeforeGenerated는 0 이상이어야 합니다.");
            DifficultyCurve d = Difficulty;
            if (d == null) p.Add("difficulty가 없습니다.");
            else
            {
                if (d.CashPerD <= 0) p.Add("difficulty.cashPerD는 0보다 커야 합니다.");
                if (d.BaseRadius < 3 || d.MaxRadius > 7 || d.BaseRadius > d.MaxRadius) p.Add("difficulty 반경은 3 ≤ baseRadius ≤ maxRadius ≤ 7 이어야 합니다.");
                if (d.RadiusStepD <= 0) p.Add("difficulty.radiusStepD는 0보다 커야 합니다.");
                if (d.MaxObstacleDensity < 0 || d.MaxObstacleDensity > 0.6) p.Add("difficulty.maxObstacleDensity는 0~0.6이어야 합니다.");
            }
            GeneratorRules g = Generator;
            if (g == null) p.Add("generator가 없습니다.");
            else
            {
                if (g.FormationMinSize < 2 || g.FormationMaxSize < g.FormationMinSize) p.Add("generator 지형 크기는 2 ≤ formationMinSize ≤ formationMaxSize 이어야 합니다.");
                foreach ((string name, double v) in new[]
                         {
                             ("mountainChance", g.MountainChance), ("mountainTurnChance", g.MountainTurnChance),
                             ("isolatedObstacleChance", g.IsolatedObstacleChance), ("oreNearObstacleChance", g.OreNearObstacleChance),
                             ("bedrockInObstacleChance", g.BedrockInObstacleChance),
                         })
                {
                    if (v < 0 || v > 1) p.Add($"generator.{name}는 0~1 확률이어야 합니다.");
                }
                if (g.MaxAttempts < 1) p.Add("generator.maxAttempts는 1 이상이어야 합니다.");
            }
            if (RegionChoices < 1) p.Add("regionChoices는 1 이상이어야 합니다.");
            if (Modifiers == null) p.Add("modifiers가 없습니다.");
            else p.AddRange(Modifiers.Validate());
            return p;
        }
    }

    /// <summary>
    /// 난이도 곡선의 조절값. D = 0(쉬움)과 D = 1(목표: 숙련 플레이어가 겨우 흑자) 두 끝값을 정하고,
    /// D &gt; 1은 같은 기울기로 계속 늘린다(상한 없음). 각 값에는 하한·상한이 있다.
    /// 우물·광맥은 '반경 3 맵(37칸)당 개수'로 정해 맵 칸 수 비율만큼 늘린다.
    /// 기본값: 지형 곡선은 Tools/SettlementSim '-- tune'(후보 E), 자금 기준·정착지 항은 '-- mods campaign 0.2 14000'(장비·모디파이어 포함 봇 캠페인) 조정 결과.
    /// </summary>
    public sealed class DifficultyCurve
    {
        /// <summary>D = max(0, (보유 자금 − CashAtD0) / CashPerD) + DPerSettlement × (지나온 생성 맵 정착지 수). 상한 없음.</summary>
        public double CashAtD0 { get; set; } = 1000;
        /// <summary>장비·모디파이어를 쓰는 숙련 봇이 약 5번째 정착지에서 D=1, 13~18번째(D 3~4)에서 첫 적자가 나도록 정착지 항과 함께 조정한 값.</summary>
        public double CashPerD { get; set; } = 14000;
        /// <summary>
        /// 생성 맵 정착지마다 D에 더하는 값(자금과 별개로 정착지 수로도 난이도가 오른다).
        /// 첫 생성 맵 정착지는 0, 그다음부터 1개마다 이만큼. 수익이 줄어도 난이도가 계속 올라 결국 적자에 닿게 한다.
        /// </summary>
        public double DPerSettlement { get; set; } = 0.25; // 결정 F5
        /// <summary>생성 맵의 유지비 배율 = 1 + 이 값 × D(먼 지역일수록 운영비가 비싸짐, 결정 H1). 0이면 끔.</summary>
        /// <summary>운영비 배율 계산에 쓰는 D의 하한(0이면 끔). 예: 0.6이면 첫 정착지부터 운영비 × (1 + UpkeepPerD × 0.6).</summary>
        public double UpkeepMinD { get; set; }
        public double UpkeepPerD { get; set; } = 0.5; // 결정 H1·FL3

        // 후반 가속(결정 LD1~LD5): D가 LateStartD를 넘은 만큼(x = max(0, D − LateStartD)) 운영비·약점·재해를 더 세게. 0이면 끔.
        /// <summary>후반 가속이 시작되는 D.</summary>
        public double LateStartD { get; set; } = 4;
        /// <summary>운영비 배율에 더하는 제곱항: 1 + UpkeepPerD × D + UpkeepLatePerD2 × x².</summary>
        public double UpkeepLatePerD2 { get; set; } = 0.2; // 결정 LD4
        /// <summary>약점 효과 세기 배수 = 1 + WeaknessLatePerD × x(1에서 벗어난 만큼을 배수만큼, 예: 무른 지반 +25% → 배수 2면 +50%). 0이면 약점 가속 없음(결정 LD5).</summary>
        public double WeaknessLatePerD { get; set; } = 0;
        /// <summary>재해 세기 배수 = 1 + DisasterLatePerD × x(지속 시간·피해, <see cref="DesertSettlement.Core.Economy.DisasterManager.Intensity"/>).</summary>
        public double DisasterLatePerD { get; set; } = 0.5;

        /// <summary>x = max(0, D − LateStartD).</summary>
        public double LateExcess(double d) => Math.Max(0, d - LateStartD);

        /// <summary>반경: D &lt; FirstRadiusStepD이면 BaseRadius, 이후 RadiusStepD마다 +1, 최대 MaxRadius(HexMap 한도 7).</summary>
        public int BaseRadius { get; set; } = 3;
        /// <summary>D=1 전에는 반경 3 유지: 반경이 커질 때 광맥 수가 늘어 난이도 순서가 뒤집히는 것을 막는다.</summary>
        public double FirstRadiusStepD { get; set; } = 1.0;
        public double RadiusStepD { get; set; } = 1.0;
        public int MaxRadius { get; set; } = 7;

        /// <summary>37칸당 우물 수: D0 → D1, 이후 같은 기울기. 37칸당 하한 MinWellsPer37, 맵 전체 최소 1.</summary>
        public double WellsPer37AtD0 { get; set; } = 2; // 결정 R3
        public double WellsPer37AtD1 { get; set; } = 1;
        public double MinWellsPer37 { get; set; } = 0.3;

        /// <summary>37칸당 광맥 수: D0 → D1, 이후 같은 기울기. 37칸당 하한 MinOresPer37, 맵 전체 최소 1.</summary>
        public double OresPer37AtD0 { get; set; } = 1.5; // 결정 R3
        public double OresPer37AtD1 { get; set; } = 0.9;
        public double MinOresPer37 { get; set; } = 0.3;

        /// <summary>장애물 비율: D0 → D1, 이후 같은 기울기, 상한 MaxObstacleDensity(맵을 막지 않는 한도).</summary>
        public double ObstacleAtD0 { get; set; } = 0.20; // 결정 R3
        public double ObstacleAtD1 { get; set; } = 0.30;
        public double MaxObstacleDensity { get; set; } = 0.40;

        /// <summary>가장 가까운 우물–광맥 거리 목표: D0 → D1, 이후 같은 기울기, 상한 = 반경.</summary>
        public double DistanceAtD0 { get; set; } = 3; // 결정 R3
        public double DistanceAtD1 { get; set; } = 3.5; // 결정 LD6

        public DifficultyCurve Clone() => (DifficultyCurve)MemberwiseClone();
    }

    /// <summary>생성 규칙의 확률·크기(사용자 규칙 + 제안 확정값, Docs/implementation_notes.md '절차 맵').</summary>
    public sealed class GeneratorRules
    {
        public int FormationMinSize { get; set; } = 3;
        public int FormationMaxSize { get; set; } = 6;
        /// <summary>장애물 지형마다 산맥(선)일 확률. 나머지는 고원(덩어리).</summary>
        public double MountainChance { get; set; } = 0.5;
        /// <summary>산맥이 한 칸 나아갈 때 60° 꺾일 확률.</summary>
        public double MountainTurnChance { get; set; } = 0.25;
        /// <summary>맵마다 한 번: 독립된 한 칸 장애물이 생길 확률.</summary>
        public double IsolatedObstacleChance { get; set; } = 0.15;
        public double OreNearObstacleChance { get; set; } = 0.9;
        public double BedrockInObstacleChance { get; set; } = 0.9;
        /// <summary>BSP 구역의 최소 타일 수.</summary>
        public int MinLeafTiles { get; set; } = 5;
        /// <summary>우물 반경 이 거리 안에는 다른 우물이 없다.</summary>
        public int WellExclusionRadius { get; set; } = 2;
        /// <summary>우물 주변 6칸 중 최소 사막 칸 수(농장·경매장 자리).</summary>
        public int MinDesertAroundWell { get; set; } = 2;
        public int MinOreSpacing { get; set; } = 2;
        public int MinDesertTiles { get; set; } = 8;
        /// <summary>규칙을 못 맞추면 처음부터 다시 만드는 최대 횟수.</summary>
        public int MaxAttempts { get; set; } = 300;
    }
}
