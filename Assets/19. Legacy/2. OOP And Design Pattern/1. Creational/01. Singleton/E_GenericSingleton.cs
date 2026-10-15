using System;
using UnityEngine;

namespace Study_OOP_And_Design_Pattern.Creational
{
    // Singleton - 제네릭 베이스
    // 아이디어 : 매니저 마다 싱글톤 코드를 반복하지 않고, 한 베이스로 통일.
    //          Mono + DontDestroy 기반의 싱글톤은 이미 제공함. SingletonBase<T>를 참고
    
    public class GenericSingleton<T> where T : class, new()
    {
        private static T instance;

        public static T Instance => instance ??= new T();
        
        protected GenericSingleton() { }
    }

    public class ConfigManager : GenericSingleton<ConfigManager> { public int Volume = 50; }
    public class SaveManager : GenericSingleton<SaveManager> { public int Gold = 100; }
    

    public class GenericSingletonDemo : MonoBehaviour
    {
        private void Start()
        {
            ConfigManager.Instance.Volume = 100;
            SaveManager.Instance.Gold = 9999;
        }
    }
}