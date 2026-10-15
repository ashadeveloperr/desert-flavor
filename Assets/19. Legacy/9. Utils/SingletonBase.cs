using UnityEngine;

namespace Jay
{
    public abstract class SingletonBase<T> : MonoBehaviour where T : MonoBehaviour
    {
        protected static T instance;
        private static readonly object lockObj = new object();
        private static bool applicationIsQuitting = false;
        private static bool isInitialized = false;

        public static bool IsInitialized => isInitialized && instance != null;
        
        public static T Instance
        {
            get
            {
                lock (lockObj)
                {
                    // 이미 살아 있는 인스턴스라면 종료 중이라도 그대로 돌려준다.
                    // OnDestroy에서 구독을 해제하는 것 같은 "정리 작업"이 이걸 필요로 한다.
                    if (instance != null) return instance;

                    // 여기부터는 "새로 만들어야 하는" 상황이다.
                    // 종료 중에 새로 만들면 파괴된 오브젝트가 되살아나 씬에 남으므로 막는다.
                    if (applicationIsQuitting)
                    {
                        Debug.LogWarning($"[Singleton] {typeof(T)} 생성 시도됨: 애플리케이션 종료 중");
                        return null;
                    }

                    // 씬에 이미 놓여 있는지 먼저 찾아보고, 없으면 그때 만든다.
                    instance = FindAnyObjectByType<T>();

                    if (instance == null)
                    {
                        var singletonObject = new GameObject(typeof(T).Name);
                        instance = singletonObject.AddComponent<T>();
                        DontDestroyOnLoad(singletonObject);
                    }

                    (instance as SingletonBase<T>)?.OnInitialize();
                    isInitialized = true;

                    return instance;
                }
            }
        }

        protected virtual void Awake()
        {
            if (instance == null)
            {
                instance = this as T;
                DontDestroyOnLoad(gameObject);

                OnInitialize();
                isInitialized = true;
            }
            else if (instance != this)
            {
                Debug.LogWarning($"[Singleton] 중복 인스턴스 감지됨: {gameObject.name} 파괴");
                Destroy(gameObject);
            }
        }

        // "종료 중"을 표시하는 곳은 여기 하나뿐이다.
        // (게임 종료 시 유니티가 OnDestroy 보다 먼저 불러 준다)
        protected virtual void OnApplicationQuit()
        {
            applicationIsQuitting = true;
        }

        protected virtual void OnDestroy()
        {
            if (instance == this)
            {
                OnDispose();
                instance = null;
                isInitialized = false;

                // 여기서 "종료 중"으로 표시하면 안 된다.
                // 씬을 옮기느라 파괴된 것뿐일 수도 있고, 그때는 다음 씬에서 다시 만들어져야 한다.
            }
        }

        /// <summary>
        /// 싱글톤 초기화 시점에서 호출됨 (오버라이드 가능)
        /// </summary>
        protected virtual void OnInitialize() { }

        /// <summary>
        /// 싱글톤 제거 시점에서 호출됨 (오버라이드 가능)
        /// </summary>
        protected virtual void OnDispose() { }

        /// <summary>
        /// 현재 인스턴스를 강제로 제거하고 다시 초기화 가능 상태로 되돌림. 다시 싱글톤 호출해야함 (어떤 문제있는지 잘 모름)
        /// </summary>
        public static void ResetInstance()
        {
            if (instance != null)
            {
                (instance as SingletonBase<T>)?.OnDispose();

#if UNITY_EDITOR
                DestroyImmediate((instance as MonoBehaviour)?.gameObject);
#else
                Destroy((instance as MonoBehaviour)?.gameObject);
#endif
                instance = null;
                isInitialized = false;
                applicationIsQuitting = false;
            }
        }
    }
}