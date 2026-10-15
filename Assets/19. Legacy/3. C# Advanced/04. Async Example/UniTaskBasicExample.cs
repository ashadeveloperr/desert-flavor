using System;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Study.Utilities;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace Study.Async
{
    // 기본 형태는 UniTaskBasicExample을 참고
    
    // 반환 타입
    // - UniTask : 값을 안 돌려주는 작업에 사용합니다. Task로 치면 그냥 Task가 됩니다
    // - UniTask<T> : T타입의 값을 반환하는 작업에 사용합니다. Task<T>와 동일하게 대응됩니다.
    // - UniTaskVoid : 이벤트 진입점에서만 사용합니다. async void와 동일하게 대응 됩니다
    
    // 주요 메서드 (대기)
    // UniTask.Delay(int ms) : 시간 대기. ms만큼 대기합니다.
    // UniTask.Delay(int ms, DelayType type) : type의 유형에 맞춰 대기합니다.
    // UniTask.WaitForSeconds(float sec) : 초 단위 대기. 코루틴의 WaitForSeconds와 대응됨
    // UniTask.DelayFrame(int n) : n 프레임 대기
    // UniTask.Yield(), UniTask.NextFrame() : 한 프레임 대기
    // UniTask.WaitForEndOfFrame : 렌더가 끝날때까지 대기. 코루틴의 WaitForEndOfFrame와 대응됨
    // UniTask.WaitForFixedUpdate : 다음 FixedUpdate까지 대기
    // UniTask.WaitUntil(람다 조건식) : 람다 조건식(()=>조건)이 참이 될때까지 대기
    // UniTask.WaitWhile(람다 조건식) : 람다 조건식이 참인 동안 계속 대기
    // UniTask.WaitUntilValueChanged(대상, x => x.값) : 해당 값이 바뀔때까지 대기
    // UniTask.WaitUntilCanceled(token) : 토큰에 의해 취소될때까지 대기
    
    
    // UniTask가 Task 대비 무엇을 더 주는지 하나씩 테스트 해보는 예제
    public class UniTaskBasicExample : MonoBehaviour
    {
        public float waitSeconds = 2f;
        public int waitFrame = 60;
        public float spinSpeed = 180f;
        public bool isReady = false;

        public int waitValue = 10;
        
        private CancellationToken destroyToken;

        private void Awake()
        {
            // destroyCancellationToken이랑 GetCancellationTokenOnDestroy() 같습니다.
            // (destroyCancellationToken 요게 Unity6에서 나왔음. 선택은 취향)
            destroyToken = this.GetCancellationTokenOnDestroy();
        }

        private void Update()
        {
            transform.Rotate(0f,0f,spinSpeed * Time.deltaTime);

            if (SimpleInput.GetKeyDown(Key.T)) ToggleTimeScale();
            
            // UniTaskVoid 형태에는 Forget을 입력해서 문법적처리로 에러로그를 방지했었음
            if (SimpleInput.GetKeyDown(Key.Digit1)) DelayGameTimeAsync().Forget();
            if (SimpleInput.GetKeyDown(Key.Digit2)) WaitUntilReady().Forget();
            if (SimpleInput.GetKeyDown(Key.Digit3)) DelayFrame().Forget();
            if (SimpleInput.GetKeyDown(Key.Digit4)) DelayValueChanged().Forget();
            if (SimpleInput.GetKeyDown(Key.Digit5)) DelayTargetMoved().Forget();
            if (SimpleInput.GetKeyDown(Key.Digit6)) RollDice();
            if (SimpleInput.GetKeyDown(Key.Digit7)) RollDiceAnyStop();
        }

        private bool timeToggle = true;
        private void ToggleTimeScale()
        {
            timeToggle = !timeToggle;
            Time.timeScale = timeToggle ? 1f : 0f;
        }
        
        private async UniTaskVoid DelayGameTimeAsync()
        {
            Debug.Log($"[UniTaskBasicExample] " +
                      $"DelayGameTimeAsync : {waitSeconds}초 대기");
            await UniTask.Delay(Mathf.RoundToInt(waitSeconds * 1000)
                , DelayType.Realtime 
                , cancellationToken: destroyToken); 
            Debug.Log($"[UniTaskBasicExample] DelayGameTimeAsync : 끝!");
        }

        private async UniTaskVoid WaitUntilReady()
        {
            Debug.Log($"[UniTaskBasicExample] WaitUntilReady : isReady 켜질때까지 대기");
            
            await UniTask.WaitUntil(() => isReady, cancellationToken: destroyToken);
            Color newColor = new Color(Random.value, Random.value, Random.value);
            GetComponent<SpriteRenderer>().color = newColor;
            // "() => isReady" 람다식이 조건문이 되는겁니다.
            // 결국 isReady가 True가 되면 대기가 완료 되는 로직이 됩니다.
            Debug.Log($"[UniTaskBasicExample] WaitUntilReady : isReady 켜짐");
        }
        
        private async UniTaskVoid DelayFrame()
        {
            Debug.Log($"[UniTaskBasicExample] DelayFrame");
            
            int beforeFrame = Time.frameCount;
            float temp = spinSpeed;
            spinSpeed = 0f;
            
            await UniTask.DelayFrame(waitFrame, cancellationToken: destroyToken);

            spinSpeed = temp;
            
            Debug.Log($"[UniTaskBasicExample] DelayFrame : {Time.frameCount - beforeFrame}," +
                      $"beforeFrame :  {beforeFrame}");
        }

        private async UniTaskVoid DelayValueChanged()
        {
            Debug.Log($"[UniTaskBasicExample] DelayValueChanged : 시작 waitValue {waitValue}");
            await UniTask.WaitUntilValueChanged(
                this, x => x.waitValue, cancellationToken: destroyToken);
            Debug.Log($"[UniTaskBasicExample] DelayValueChanged : 끝 waitValue {waitValue}");
        }
        
        public Transform Target;
        
        private async UniTaskVoid DelayTargetMoved()
        {
            Debug.Log($"[UniTaskBasicExample] DelayTargetMoved : 시작 {Target.transform.position}");
            await UniTask.WaitUntilValueChanged(
                Target.transform, 
                //targetTransform => targetTransform.position,
                IsInRange,
                cancellationToken: destroyToken);
            Debug.Log($"[UniTaskBasicExample] DelayTargetMoved : 끝 {Target.transform.position}");
        }

        private bool IsInRange(Transform targetTransform)
        {
            // 내 위치에서 3만큼 멀어졌니?
            return (5 > Vector3.Distance(transform.position, targetTransform.position));
        }
        
        
        // 여러개 처리하기(묶어서 처리하기)
        // UniTask.WhenAll(...) : 전부 끝날 때까지. 반환은 UniTask
        // UniTask.WhenAll<T>() : 전부 끝나고 결과까지. 반환은 배열.
        // UniTask.WhenAny(...) : 여러개중 하나가 끝날때까지. 반환은 UniTask<int>. 먼저 끝난것의 번호
        // UniTask.WhenAny<T>() : 전부 끝나고 결과까지. 반환은 배열.
        
        private async UniTask<int> RequestRollDice()
        {
            int randWait = Random.Range(500, 1000);
            await UniTask.Delay(randWait);
            int result = UnityEngine.Random.Range(1, 32);
            Debug.Log($"RequestRollDice : {result}" );
            return result;
        }

        private async UniTask<string> RequestMessage()
        {
            int randWait = Random.Range(500, 1000);
            await UniTask.Delay(randWait);
            string[] msg = { "승리", "패배", "무승부" };
            int randIndex = Random.Range(0, msg.Length);
            return msg[randIndex];
        }

        private async void RollDice()
        {
            Debug.Log($"Roll Dice Start : Time = {Time.time}");
            UniTask<int> first = RequestRollDice();
            UniTask<int> second = RequestRollDice();
            UniTask<int> third = RequestRollDice();
            UniTask<string> message = RequestMessage();

            //int[] results = await UniTask.WhenAll<int>(first, second, third);
            (int f, int s, int t, string m) results = 
                await UniTask.WhenAll(first, second, third, message);
            
            // Debug.Log($"Roll Dice Start : Time = {Time.time}, " +
            //           $"Result = {results[0]}, {results[1]}, {results[2]}");
            
            Debug.Log($"Roll Dice Start : Time = {Time.time}, " +
                      $"Result = {results.f}, {results.s}, {results.t}, {results.m}");
        }

        private async void RollDiceAnyStop()
        {
            Debug.Log($"RollDiceAnyStop Start : Time = {Time.time}");
            UniTask<int> first = RequestRollDice();
            UniTask<int> second = RequestRollDice();
            UniTask<int> third = RequestRollDice();
            
            var results = 
                await UniTask.WhenAny<int>(first, second, third);
            
            Debug.Log($"RollDiceAnyStop Start : 끝. " +
                      $"{results.winArgumentIndex}, {results.result}");
        }
    }
}