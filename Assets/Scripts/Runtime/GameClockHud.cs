using DesertSettlement.Core.Timing;
using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 화면 왼쪽 위에 현재 시각·낮밤·배속을 표시하고 일시정지/배속 버튼을 제공하는 임시 HUD(IMGUI).
    /// 정식 UI를 만들면 이 컴포넌트는 지워도 된다.
    /// </summary>
    [RequireComponent(typeof(GameSession))]
    public sealed class GameClockHud : MonoBehaviour
    {
        [SerializeField] private Vector2 position = new Vector2(10f, 10f);

        private GameSession _session;

        private void Awake()
        {
            _session = GetComponent<GameSession>();
        }

        private void OnGUI()
        {
            if (_session == null || !_session.IsReady) return;
            GameClock clock = _session.Clock;

            var area = new Rect(position.x, position.y, 360f, 90f);
            HudInputBlocker.SetRects(this, area);
            GUILayout.BeginArea(area, GUI.skin.box);

            int hour = (int)clock.HourOfDay;
            int minute = (int)((clock.HourOfDay - hour) * 60.0);
            string phase = clock.IsDaytime ? "낮" : "밤";
            string state = clock.IsPaused ? "일시정지" : $"{clock.Speed:0.#}배속";
            GUILayout.Label($"{clock.Day}일차  {hour:00}:{minute:00}  ({phase})   {state}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(clock.IsPaused ? "재개" : "일시정지")) clock.TogglePause();
            for (int i = 0; i < clock.SpeedStepCount; i++)
            {
                string label = $"{clock.SpeedAt(i):0.#}x";
                if (i == clock.SpeedIndex) label = $"[{label}]";
                if (GUILayout.Button(label)) clock.SetSpeedIndex(i);
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }
    }
}
