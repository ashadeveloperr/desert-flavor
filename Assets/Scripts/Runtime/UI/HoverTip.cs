using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DesertSettlement.Runtime.UI
{
    /// <summary>
    /// UI 요소에 붙이는 마우스 툴팁 문구(결정 HT1). 마우스가 올라간 요소를 <see cref="Current"/>로 알려 주고,
    /// 그리는 일은 맵 툴팁과 같은 <see cref="TooltipView"/> 한 벌이 맡는다(UI 툴팁이 맵 툴팁보다 우선).
    /// 문구는 함수라서 0.2초마다 다시 읽어 지금 값을 보여 줄 수 있다. 붙이기는 <see cref="UiKit.Tip"/>.
    /// </summary>
    public sealed class HoverTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>지금 마우스가 올라간 툴팁(없으면 null).</summary>
        public static HoverTip Current { get; private set; }

        /// <summary>툴팁 문구를 만드는 함수.</summary>
        public Func<string> Content;

        public void OnPointerEnter(PointerEventData eventData) => Current = this;

        public void OnPointerExit(PointerEventData eventData)
        {
            if (Current == this) Current = null;
        }

        // 마우스가 올라간 채로 꺼지면(창이 바뀌는 등) 나가기 신호가 오지 않는다.
        private void OnDisable()
        {
            if (Current == this) Current = null;
        }
    }
}
