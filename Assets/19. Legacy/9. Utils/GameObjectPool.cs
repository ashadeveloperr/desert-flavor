using System;
using System.Collections.Generic;
using UnityEngine;

namespace Study.Utilities
{
    // # GameObjectPool<T> — 오브젝트 풀 (개선판)
    //
    // 앞서 만든 ComponentPool 은 "켜진 것을 찾을 때까지 리스트를 처음부터 훑는다"(선형 탐색).
    // 개체가 많아질수록 매번 훑는 비용이 커진다.
    //
    // 이번 버전은 두 가지를 바꾼다.
    //  1) 스택(Stack) : "쉬고 있는(반납된) 개체"만 따로 쌓아 둔다.
    //     → 꺼낼 때 Pop, 넣을 때 Push. 전체를 훑지 않으므로 항상 O(1).
    //  2) 델리게이트 : 꺼낼 때/넣을 때 할 일을 밖에서 정해 넣을 수 있다.
    //     → 상속하지 않고도 "꺼내면 방향 초기화", "넣으면 이펙트 정지" 같은 걸 끼운다.
    //
    // 반납이 "명시적"이라는 점도 다르다.
    //  - ComponentPool : 그냥 꺼두면 다음 Get 이 알아서 찾아 씀 (스캔)
    //  - 여기          : 다 쓴 쪽이 Release(item) 을 불러 직접 되돌려 준다
    //
    // Unity 내장 UnityEngine.Pool.ObjectPool<T> 와 같은 사고방식이라, 나중에 그대로 옮겨 탈 수 있다.
    public class GameObjectPool<T> where T : Component
    {
        private readonly T prefab;          // 복제 원본
        private readonly Transform parent;  // 복제본을 붙일 부모 (null이면 루트)

        // 쉬고 있는(반납된) 개체들만 담는다. "지금 사용 중인 것"은 여기 없다.
        private readonly Stack<T> pool = new Stack<T>();

        // 꺼낼 때 / 되돌릴 때 할 일. 지정하지 않으면 켜고 끄기만 한다.
        private readonly Action<T> onGet;
        private readonly Action<T> onRelease;

        public GameObjectPool(T prefab, Transform parent = null,
                              Action<T> onGet = null, Action<T> onRelease = null)
        {
            this.prefab = prefab;
            this.parent = parent;

            // 델리게이트를 안 넘기면 기본 동작(SetActive)으로 채운다.
            this.onGet = onGet ?? (item => item.gameObject.SetActive(true));
            this.onRelease = onRelease ?? (item => item.gameObject.SetActive(false));
        }

        // 쓸 개체를 하나 꺼낸다. 쉬는 게 없으면 그때 새로 만든다.
        public T Get()
        {
            T item = (pool.Count > 0)
                ? pool.Pop()                              // 쉬고 있던 것 재사용 (O(1))
                : UnityEngine.Object.Instantiate(prefab, parent);

            onGet(item);
            return item;
        }

        // 다 쓴 개체를 되돌려 준다.
        public void Release(T item)
        {
            onRelease(item);
            pool.Push(item);
        }
    }
}
