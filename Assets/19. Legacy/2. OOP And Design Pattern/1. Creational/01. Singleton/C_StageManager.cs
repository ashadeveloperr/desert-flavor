using System;
using System.Collections;
using UnityEngine;

namespace Study_OOP_And_Design_Pattern.Creational
{
    // Singleton - MonoBehaviour + 씬 종속(씬 나가면 삭제됨)
    // 상황 : "인게임 스테이지"에만 존재해야하는 매니저(스테이지 마다 초기화 됨)
    // 형태 : 예시 B와 거의 같지만 DontDestroyOnLoad를 "하지 않는다"
    //       씬이 바뀌면 이 오브젝트도 파괴되고 Instance는 무효가 되지만,
    //       "인게임 스테이지"에 한정해서는 늘 유효한 단일 인스턴가 보장된다
    
    public class StageManager : MonoBehaviour
    {
        public StageManager Instance { get; private set; }

        protected virtual void Awake()
        {
            // 중복 검사
            if (Instance != null && Instance != this)
            {
                // case 1 (권장 사용)
                Destroy(gameObject);
                return;
                
                // case 2 (아래처럼 사용해도 됨. 기존에 있던녀석을 밀어냄)
                // Destroy(Instance.gameObject);
            }

            Instance = this;
            // 여기서 DontDestroyOnLoad 처리를 하지 않음.
        }

        protected virtual void OnDestroy()
        {
            // 아래 처리를 하지 않아도 무방함. 씬 사라지면 어차피 다 지워짐
            // 갑자기 든 생각 : 하는게 좋을것 같은데?
            if (Instance == this) Instance = null;
        }

        public virtual void StartStage()
        {
            Debug.Log("StartStage");
        }
    }

    public class NormalStageManager : StageManager
    {
        public GameObject EnemyPrefab;
        public int EnemyCount = 10;

        private void Start()
        {
            for (int i = 0; i < EnemyCount; i++)
            {
                Instantiate(EnemyPrefab);
            }
        }

        public override void StartStage()
        {
            Debug.Log("노멀 스테이지 시작!");
        }
    }

    public class BossStageManager : StageManager
    {
        public GameObject BossPrefab;
        public GameObject MinionPrefab;
        
        private void Start()
        {
            Instantiate(BossPrefab);
        }

        public override void StartStage()
        {
            Debug.Log("보스 스테이지 시작!");
            StartCoroutine(SpawnMinionCoroutine());
        }
        
        private IEnumerator SpawnMinionCoroutine()
        {
            while (gameObject.activeInHierarchy)
            {
                Instantiate(BossPrefab);
                yield return new WaitForSeconds(3.5f);
            }
        }
    }
    
    public class StageManagerDemo : MonoBehaviour
    {
        public int StageID;
        
        private StageManager stageManager;
        
        private void Start()
        {
            if (StageID % 10 == 0)
            {
                stageManager = 
                    new GameObject("BossStageManager").AddComponent<BossStageManager>();
            }
            else
            {
                stageManager =
                    new GameObject("NormalStageManager").AddComponent<NormalStageManager>();
            }
            
            stageManager.StartStage();
        }
    }
}