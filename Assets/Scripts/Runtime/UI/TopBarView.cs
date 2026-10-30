using System;
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
    /// 위쪽 띠(UI2): 왼쪽 자원(자금·식량·석재·주민과 일손(노동력이 꺼져 있으면 식량 계수) + 지난 1시간 변화), 가운데 정착지·지역 특성·장비,
    /// 오른쪽 시계·일시정지·배속(` 1 2 3) | 저장(F5)·불러오기(F9).
    /// 좁은 화면에서는 세 구역이 같은 비율로 줄어든다(LY3, 16:10까지는 그대로).
    /// </summary>
    public sealed class TopBarView : IUiView
    {
        // 구역 폭(1920×1080 기준). 가운데는 화면 중앙이라 '화면 절반 − 가운데 절반'에 왼쪽·오른쪽 중 넓은 쪽이 들어가야 한다.
        private const float LeftWidth = 624, CenterWidth = 500, Margin = 8;
        private readonly GameUi _ui;
        private readonly RectTransform _left, _right;
        private readonly Text _cash, _food, _stone, _coef, _delta, _place, _traits, _clock, _message;
        private readonly Button _pause, _save, _load;
        private readonly Button[] _speeds = new Button[3];
        private float _messageUntil;
        private long _shownMinute = -1;

        public TopBarView(GameUi ui, Transform canvas)
        {
            _ui = ui;
            RectTransform bar = Panel("Top Bar", canvas).Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 64));

            // 왼쪽: 자원
            HorizontalLayoutGroup res = Row(bar, 12, 8);
            _left = ((RectTransform)res.transform).Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(LeftWidth, 0));
            // 칸 폭(2026-10-27 이슈 #5): '식량 계수 100 %'가 140에서 두 줄로 넘쳐 자금 칸을 줄이고 계수 칸을 늘림(합계 570 + 간격 36 + 여백 16 = 622).
            _cash = Resource(res.transform, "자금", 120);
            _food = Resource(res.transform, "식량", 140);
            _stone = Resource(res.transform, "석재", 140);
            _coef = Resource(res.transform, "식량 계수", 170); // 노동력이 켜져 있으면 '주민 n 일손 a/b'
            _delta = Label(bar, "", Theme.Small, Theme.Muted);
            _delta.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(16, 2), new Vector2(640, 18));

            // 가운데: 정착지·지역 특성
            _place = Label(bar, "", Theme.Title, Theme.Accent, TextAnchor.UpperCenter);
            _place.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(CenterWidth, 22));
            _traits = Label(bar, "", Theme.Small, Theme.Muted, TextAnchor.UpperCenter);
            _traits.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(CenterWidth, 32));

            // 오른쪽: 시계·배속 묶음 | 저장·불러오기 묶음(2026-10-27 LY1, 이슈 #5: 구분선으로 떼어 배속을 누르다 저장을 잘못 누르지 않게).
            // 폭은 내용에 맞춘다(전에는 560 고정 안에 약 600이 들어가 가운데로 넘쳤다).
            HorizontalLayoutGroup right = Row(bar, 12, 8);
            right.childAlignment = TextAnchor.MiddleRight;
            _right = ((RectTransform)right.transform).Place(new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-8, 0), new Vector2(0, 0));
            right.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            Transform time = Row(right.transform, 4).transform;
            _clock = Label(time, "", Theme.Title, Theme.Text, TextAnchor.MiddleRight);
            _clock.gameObject.AddComponent<LayoutElement>().preferredWidth = 150;
            _pause = Btn(time, "일시정지 `", () => { _ui.Session.Clock.TogglePause(); _ui.RefreshNow(); }, 96);
            for (int i = 0; i < _speeds.Length; i++)
            {
                int index = i;
                _speeds[i] = Btn(time, "", () => { _ui.Session.Clock.SelectSpeed(index); _ui.RefreshNow(); }, 46);
            }
            Divider(right.transform);
            Transform file = Row(right.transform, 4).transform;
            _save = Btn(file, "저장 F5", () => Show(_ui.Session.SaveGame()), 76);
            _load = Btn(file, "불러오기 F9", () => Show(_ui.Session.LoadGame()), 104);

            _message = Label(canvas, "", Theme.Body, Theme.Accent, TextAnchor.UpperRight);
            _message.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -70), new Vector2(520, 24));
        }

        private static Text Resource(Transform parent, string name, float width)
        {
            Text t = Label(parent, name, Theme.Big);
            t.gameObject.AddComponent<LayoutElement>().preferredWidth = width;
            return t;
        }

        public void Show(string message)
        {
            _message.text = message;
            _messageUntil = Time.unscaledTime + 4f;
        }

        /// <summary>새 정착지: 일시정지로 시작한다는 안내(2026-10-23 플레이 피드백 CK2).</summary>
        public void Reset()
        {
            _shownMinute = -1;
            Refresh();
            if (_ui.Session.Clock != null && _ui.Session.Clock.IsPaused) Show("일시정지 상태로 시작합니다 — 맵을 살펴본 뒤 ` 키나 배속 버튼으로 시작하세요");
        }

        /// <summary>
        /// 시계 글자는 매 프레임 확인하고 게임 시간 1분이 바뀔 때마다 다시 쓴다(CK1). 0.2초 갱신에 묶으면 1배속에서도 1~2분씩 건너뛴다.
        /// 4배속(1초 = 게임 32분)은 화면 프레임보다 빨라 몇 분씩 넘어갈 수 있다.
        /// </summary>
        public void TickClock()
        {
            GameClock clock = _ui.Session.Clock;
            if (clock == null) return;
            int minuteOfDay = Math.Min(24 * 60 - 1, (int)Math.Floor(clock.HourOfDay * 60.0));
            long minute = (clock.Day - 1) * 24L * 60L + minuteOfDay;
            bool day = clock.IsDaytime;
            if (minute == _shownMinute) return;
            _shownMinute = minute;
            int hour = minuteOfDay / 60;
            int m = minuteOfDay % 60;
            _clock.text = $"{clock.Day}일차 {hour:00}:{m:00} {(day ? "낮" : Paint("밤", Theme.Water))}";
        }

        public void Refresh()
        {
            GameSession session = _ui.Session;
            Settlement s = session.Settlement;
            Campaign c = session.Campaign;
            if (s == null || c == null) return;

            _cash.text = $"{Paint("자금", Theme.Muted)} {Paint(s.Cash.ToString("0"), s.Cash < 0 ? Theme.Danger : Theme.Text)}";
            _food.text = $"{Paint("식품", Theme.Muted)} {s.PantryUsed:0}/{s.FoodCapacity:0}"; // 식품 창고(결정 FD5), 재화별은 왼쪽 패널 원형 그래프
            _stone.text = $"{Paint("석재", Theme.Muted)} {s.Stone:0}/{s.StoneCapacity:0}";
            if (s.Rules.LaborOn)
            {
                // 노동력(결정 LB1~): 주민 수와 일손(배치/필요). 모자라면 노랑, 주민이 떠나는 중이면 빨강.
                LaborAssignment labor = s.LastLabor;
                bool short_ = labor.Assigned + 0.05 < labor.Needed;
                Color handsColor = s.FoodSatisfaction < 0.999 ? Theme.Danger : short_ ? Theme.Warn : Theme.Good;
                _coef.text = $"{Paint("주민", Theme.Muted)} {s.Residents:0} {Paint($"일 {labor.Assigned:0}/{labor.Needed:0}", handsColor)}";
            }
            else
            {
                Color coefColor = s.FoodCoefficient >= 0.95 ? Theme.Good : s.FoodCoefficient >= 0.7 ? Theme.Warn : Theme.Danger;
                _coef.text = $"{Paint("식량 계수", Theme.Muted)} {Paint(s.FoodCoefficient.ToString("P0"), coefColor)}";
            }
            HourRecord last = s.History.LastOrDefault();
            _delta.text = last == null ? "" :
                $"지난 1시간  식량 +{last.FoodProduced:0.0} −{last.FoodDemand:0.0}   석재 +{last.StoneProduced:0.0}   광물 +{last.OreProduced:0.0}   유지비 −{last.Upkeep:0.0}" +
                (s.Rules.LaborOn ? $"   일손 배치 {s.LastLabor.Assigned:0}/{s.LastLabor.Needed:0} · 남는 주민 {s.LastLabor.IdleResidents:0}" : "");

            // 생성 맵 이름에는 시드가 들어 있어(맵 스케치 미리보기용) 화면에는 난이도만 보인다(UX4).
            string place = c.CurrentGenerated != null ? $"생성 지역 · 난이도 {c.CurrentGenerated.Params.D:0.00}" : c.CurrentMap.Name;
            _place.text = $"정착지 {c.SettlementNumber} · {place}";
            string traits = string.Join("   ", c.CurrentModifiers.Select(m =>
                Paint((m.Kind == RegionModifierKind.Weakness ? "약점 " : "강점 ") + m.Name, m.Kind == RegionModifierKind.Weakness ? Theme.Warn : Theme.Good)));
            string gear = c.Loadout.Count == 0 ? "장비 없음" : "장비 " + string.Join(", ", c.Shop.Where(e => c.IsEquipped(e.Id)).Select(e => e.Name));
            // 운영비 배율(장비·약점·난이도 H1 포함): 먼 지역일수록 비싸진다.
            string upkeep = s.Rules.UpkeepMultiplier > 1.005 ? "   " + Paint($"운영비 ×{s.Rules.UpkeepMultiplier:0.00}", Theme.Warn) : "";
            _traits.text = (traits.Length > 0 ? traits + "\n" : "") + gear + upkeep;

            GameClock clock = session.Clock;
            TickClock();
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
            FitToWidth();
        }

        /// <summary>좁은 화면 축소(LY3). 오른쪽 묶음 폭은 내용에 따라 달라서 매번 잰다.</summary>
        private void FitToWidth()
        {
            float side = Math.Max(LeftWidth, LayoutUtility.GetPreferredWidth(_right));
            float s = FitScale(_ui.CanvasWidth / 2f - 2 * Margin, CenterWidth / 2f + side + Margin);
            foreach (RectTransform r in new[] { _left, _right, _delta.rectTransform, _place.rectTransform, _traits.rectTransform }) SetScale(r, s);
        }
    }
}
