using System;
using System.Linq;
using DesertSettlement.Core.Timing;
using UnityEngine.InputSystem;
using static DesertSettlement.Runtime.UI.UiKit;

namespace DesertSettlement.Runtime.UI
{
    /// <summary>
    /// 게임 진행 단축키(저장·불러오기·시간, 결정 HK1)와 오른쪽 아래 ? 버튼의 조작 안내(<see cref="HelpText"/>, 결정 HT2).
    /// 정식 UI의 뿌리 <see cref="GameUi"/>가 매 프레임 <see cref="Handle"/>을 부른다.
    /// 단축키를 더하거나 바꾸려면 아래 표만 고친다(안내 문구는 표의 설명에서 만든다). 같은 기능의 버튼은 위쪽 띠(<see cref="TopBarView"/>)에 있고, 버튼 글자에 키를 적어 둔다.
    /// 다른 키: 카메라 이동·회전(WASD·방향키·QE)은 <see cref="StrategyCameraRig"/>, 도구 해제(Esc)는 <see cref="BuildInputController"/> —
    /// 그쪽 키를 바꾸면 <see cref="OtherControls"/>도 고친다.
    /// </summary>
    public static class Hotkeys
    {
        private static readonly (Key Key, string Label, string Help, Action<GameUi> Run)[] Table =
        {
            (Key.F5, "F5", "저장", ui => ui.ShowMessage(ui.Session.SaveGame())),
            (Key.F9, "F9", "불러오기", ui => ui.ShowMessage(ui.Session.LoadGame())),
            (Key.Backquote, "`", "일시정지 · 다시 누르면 재개", ui => ClockOf(ui)?.TogglePause()),
            (Key.Digit1, "1", "배속 1단계(일시정지 해제)", ui => SelectSpeed(ui, 0)),
            (Key.Digit2, "2", "배속 2단계", ui => SelectSpeed(ui, 1)),
            (Key.Digit3, "3", "배속 3단계", ui => SelectSpeed(ui, 2)),
        };

        /// <summary>다른 곳에서 처리하는 키·마우스 조작(안내 문구용).</summary>
        private static readonly (string Label, string Help)[] OtherControls =
        {
            ("W A S D · 방향키", "카메라 이동(화면 가장자리에 마우스를 대도 이동)"),
            ("Q · E", "카메라 회전"),
            ("마우스 휠", "확대·축소"),
            ("왼쪽 클릭", "건설 · 시설 선택(수로는 끌어서 여러 칸)"),
            ("오른쪽 클릭 · Esc", "선택 도구로 돌아가기"),
        };

        /// <summary>? 버튼 툴팁: 모든 조작 안내.</summary>
        public static string HelpText()
        {
            string Line((string Label, string Help) c) => Paint(c.Label, Theme.Accent) + "   " + c.Help;
            return Paint("조작 안내", Theme.Accent) + "\n"
                + string.Join("\n", OtherControls.Select(Line)) + "\n"
                + string.Join("\n", Table.Select(t => Line((t.Label, t.Help))));
        }

        /// <summary>이번 프레임에 눌린 단축키를 실행한다.</summary>
        public static void Handle(GameUi ui)
        {
            Keyboard k = Keyboard.current;
            if (k == null) return;
            foreach ((Key key, _, _, Action<GameUi> run) in Table)
            {
                if (!k[key].wasPressedThisFrame) continue;
                run(ui);
                ui.RefreshNow();
            }
        }

        private static GameClock ClockOf(GameUi ui) => ui.Session.IsReady ? ui.Session.Clock : null;

        private static void SelectSpeed(GameUi ui, int step)
        {
            GameClock clock = ClockOf(ui);
            if (clock != null && step < clock.SpeedStepCount) clock.SelectSpeed(step);
        }
    }
}
