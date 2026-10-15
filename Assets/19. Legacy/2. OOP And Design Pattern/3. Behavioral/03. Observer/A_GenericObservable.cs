using System;
using System.Collections.Generic;
using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study_OOP_And_Design_Pattern.Behavioral
{
    // Observer - 응용편 : 제네릭 + 델리게이트

    public class Observable<T>
    {
        private T value;
        public event Action<T> OnChanged;
        public Observable(T initial) { value = initial;  }

        public T Value
        {
            get
            {
                return value;
            }
            set
            {
                // EqualityComparer?
                // : 같은지를 비교하는 객체를 통해서 해당 value가 동일한지 비교하는
                //  방법을 위임합니다. Generic 프로그래밍에는 해당 객체를 쫌 많이 사용합니다.
                // Tip : 뒤에있는 파란색 value는 외부에서 Set 프로퍼티를 호출할때 전달받는
                //      대입값 키워드입니다
                if (EqualityComparer<T>.Default.Equals(this.value, value)) return;
                
                // 다르다면
                this.value = value;         // 대입하고
                OnChanged?.Invoke(value);   // 알려준다
            }
        }
    }

    public class ObservableDemo : MonoBehaviour
    {
        private Observable<int> hp =  new Observable<int>(100);
        
        private void Start()
        {
            hp.OnChanged += value =>
            {
                Debug.Log($"[체력] : {value}");
            };
            
            hp.OnChanged += value =>
            {
                if(hp.Value <= 0) Debug.Log($"[게임오버]");
            };
        }

        private void Update()
        {
            if (SimpleInput.GetKeyDown(Key.Space))
            {
                hp.Value -= 10;
            }
        }
    }
}