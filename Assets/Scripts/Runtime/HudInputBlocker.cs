using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// IMGUI 패널이 차지한 화면 영역을 모아 두고, 마우스가 패널 위에 있으면 월드 클릭(건설·카메라 가장자리 이동)을 막는다.
    /// 패널은 OnGUI에서 자기 영역을 <see cref="SetRects"/>로 등록한다.
    /// </summary>
    public static class HudInputBlocker
    {
        private static readonly Dictionary<object, List<Rect>> Rects = new Dictionary<object, List<Rect>>();

        /// <summary>owner의 패널 영역(GUI 좌표: 왼쪽 위 원점)을 교체한다.</summary>
        public static void SetRects(object owner, params Rect[] rects)
        {
            if (!Rects.TryGetValue(owner, out List<Rect> list)) Rects[owner] = list = new List<Rect>();
            list.Clear();
            list.AddRange(rects);
        }

        public static void Clear(object owner) => Rects.Remove(owner);

        /// <summary>화면 좌표(왼쪽 아래 원점, Input System 마우스 좌표)가 패널 위인지.</summary>
        public static bool IsOverHud(Vector2 screenPosition)
        {
            // 정식 UI(uGUI, 2026-10-23): 마우스가 UI 위면 맵 조작을 막는다.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return true;
            var gui = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            foreach (List<Rect> list in Rects.Values)
            {
                foreach (Rect r in list)
                {
                    if (r.Contains(gui)) return true;
                }
            }
            return false;
        }
    }
}

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 어느 화면을 쓰는지(2026-10-23 정식 UI, Docs/decision_log_auto.md UI3). false = 새 화면(uGUI), true = 예전 임시 화면(IMGUI). F1로 바꾼다.
    /// 예전 화면 컴포넌트는 꺼지지 않고 그리기만 멈춘다(단축키·툴팁은 그대로 동작).
    /// </summary>
    public static class HudMode
    {
        public static bool Legacy;
    }
}
