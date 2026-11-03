using UnityEngine;
using UnityEngine.EventSystems;

namespace DesertSettlement.Runtime
{
    /// <summary>마우스가 화면 UI 위에 있으면 월드 클릭(건설·카메라 가장자리 이동·휠 줌)을 막는다.</summary>
    public static class HudInputBlocker
    {
        /// <summary>화면 좌표(Input System 마우스 좌표)가 UI(uGUI) 위인지.</summary>
        public static bool IsOverHud(Vector2 screenPosition) =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
