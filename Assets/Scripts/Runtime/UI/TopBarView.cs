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
    /// 위쪽 띠(UI2): 왼쪽 자원 카드(결정 UV3, Frostpunk 식 — 아이콘 + 큰 숫자 + 시간당 변화, 위험하면 붉게 깜빡임):
    /// 자금(유지비·버틸 시간)·식품(순 식량·버틸 시간)·석재(생산·한도)·주민(정원·일손, 노동력이 꺼져 있으면 식량 계수)·광물(경매 가치), 가운데 정착지·지역 특성·받은 영구 모디파이어,
    /// 오른쪽 시계·일시정지·배속(` 1 2 3) | 저장(F5)·불러오기(F9).
    /// 자원 카드에 마우스를 올리면 설명 툴팁(<see cref="HelpTexts"/>, 결정 HT4).
    /// 좁은 화면에서는 세 구역이 같은 비율로 줄어든다(LY3, 16:10까지는 그대로).
    /// </summary>
    public sealed class TopBarView : IUiView
    {
        // 구역 폭(1920×1080 기준). 가운데는 화면 중앙이라 '화면 절반 − 가운데 절반'에 왼쪽·오른쪽 중 넓은 쪽이 들어가야 한다.
        private const float LeftWidth = 700, CenterWidth = 500, Margin = 8;
        private const float CardHeight = 54;
        private readonly GameUi _ui;
        private readonly RectTransform _left, _right;
        private readonly Card _cash, _food, _stone, _people, _ore;
        private readonly Text _place, _traits, _clock, _message;
        private readonly Button _pause, _save, _load;
        private readonly Button[] _speeds = new Button[3];
        private float _messageUntil;
        private long _shownMinute = -1;

        public TopBarView(GameUi ui, Transform canvas)
        {
            _ui = ui;
            RectTransform bar = Panel("Top Bar", canvas).Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 64));

            // 왼쪽: 자원
            HorizontalLayoutGroup res = Row(bar, 6, 5);
            res.childAlignment = TextAnchor.MiddleLeft;
            _left = ((RectTransform)res.transform).Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(4, 0), new Vector2(LeftWidth, 0));
            // 카드 폭 합계 160 + 150 + 136 + 136 + 90 = 672 + 간격 24 = 696(가운데 구역 왼쪽 끝 710 안).
            _cash = new Card(res.transform, Icons.Kind.Cash, 160);
            _food = new Card(res.transform, Icons.Kind.Food, 150);
            _stone = new Card(res.transform, Icons.Kind.Stone, 136);
            _people = new Card(res.transform, Icons.Kind.People, 136);
            _ore = new Card(res.transform, Icons.Kind.Ore, 90);
            // 마우스를 올리면 자원 설명(결정 HT4).
            _cash.Root.Tip(() => HelpTexts.CashTip(_ui.Session.Settlement));
            _food.Root.Tip(() => HelpTexts.FoodTip(_ui.Session.Settlement));
            _stone.Root.Tip(() => HelpTexts.StoneTip(_ui.Session.Settlement));
            _people.Root.Tip(() => HelpTexts.PeopleTip(_ui.Session.Settlement));
            _ore.Root.Tip(() => HelpTexts.OreTip(_ui.Session.Settlement));

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

        /// <summary>자원 카드 하나: 아이콘 + 큰 숫자 + 아랫줄(변화·이유). 위험하면 바탕이 붉게 깜빡인다.</summary>
        private sealed class Card
        {
            private static readonly Color DangerBack = new Color(0.55f, 0.13f, 0.10f, 0.95f);
            private static readonly Color WarnBack = new Color(0.36f, 0.25f, 0.10f, 0.95f);
            private readonly Image _back;
            private readonly Text _value, _sub;
            private int _level;

            /// <summary>카드 바탕(툴팁을 붙이는 곳).</summary>
            public RectTransform Root { get; }

            public Card(Transform parent, Icons.Kind icon, float width)
            {
                RectTransform r = Panel("Card " + icon, parent, Theme.PanelLight);
                Root = r;
                var le = r.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = le.minWidth = width;
                le.preferredHeight = le.minHeight = CardHeight;
                _back = r.GetComponent<Image>();
                HorizontalLayoutGroup row = Row(r, 6, 4);
                Stretch((RectTransform)row.transform, Vector2.zero);
                row.childAlignment = TextAnchor.MiddleLeft;
                RectTransform pic = Rect("Icon", row.transform);
                Image img = pic.gameObject.AddComponent<Image>();
                img.sprite = Icons.Get(icon);
                img.preserveAspect = true;
                img.raycastTarget = false;
                var ple = pic.gameObject.AddComponent<LayoutElement>();
                ple.preferredWidth = ple.minWidth = ple.preferredHeight = ple.minHeight = 36;
                VerticalLayoutGroup col = Column(row.transform, 0);
                col.childAlignment = TextAnchor.MiddleLeft;
                col.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                _value = Label(col.transform, "", Theme.Huge, Theme.Text);
                _value.fontStyle = FontStyle.Bold;
                _value.horizontalOverflow = HorizontalWrapMode.Overflow;
                _sub = Label(col.transform, "", Theme.Small, Theme.Muted);
                _sub.horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            /// <summary>값·아랫줄·상태(0 = 보통, 1 = 주의, 2 = 위험). 위험이면 바탕이 깜빡인다(<see cref="Pulse"/>).</summary>
            public void Set(string value, string sub, int level, Color? valueColor = null)
            {
                _value.text = value;
                _sub.text = sub;
                _value.color = valueColor ?? (level >= 2 ? Theme.Danger : level == 1 ? Theme.Warn : Theme.Text);
                _level = level;
                Pulse();
            }

            /// <summary>매 프레임: 위험 카드의 바탕을 깜빡인다.</summary>
            public void Pulse()
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
                _back.color = _level >= 2 ? Color.Lerp(Theme.PanelLight, DangerBack, pulse) : _level == 1 ? WarnBack : Theme.PanelLight;
            }
        }

        /// <summary>매 프레임(GameUi): 위험 카드 깜빡임.</summary>
        public void Pulse()
        {
            _cash.Pulse(); _food.Pulse(); _stone.Pulse(); _people.Pulse(); _ore.Pulse();
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

            RefreshResources(s);

            // 생성 맵 이름에는 시드가 들어 있어(맵 스케치 미리보기용) 화면에는 난이도만 보인다(UX4).
            string place = c.CurrentGenerated != null ? $"생성 지역 · 난이도 {c.CurrentGenerated.Params.D:0.00}" : c.CurrentMap.Name;
            _place.text = $"정착지 {c.SettlementNumber} · {place}";
            string traits = string.Join("   ", c.CurrentModifiers.Select(m =>
                Paint((m.Kind == RegionModifierKind.Weakness ? "약점 " : "강점 ") + m.Name, m.Kind == RegionModifierKind.Weakness ? Theme.Warn : Theme.Good)));
            string gear = c.Modifiers.Count == 0 ? "영구 모디파이어 없음" : $"영구 모디파이어 {c.Modifiers.Count}개 — {c.ModifierEffects.Describe()}";
            // 운영비 배율(영구 모디파이어·약점·난이도 H1 포함): 먼 지역일수록 비싸진다.
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

        /// <summary>
        /// 자원 카드(결정 UV3). 시간당 변화는 직전 정산 기록. '버틸 시간'은 지금 줄어드는 속도로 0이 되기까지(하루 안이면 주의, 12시간 안이면 위험).
        /// </summary>
        private void RefreshResources(Settlement s)
        {
            HourRecord last = s.History.LastOrDefault();
            string Rate(double v) => (v >= 0 ? "+" : "−") + Math.Abs(v).ToString("0.0") + "/h";
            string Small(string text) => $"<size={Theme.Small}>{text}</size>";

            // 자금: 유지비와 그 속도로 버틸 시간.
            double upkeep = last?.Upkeep ?? 0;
            double cashHours = upkeep > 1e-9 ? s.Cash / upkeep : double.PositiveInfinity;
            int cashLevel = s.Cash < 0 || cashHours < 12 ? 2 : cashHours < 24 ? 1 : 0;
            string cashSub = cashHours < 48
                ? Paint($"−{upkeep:0.0}/h · {Math.Max(0, cashHours):0}시간분", cashLevel >= 1 ? Theme.Danger : Theme.Warn)
                : Paint($"유지비 −{upkeep:0.0}/h", Theme.Muted);
            _cash.Set(s.Cash.ToString("0"), cashSub, cashLevel);

            // 식품: 순 식량(생산 − 먹은 양 − 썩은 양)과 굶주림. 창고가 거의 차면 주의(넘치면 자동 판매·버림).
            double net = last == null ? 0 : last.FoodProduced - last.FoodDemand - last.Spoiled;
            double foodHours = net < -1e-9 ? s.Food / -net : double.PositiveInfinity;
            bool starving = s.Rules.LaborOn ? s.FoodSatisfaction < 0.999 : last != null && last.FoodDeficit > 1e-9;
            bool pantryFull = s.PantryUsed >= s.FoodCapacity * 0.95;
            int foodLevel = starving || foodHours < 12 ? 2 : foodHours < 24 || pantryFull ? 1 : 0;
            string foodSub = starving ? Paint("굶는 중!", Theme.Danger)
                : Paint(Rate(net), net < 0 ? Theme.Warn : Theme.Good)
                  + (foodHours < 48 ? Paint($" · {foodHours:0}시간분", Theme.Warn) : pantryFull ? Paint(" · 가득", Theme.Warn) : "");
            _food.Set($"{s.PantryUsed:0}{Small($"/{s.FoodCapacity:0}")}", foodSub, foodLevel);

            // 석재: 생산과 한도. 맨 앞 대기 공사가 석재를 기다리면 위험, 한도 90% 이상이면 주의.
            ConstructionJob next = s.Jobs.FirstOrDefault(j => j.State == JobState.Queued);
            bool waiting = next != null && next.StoneCost > s.Stone + 1e-9;
            bool full = s.Stone >= s.StoneCapacity * 0.9;
            int stoneLevel = waiting ? 2 : full ? 1 : 0;
            string stoneSub = waiting ? Paint($"공사 대기 {next.StoneCost:0} 필요", Theme.Danger)
                : Paint("+" + (last?.StoneProduced ?? 0).ToString("0.0") + "/h", Theme.Good) + (full ? Paint(" · 곧 넘침", Theme.Warn) : "");
            _stone.Set($"{s.Stone:0}{Small($"/{s.StoneCapacity:0}")}", stoneSub, stoneLevel);

            if (s.Rules.LaborOn)
            {
                // 노동력(결정 LB1~): 주민/정원과 일손(배치/필요). 떠나는 주택이 있으면 위험, 일손이 모자라면 주의.
                LaborAssignment labor = s.LastLabor;
                bool short_ = labor.Assigned + 0.05 < labor.Needed;
                bool leaving = s.Facilities.Any(s.IsLeaving);
                int level = leaving ? 2 : short_ ? 1 : 0;
                string sub = Paint($"일손 {labor.Assigned:0}/{labor.Needed:0}", short_ ? Theme.Warn : Theme.Good)
                    + (leaving ? Paint(" · 떠남", Theme.Danger) : "");
                _people.Set($"{s.Residents:0}{Small($"/{s.HousingCapacity:0}")}", sub, level);
            }
            else
            {
                int level = s.FoodCoefficient >= 0.95 ? 0 : s.FoodCoefficient >= 0.7 ? 1 : 2;
                _people.Set(s.FoodCoefficient.ToString("P0"), "식량 계수", level);
            }

            _ore.Set("+" + (last?.OreProduced ?? 0).ToString("0"), "광물/h", 0, Theme.Accent);
        }

        /// <summary>좁은 화면 축소(LY3). 오른쪽 묶음 폭은 내용에 따라 달라서 매번 잰다.</summary>
        private void FitToWidth()
        {
            float side = Math.Max(LeftWidth, LayoutUtility.GetPreferredWidth(_right));
            float s = FitScale(_ui.CanvasWidth / 2f - 2 * Margin, CenterWidth / 2f + side + Margin);
            foreach (RectTransform r in new[] { _left, _right, _place.rectTransform, _traits.rectTransform }) SetScale(r, s);
        }
    }
}
