using DesertSettlement.Core.Timing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 화면 왼쪽 위에 현재 시각·낮밤·배속을 표시하고 일시정지/배속 버튼을 제공하는 임시 HUD(IMGUI).
    /// 저장·불러오기 버튼(F5 / F9)도 여기 둔다(2026-10-23 로드맵 2-①). 배속: 일시정지 + 1/2/4x, 단축키 ` 1 2 3.
    /// 정식 UI를 만들면 이 컴포넌트는 지워도 된다.
    /// </summary>
    [RequireComponent(typeof(GameSession))]
    public sealed class GameClockHud : MonoBehaviour
    {
        [SerializeField] private Vector2 position = new Vector2(10f, 10f);
        [Tooltip("저장·불러오기 결과 문구를 보여 주는 시간(실제 초)")]
        [SerializeField] private float messageSeconds = 4f;

        private GameSession _session;
        private string _message;
        private float _messageUntil;

        private void Awake()
        {
            _session = GetComponent<GameSession>();
        }

        private void Update()
        {
            Keyboard k = Keyboard.current;
            if (k == null || _session == null) return;
            if (k.f5Key.wasPressedThisFrame) Show(_session.SaveGame());
            // 배속 단축키(2026-10-23 플레이테스트 메모): ` = 일시정지(다시 누르면 재개), 1·2·3 = 1x·2x·4x(누르면 일시정지 해제).
            GameClock clock = _session.IsReady ? _session.Clock : null;
            if (clock != null)
            {
                if (k.backquoteKey.wasPressedThisFrame) clock.TogglePause();
                Key[] digits = { Key.Digit1, Key.Digit2, Key.Digit3 };
                for (int i = 0; i < digits.Length && i < clock.SpeedStepCount; i++)
                    if (k[digits[i]].wasPressedThisFrame) clock.SelectSpeed(i);
            }
            if (k.f9Key.wasPressedThisFrame) Show(_session.LoadGame());
        }

        private void Show(string message)
        {
            GetComponent<UI.GameUi>()?.ShowMessage(message); // 새 화면에서도 보이게
            _message = message;
            _messageUntil = Time.unscaledTime + messageSeconds;
        }

        private void OnGUI()
        {
            if (!HudMode.Legacy) { HudInputBlocker.Clear(this); return; } // 새 화면(uGUI)을 쓰는 동안은 그리지 않는다(F1로 전환)
            if (_session == null || !_session.IsReady) return;
            GameClock clock = _session.Clock;

            var area = new Rect(position.x, position.y, 360f, 118f);
            HudInputBlocker.SetRects(this, area);
            GUILayout.BeginArea(area, GUI.skin.box);

            int hour = (int)clock.HourOfDay;
            int minute = (int)((clock.HourOfDay - hour) * 60.0);
            string phase = clock.IsDaytime ? "낮" : "밤";
            string state = clock.IsPaused ? "일시정지" : $"{clock.Speed:0.#}배속";
            GUILayout.Label($"{clock.Day}일차  {hour:00}:{minute:00}  ({phase})   {state}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(clock.IsPaused ? "재개 (`)" : "일시정지 (`)")) clock.TogglePause();
            for (int i = 0; i < clock.SpeedStepCount; i++)
            {
                string label = $"{clock.SpeedAt(i):0.#}x ({i + 1})";
                if (i == clock.SpeedIndex && !clock.IsPaused) label = $"[{label}]";
                if (GUILayout.Button(label)) clock.SelectSpeed(i); // 배속을 고르면 일시정지가 풀린다
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("저장 (F5)")) Show(_session.SaveGame());
            GUI.enabled = _session.HasSave;
            if (GUILayout.Button("불러오기 (F9)")) Show(_session.LoadGame());
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (_message != null && Time.unscaledTime < _messageUntil) GUILayout.Label(_message);

            GUILayout.EndArea();
        }
    }
}
