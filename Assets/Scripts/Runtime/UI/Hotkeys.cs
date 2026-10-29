using System;
using DesertSettlement.Core.Timing;
using UnityEngine.InputSystem;

namespace DesertSettlement.Runtime.UI
{
    /// <summary>
    /// 게임 진행 단축키(저장·불러오기·시간, 결정 HK1). 정식 UI의 뿌리 <see cref="GameUi"/>가 매 프레임 <see cref="Handle"/>을 부른다.
    /// 단축키를 더하거나 바꾸려면 아래 표만 고친다. 같은 기능의 버튼은 위쪽 띠(<see cref="TopBarView"/>)에 있고, 버튼 글자에 키를 적어 둔다.
    /// 다른 키: 카메라 이동·회전(WASD·방향키·QE)은 <see cref="StrategyCameraRig"/>, 도구 해제(Esc)는 <see cref="BuildInputController"/>.
    /// </summary>
    public static class Hotkeys
    {
        private static readonly (Key Key, Action<GameUi> Run)[] Table =
        {
            (Key.F5, ui => ui.ShowMessage(ui.Session.SaveGame())),
            (Key.F9, ui => ui.ShowMessage(ui.Session.LoadGame())),
            (Key.Backquote, ui => ClockOf(ui)?.TogglePause()), // ` = 일시정지(다시 누르면 재개)
            (Key.Digit1, ui => SelectSpeed(ui, 0)),           // 1·2·3 = 배속 단계(누르면 일시정지 해제)
            (Key.Digit2, ui => SelectSpeed(ui, 1)),
            (Key.Digit3, ui => SelectSpeed(ui, 2)),
        };

        /// <summary>이번 프레임에 눌린 단축키를 실행한다.</summary>
        public static void Handle(GameUi ui)
        {
            Keyboard k = Keyboard.current;
            if (k == null) return;
            foreach ((Key key, Action<GameUi> run) in Table)
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
