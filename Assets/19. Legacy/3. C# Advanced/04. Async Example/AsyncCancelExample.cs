using System;
using System.Threading;
using System.Threading.Tasks;
using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study.Async
{
    // 오래 걸리는 비동기에서 반드시 생기는 이슈와 그 처리
    
    // 상황
    // 1. 사용자가 중간에 그만두고 싶다.
    // 2. 대기 중에 이 오브젝트가 파괴된다.
    
    public class AsyncCancelExample : MonoBehaviour
    {
        public int stepCount = 10;
        public float stepSeconds = 0.5f;
        
        // 비동기 작업의 취소버튼 : CancellationTokenSource
        // (진짜로 줄여서 cts라고 함)
        private CancellationTokenSource cts;
        
        private void Update()
        {
            if(SimpleInput.GetKeyDown(Key.Digit1)) StartWork();
            if(SimpleInput.GetKeyDown(Key.Digit2)) CancelWork();
        }

        private void OnDestroy()
        {
            CancelWork();
        }

        private void CancelWork()
        {
            cts?.Cancel();      // 현재 cts가 있니? 있으면 토큰을 취소로 바꿔줘
            cts?.Dispose();     // cts 인스턴스 해제 해줘
            cts = null;         // cts 객체를 비워줘
        }
        
        private async void StartWork()
        {
            CancelWork();
            
            // CancellationTokenSource는 한번 취소하면 되살릴 수 없어서
            // 작업마다 새로 만들어 줘야함.
            
            // 1. 순수 C#기준의 CancellationTokenSource();
            // cts = new CancellationTokenSource(); 
            
            // 2. Unity에서는 아래의 cts를 사용함
            cts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            // destroyCancellationToken ?
            // : 강제적으로 게임오브젝트가 삭제되었을때 사용하라고 만든
            //  토큰임. CreateLinkedTokenSource() 함수를 이용해서
            //  Mono 객체의 경우 생명주기 토큰으로 연결해주는게 안전합니다.
            
            
            // 비동기를 이용한 긴 작업의 경우. try-catch문을 이용해서
            // 예외처리 해줍니다.
            // (긴 작업? => 완료 시간을 예측하기 어려울 경우)
            try
            {
                await DoWork(cts.Token);
                Debug.Log($"Work Done {cts.Token}");
            }
            catch (OperationCanceledException e)
            {
                Debug.Log("Work Cancel: " + e);
            }
        }

        private async Task DoWork(CancellationToken token)
        {
            for (int i = 0; i < stepCount; ++i)
            {
                await Task.Delay(Mathf.RoundToInt(stepSeconds * 1000), token);
                Debug.Log($"Step {i}/{stepCount}");
            }
        }
        
    }
}