using System;
using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Economy;

namespace DesertSettlement.Core.Progression
{
    public enum RegionModifierKind { Weakness, Strength }

    /// <summary>지역 모디파이어 하나(약점 또는 강점). 목록은 procedural_map_config.json의 modifiers.definitions.</summary>
    public sealed class RegionModifierDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public RegionModifierKind Kind { get; set; }
        /// <summary>UI에 보일 한 줄 설명(분위기 문구). 수치 요약은 Effects.Describe().</summary>
        public string Description { get; set; }
        public EconomyEffects Effects { get; set; } = new EconomyEffects();
    }

    /// <summary>
    /// 지역 모디파이어 출현 규칙(2026-10-23 확정):
    /// 약점은 D ≥ WeaknessStartD부터 1개, 이후 WeaknessStepD마다 1개씩 늘고(같은 약점은 중복 없음),
    /// 강점은 StrengthChance 확률로 1개 붙는다.
    /// </summary>
    public sealed class RegionModifierSettings
    {
        public double WeaknessStartD { get; set; } = 0.5;
        public double WeaknessStepD { get; set; } = 1.0;
        public double StrengthChance { get; set; } = 0.4;
        public List<RegionModifierDefinition> Definitions { get; set; } = new List<RegionModifierDefinition>();

        /// <summary>난이도 d의 약점 개수(약점 종류 수를 넘지 않음).</summary>
        public int WeaknessCount(double d)
        {
            if (d < WeaknessStartD) return 0;
            int n = 1 + (int)Math.Floor((d - WeaknessStartD) / WeaknessStepD + 1e-9);
            return Math.Min(n, Definitions.Count(x => x.Kind == RegionModifierKind.Weakness));
        }

        /// <summary>
        /// 난이도 d·시드로 모디파이어를 고른다(같은 입력 = 같은 결과). 약점 먼저, 그다음 강점.
        /// exclude에 있는 약점은 가능하면 피한다(다음 지역 후보 두 곳이 서로 다른 약점을 갖게 할 때 쓴다).
        /// </summary>
        public List<RegionModifierDefinition> Roll(double d, int seed, ICollection<string> exclude = null)
        {
            var rng = new DeterministicRandom(unchecked(seed * 7 + 0x5EED));
            var result = new List<RegionModifierDefinition>();
            List<RegionModifierDefinition> weaknesses = Definitions.Where(x => x.Kind == RegionModifierKind.Weakness).ToList();
            int count = WeaknessCount(d);
            // 피할 약점이 아닌 것 → 피할 약점 순서로 섞어서 앞에서부터 고른다.
            List<RegionModifierDefinition> order = weaknesses.OrderBy(_ => rng.Next()).ToList();
            if (exclude != null && exclude.Count > 0)
                order = order.Where(x => !exclude.Contains(x.Id)).Concat(order.Where(x => exclude.Contains(x.Id))).ToList();
            result.AddRange(order.Take(count));

            List<RegionModifierDefinition> strengths = Definitions.Where(x => x.Kind == RegionModifierKind.Strength).ToList();
            if (strengths.Count > 0 && rng.NextDouble() < StrengthChance) result.Add(strengths[rng.Next(strengths.Count)]);
            return result;
        }

        public List<string> Validate()
        {
            var p = new List<string>();
            if (WeaknessStepD <= 0) p.Add("modifiers.weaknessStepD는 0보다 커야 합니다.");
            if (StrengthChance < 0 || StrengthChance > 1) p.Add("modifiers.strengthChance는 0~1 확률이어야 합니다.");
            if (Definitions == null) { p.Add("modifiers.definitions가 없습니다."); return p; }
            foreach (RegionModifierDefinition m in Definitions)
            {
                if (string.IsNullOrEmpty(m.Id)) p.Add("modifiers.definitions에 id 없는 항목이 있습니다.");
                if (m.Effects == null) p.Add($"modifiers.{m.Id}.effects가 없습니다.");
            }
            if (Definitions.Select(m => m.Id).Distinct().Count() != Definitions.Count) p.Add("modifiers.definitions의 id가 겹칩니다.");
            return p;
        }
    }
}
