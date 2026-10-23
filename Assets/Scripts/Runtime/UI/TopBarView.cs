using System.Linq;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Progression;
using DesertSettlement.Core.Timing;
using UnityEngine;
using UnityEngine.UI;
using static DesertSettlement.Runtime.UI.UiKit;

namespace DesertSettlement.Runtime.UI
{
    /// <summary>
    /// 위쪽 띠(UI2): 왼쪽 자원(자금·식량·석재·식량 계수 + 지난 1시간 변화), 가운데 정착지·지역 특성·장비,
    /// 오른쪽 시계·일시정지·배속(` 1 2 3)·저장(F5)·불러오기(F9).
    /// </summary>
    public sealed class TopBarView : IUiView
    {
        private readonly GameUi _ui;
        private readonly Text _cash, _food, _stone, _coef, _delta, _place, _traits, _clock, _message;
        private readonly Button _pause, _save, _load;
        private readonly Button[] _speeds = new Button[3];
        private float _messageUntil;

        public TopBarView(GameUi ui, Transform canvas)
        {
            _ui = ui;
            RectTransform bar = Panel("Top Bar", canvas).Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 64));

            // 왼쪽: 자원
            HorizontalLayoutGroup res = Row(bar, 18, 8);
            ((RectTransform)res.transform).Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(620, 0));
            _cash = Resource(res.transform, "자금");
            _food = Resource(res.transform, "식량");
            _stone = Resource(res.transform, "석재");
            _coef = Resource(res.transform, "식량 계수");
            _delta = Label(bar, "", Theme.Small, Theme.Muted);
            _delta.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(16, 2), new Vector2(640, 18));

            // 가운데: 정착지·지역 특성
            _place = Label(bar, "", Theme.Title, Theme.Accent, TextAnchor.UpperCenter);
            _place.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(560, 22));
            _traits = Label(bar, "", Theme.Small, Theme.Muted, TextAnchor.UpperCenter);
            _traits.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(620, 32));

            // 오른쪽: 시계·배속·저장
            HorizontalLayoutGroup right = Row(bar, 4, 8);
            right.childAlignment = TextAnchor.MiddleRight;
            ((RectTransform)right.transform).Place(new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-8, 0), new Vector2(560, 0));
            _clock = Label(right.transform, "", Theme.Title, Theme.Text, TextAnchor.MiddleRight);
            _clock.gameObject.AddComponent<LayoutElement>().preferredWidth = 190;
            _pause = Btn(right.transform, "일시정지 `", () => { _ui.Session.Clock.TogglePause(); _ui.RefreshNow(); }, 96);
            for (int i = 0; i < _speeds.Length; i++)
            {
                int index = i;
                _speeds[i] = Btn(right.transform, "", () => { _ui.Session.Clock.SelectSpeed(index); _ui.RefreshNow(); }, 46);
            }
            _save = Btn(right.transform, "저장 F5", () => Show(_ui.Session.SaveGame()), 70);
            _load = Btn(right.transform, "불러오기 F9", () => Show(_ui.Session.LoadGame()), 84);

            _message = Label(canvas, "", Theme.Body, Theme.Accent, TextAnchor.UpperRight);
            _message.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -70), new Vector2(520, 24));
        }

        private static Text Resource(Transform parent, string name)
        {
            Text t = Label(parent, name, Theme.Big);
            t.gameObject.AddComponent<LayoutElement>().preferredWidth = 140;
            return t;
        }

        public void Show(string message)
        {
            _message.text = message;
            _messageUntil = Time.unscaledTime + 4f;
        }

        public void Reset() => Refresh();

        public void Refresh()
        {
            GameSession session = _ui.Session;
            Settlement s = session.Settlement;
            Campaign c = session.Campaign;
            if (s == null || c == null) return;

            _cash.text = $"{Paint("자금", Theme.Muted)} {Paint(s.Cash.ToString("0"), s.Cash < 0 ? Theme.Danger : Theme.Text)}";
            _food.text = $"{Paint("식량", Theme.Muted)} {s.Food:0}/{s.FoodCapacity:0}";
            _stone.text = $"{Paint("석재", Theme.Muted)} {s.Stone:0}/{s.StoneCapacity:0}";
            Color coefColor = s.FoodCoefficient >= 0.95 ? Theme.Good : s.FoodCoefficient >= 0.7 ? Theme.Warn : Theme.Danger;
            _coef.text = $"{Paint("식량 계수", Theme.Muted)} {Paint(s.FoodCoefficient.ToString("P0"), coefColor)}";
            HourRecord last = s.History.LastOrDefault();
            _delta.text = last == null ? "" :
                $"지난 1시간  식량 +{last.FoodProduced:0.0} −{last.FoodDemand:0.0}   석재 +{last.StoneProduced:0.0}   광물 +{last.OreProduced:0.0}   유지비 −{last.Upkeep:0.0}";

            _place.text = $"정착지 {c.SettlementNumber} · {c.CurrentMap.Name}";
            string traits = string.Join("   ", c.CurrentModifiers.Select(m =>
                Paint((m.Kind == RegionModifierKind.Weakness ? "약점 " : "강점 ") + m.Name, m.Kind == RegionModifierKind.Weakness ? Theme.Warn : Theme.Good)));
            string gear = c.Loadout.Count == 0 ? "장비 없음" : "장비 " + string.Join(", ", c.Shop.Where(e => c.IsEquipped(e.Id)).Select(e => e.Name));
            // 운영비 배율(장비·약점·난이도 H1 포함): 먼 지역일수록 비싸진다.
            string upkeep = s.Rules.UpkeepMultiplier > 1.005 ? "   " + Paint($"운영비 ×{s.Rules.UpkeepMultiplier:0.00}", Theme.Warn) : "";
            _traits.text = (traits.Length > 0 ? traits + "\n" : "") + gear + upkeep;

            GameClock clock = session.Clock;
            int hour = (int)clock.HourOfDay;
            int minute = (int)((clock.HourOfDay - hour) * 60.0);
            _clock.text = $"{clock.Day}일차 {hour:00}:{minute:00} {(clock.IsDaytime ? "낮" : Paint("밤", Theme.Water))}";
            _pause.SetText(clock.IsPaused ? "재개 `" : "일시정지 `");
            _pause.SetOn(clock.IsPaused);
            for (int i = 0; i < _speeds.Length; i++)
            {
                bool exists = i < clock.SpeedStepCount;
                _speeds[i].gameObject.SetActive(exists);
                if (!exists) continue;
                _speeds[i].SetText($"{clock.SpeedAt(i):0.#}x");
                _speeds[i].SetOn(!clock.IsPaused && clock.SpeedIndex == i);
            }
            _load.interactable = session.HasSave;
            if (Time.unscaledTime > _messageUntil) _message.text = "";
        }
    }
}
