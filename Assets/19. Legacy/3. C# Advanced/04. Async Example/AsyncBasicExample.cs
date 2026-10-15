using System.Threading;
using System.Threading.Tasks;
using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study.Async
{
    // 비동기 기초 예제 : 물체 회전
    public class AsyncBasicExample : MonoBehaviour
    {
        public float waitSeconds = 2f;
        public float spinSpeed = 180f;

        private void Update()
        {
            transform.Rotate(0, 0, spinSpeed * Time.deltaTime);

            if (SimpleInput.GetKeyDown(Key.Digit1)) BlockingWait();
            if (SimpleInput.GetKeyDown(Key.Digit2)) TaskDelayWait();
            if (SimpleInput.GetKeyDown(Key.Digit3)) AwaitableWait();
            if (SimpleInput.GetKeyDown(Key.Digit4)) ToggleTimeScale();
            
            if (SimpleInput.GetKeyDown(Key.R)) RollDice();
            if (SimpleInput.GetKeyDown(Key.S)) SumTask();
        }

        private bool timeToggle = true;
        
        private void ToggleTimeScale()
        {
            timeToggle = !timeToggle;
            Time.timeScale = timeToggle ? 1f : 0f;
        }
        
        // 1. 동기 대기 (실전에서 사용하지 말것)
        private void BlockingWait()
        {
            Debug.Log("Blocking wait");
            // Thread.Sleep은 메인 쓰레드를 통쨰로 재웁니다
            // 그 사이 Update도 렌더링도 입력도 돌지 않습니다.
            // (화면이 그대로임)
            Thread.Sleep(Mathf.RoundToInt(waitSeconds * 1000));
            
            Debug.Log("Blocking wait - 끝-");
        }

        // 2. Task.Delay 기준 대기
        private async void TaskDelayWait()
        {
            Debug.Log("TaskDelayWait");
            await Task.Delay(Mathf.RoundToInt(waitSeconds * 1000));
            
            Debug.Log("TaskDelayWait - 끝 -");
        }
        
        // 3. Awaitable - Unity 6 내장
        // 게임 시간 기준 대기
        private async void AwaitableWait()
        {
            Debug.Log("AwaitableWait");
            await Awaitable.WaitForSecondsAsync(waitSeconds);
            Debug.Log("AwaitableWait - 끝 -");
        }
        
        // 4. 작업이 완료될때까지 기다리는 비동기 코드
        // : 가상의 서버코드가 있다고 가정을 해봅시다.
        // 아래는 주사위를 굴려서 값을 전달받는 가상의 서버코드
        private async Task<int> RequestRollDice()
        {
            int randWait = Random.Range(500, 1000);
            await Task.Delay(randWait);
            int result = UnityEngine.Random.Range(1, 32);
            Debug.Log($"RequestRollDice : {result}" );
            return result;
        }

        // 주사위를 굴리라고 요청하는 가상의 클라이언트 측 코드
        private async void RollDice()
        {
            var startTime = Time.time;
            Debug.Log($"Start Time {startTime}");
            
            // 직렬 처리 예제
            // int first = await RequestRollDice();
            // int second = await RequestRollDice();
            // int third = await RequestRollDice();
            
            //Debug.Log($"RollDice: {first}, {second}, {third}, {Time.time - startTime}");
            
            // 병렬 처리 예제
            Task<int> first = RequestRollDice();
            Task<int> second = RequestRollDice();
            Task<int> third = RequestRollDice();
            
            await Task.WhenAll(first, second, third);
            
            Debug.Log($"RollDice: {first.Result}, {second.Result}, {third.Result}, {Time.time - startTime}");
        }


        private async void Test()
        {
            
            
            
        }

        private async void SumTask()
        {
            Task plus = DoPlus();
            Task minus = DoMinus();
            Task print = DoPrint();
            
            await Task.WhenAll(plus, minus, print);
        }

        public int stepCount = 100;
        private int result = 0;
        
        private async Task DoPlus()
        {
            for (int i = 0; i < stepCount; ++i)
            {
                await Task.Delay(Mathf.RoundToInt(10));
                result++;
            }
        }
        
        private async Task DoMinus()
        {
            for (int i = 0; i < stepCount; ++i)
            {
                await Task.Delay(Mathf.RoundToInt(10));
                result--;
            }
        }
        
        private async Task DoPrint()
        {
            for (int i = 0; i < stepCount; ++i)
            {
                await Task.Delay(Mathf.RoundToInt(10));
                Debug.Log($"Step {i}/{stepCount} : result : {result}");
            }
        }
    }
}