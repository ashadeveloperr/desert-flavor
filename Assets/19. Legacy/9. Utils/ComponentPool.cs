using System.Collections.Generic;
using UnityEngine;


namespace Study.Utilities
{
    // # ComponentPool<T> - 간이 오브젝트 풀
    //
    // - "Destroy 대신 꺼두고, Instantiate 대신 꺼둔 것을 다시 켠다"
    
    // Instantiate/Destroy는 비싼 작업이라, 탄환/데미지 팝업처럼
    // 짧게 살고 자주 생기는 개체는 재사용하는 것이 정석이다.

    public class ComponentPool<T> where T : Component
    {
        private readonly T original;       // 복제 원본 (프리팹 또는 씬 개체)
        private readonly Transform parent; // 복제본을 붙일 부모 (null이면 루트)

        // 지금까지 만든 복제본들. 켜짐/꺼짐 여부로 사용 중인지 구분한다.
        private readonly List<T> spawned = new List<T>();

        public ComponentPool(T original, Transform parent = null)
        {
            this.original = original;
            this.parent = parent;
        }

        /// <summary>
        /// 사용 가능한 개체를 하나 꺼내온다. (켜진 상태로 반환됨)
        /// </summary>
        public T Get()
        {
            // 1. 먼저 꺼져 있는(재사용 가능한) 복제본이 있는지 찾아본다.
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i].gameObject.activeSelf == false)
                {
                    spawned[i].gameObject.SetActive(true);
                    return spawned[i];
                }
            }

            // 2. 재사용할 것이 없을 때만 새로 만든다.
            T newOne = Object.Instantiate(original, parent);
            spawned.Add(newOne);
            newOne.gameObject.SetActive(true);
            return newOne;
        }
    }
}


