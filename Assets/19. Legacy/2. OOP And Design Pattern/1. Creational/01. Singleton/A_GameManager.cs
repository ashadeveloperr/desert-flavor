using System;
using UnityEngine;

namespace Study_OOP_And_Design_Pattern.Creational
{
    // Singleton - 순수 C# (가장 기본 형태)
    // 상황 : 게임에 하나만 있어야 하는 GameManager(점수/스테이지 등등)
    // 형태 : static 인스턴스 + private 생성자. (유니티와 무관한 순수 C#)
    
    public class GameManager
    {
        private static GameManager instance;
        public static GameManager Instance
        {
            get { return instance ??= new GameManager(); }
            // # instance ??= new GameManager(); 는 아래의 코드와 같습니다
            // if(instance == null)
            // {
            //   instance = new GameManager();
            // }
        }

        private GameManager() {}

        public int Score { get; private set; }

        public void AddScore(int score)
        {
            Score += score;
        }

        // 객체를 새로 만들어서 static 변수에 할당합니다
        public void Reset()
        {
            instance = new GameManager();
        }
    }

    public class SingletonBasicDemo : MonoBehaviour
    {
        private void Start()
        {
            GameManager gm = GameManager.Instance;
            gm.AddScore(10);
            Debug.Log(gm.Score);
            //=================================
            GameManager.Instance.AddScore(10);
            Debug.Log(GameManager.Instance.Score);
        }
    }
}