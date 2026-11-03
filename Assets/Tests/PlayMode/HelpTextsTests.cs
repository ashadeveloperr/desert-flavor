using System;
using System.Linq;
using DesertSettlement.Core.Economy;
using DesertSettlement.Runtime.UI;
using NUnit.Framework;

namespace DesertSettlement.PlayMode.Tests
{
    /// <summary>도구 막대 아이콘·툴팁 안내(결정 HT3)가 모든 시설을 덮는지. 빠뜨리면 버튼에 엉뚱한 아이콘·빈 툴팁이 조용히 뜬다.</summary>
    public class HelpTextsTests
    {
        [Test]
        public void EveryFacility_HasIconAndDescription()
        {
            string[] missing = FacilityCatalog.All.Select(t => t.Kind).Where(k => !HelpTexts.Facilities.ContainsKey(k)).ToArray();
            Assert.That(missing, Is.Empty, "HelpTexts.Facilities에 없는 시설");
            foreach (var pair in HelpTexts.Facilities)
            {
                Assert.That(FacilityCatalog.Get(pair.Key), Is.Not.Null, $"카탈로그에 없는 시설 {pair.Key}");
                Assert.That(pair.Value.Role, Is.Not.Empty, pair.Key);
                Assert.That(pair.Value.Use, Is.Not.Empty, pair.Key);
            }
        }

        [Test]
        public void EveryFacility_HasDistinctIcon()
        {
            var icons = HelpTexts.Facilities.Values.Select(e => e.Icon).ToList();
            Assert.That(icons.Distinct().Count(), Is.EqualTo(icons.Count), "시설 아이콘이 겹친다");
        }

        [Test]
        public void EveryIcon_Draws()
        {
            foreach (Icons.Kind kind in Enum.GetValues(typeof(Icons.Kind)))
            {
                var sprite = Icons.Get(kind);
                Assert.That(sprite, Is.Not.Null, kind.ToString());
                // 그림이 비어 있지 않은지(새 종류를 enum에만 더하고 그리기를 빠뜨린 경우).
                Assert.That(sprite.texture.GetPixels().Any(c => c.a > 0.5f), Is.True, $"{kind} 아이콘이 비어 있다");
            }
        }

        [Test]
        public void HelpText_ListsHotkeys()
        {
            string help = Hotkeys.HelpText();
            foreach (string key in new[] { "F5", "F9", "`", "Q · E", "Esc" }) StringAssert.Contains(key, help);
        }
    }
}
