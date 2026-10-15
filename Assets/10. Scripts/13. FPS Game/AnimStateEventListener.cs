// ─────────────────────────────────────────────
// AnimStateEventListener — 애니메이터 상태가 보낸 신호를 받아 구독자에게 넘기는 우체통
// 학습 포인트
//  - StateMachineBehaviour(AnimStateEventSender)는 씬 오브젝트를 직접 참조할 수 없어, Animator와 같은 오브젝트의 이 컴포넌트를 거칩니다
//  - 받는 쪽(LocalPlayer)은 신호 이름 문자열로 무엇이 끝났는지 구분합니다
// 조작
//  - 없음
// ─────────────────────────────────────────────
using System;
using UnityEngine;

namespace Study3D.FpsGame
{
    public class AnimStateEventListener : MonoBehaviour
    {
        // (신호 이름, 매개변수)
        public event Action<string, string> OnOccursAnimStateEvent;

        public void OccursAnimStateEvent(string eventName, string parameter)
        {
            OnOccursAnimStateEvent?.Invoke(eventName, parameter);
        }
    }
}
