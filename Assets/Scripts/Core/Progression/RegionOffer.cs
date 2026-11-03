using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Economy;

namespace DesertSettlement.Core.Progression
{
    /// <summary>
    /// 상점에서 고를 다음 지역 후보 하나. 개략 조건(난이도 수치·모디파이어)만 보이고 지형 배치는 들어가야 만들어진다.
    /// 고정 맵 차례면 FixedMap이 있고 Params·Modifiers는 비어 있다.
    /// </summary>
    public sealed class RegionOffer
    {
        public int Index { get; internal set; }
        /// <summary>생성 시드(같은 후보 = 같은 지형).</summary>
        public int Seed { get; internal set; }
        public DifficultyParams Params { get; internal set; }
        public MapDefinition FixedMap { get; internal set; }
        public IReadOnlyList<RegionModifierDefinition> Modifiers { get; internal set; } = new List<RegionModifierDefinition>();

        public bool IsFixedMap => FixedMap != null;
        public IEnumerable<RegionModifierDefinition> Weaknesses => Modifiers.Where(m => m.Kind == RegionModifierKind.Weakness);
        public IEnumerable<RegionModifierDefinition> Strengths => Modifiers.Where(m => m.Kind == RegionModifierKind.Strength);

        /// <summary>모디파이어 효과를 합친 것.</summary>
        public EconomyEffects Effects => EconomyEffects.Combine(Modifiers.Select(m => m.Effects));
    }
}
