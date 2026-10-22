using System.Collections.Generic;
using UnityEngine;

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
