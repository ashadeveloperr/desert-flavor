using System;
using UnityEngine;

namespace Study_OOP_And_Design_Pattern.Creational
{
    // Singleton - MonoBehaviour + DontDestroyOnLoad 형태
    // 상황 : 씬이 바뀌어도 유지돼야 하는 사운드 매니저
    // 형태 : Awake에서 자기를 등록 + DontDestroyOnLoad + 중복 파괴 기능
    //      순수 C#과는 달리 GameObject/컴포넌트에 얹힌다.
    
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; } 
        
        private void Awake()
        {
            // 중복검사 (씬에 인스턴스가 존재하며, 해당 인스턴스가 내가 아니라면)
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            // 인스턴스 할당
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Play(string soundKey)
        {
            Debug.Log($"재생! {soundKey}");
        }
    }

    public class SoundDemo : MonoBehaviour
    {
        private void Start()
        {
            // 모노 싱글톤 사운드매니저가 없다면 컴포넌트를 추가한다
            // 해당 컴포넌트가 할당되고 Awake()에서 알아서 싱글톤으로 바뀐다
            // 아래는 보통 첫 실행시에 검사만 하면 됨. 씬에 배치했으면 거의 동작 안함.
            if (SoundManager.Instance == null)
            {
                new GameObject("SoundManager").AddComponent<SoundManager>();
            }
            
            SoundManager.Instance.Play("BGM");
        }
    }
}