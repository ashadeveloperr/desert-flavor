using System;
using UnityEngine;

namespace Study_DataStructure
{
    public class Study_Array1D : MonoBehaviour
    {
        private void Start()
        {
            StudyArray1D();
        }

        // 1차원 배열(Array)은 "번호표 가 붙은 사물함 한 줄 = 라커룸"
        // 1. 고정된 크기 : 배열을 만들 때, 몇 개의 사물함을 만들지 미리 정해야 합니다. 한 번 정하면 못 바꾼다
        //      (List<T>와 차이점은 List는 크기가 자유자재로 변경될 수 있다는 것. 배열은 못 바꿈)
        // 2. 같은 종류만 보관 : 모든 사물함에는 같은 종류의 물건(데이터)만 보관 가능. 예를 들어 숫자는 숫자 배열에,
        //      문자열은 문자열 배열에 보관 가능.
        // 3. 순서와 인덱스 : 각 사물함에는 0번부터 시작하는 고유한 번호표(인덱스)가 붙어 있습니다. 이 번호표를
        //      통해서 사물함에 접근이 가능하다.
    
        public void StudyArray1D()
        {
            // === 1. 배열 생성 및 초기화: 크기를 지정하거나 값을 바로 할당 ===
            // 1-1. 크기만 지정하고 나중에 값 할당 (int형 5칸짜리 배열)
            int[] scores = new int[5]; // 0번부터 4번까지 5개의 사물함이 생기고, 기본값인 0으로 채워집니다.
    
            Debug.Log("--- 1-1. 크기만 지정한 배열 생성 직후 ---");
            Debug.Log($"scores 배열의 길이: {scores.Length}"); // .Length는 배열의 크기를 알려줍니다.
            Debug.Log($"scores[0]의 초기값: {scores[0]}"); // 0 출력
    
            //Console.WriteLine($"Scores[5] 요소의 값은 : {scores[5]}"); 
            //=> 길이를 벗어나면 에러! (주석풀고 실행)        
            
            Debug.Log(" ");
    
            // 1-2. 생성과 동시에 값 할당 (string형 배열)
            string[] fruits = { "사과", "바나나", "딸기" }; // 3칸짜리 배열이 생성되고, 지정된 값으로 채워집니다.
    
            Debug.Log("--- 1-2. 값과 함께 초기화한 배열 생성 직후 ---");
            Debug.Log($"fruits 배열의 길이: {fruits.Length}"); // 3 출력
            Debug.Log($"fruits[1]의 값: {fruits[1]}"); // "바나나" 출력
            Debug.Log(" ");
    
    
            // === 2. 데이터 접근 및 수정: 인덱스(index) 사용 ===
            // scores 배열의 각 사물함에 점수를 넣어봅시다.
            scores[0] = 85;
            scores[1] = 90;
            scores[2] = 78;
            scores[3] = 92;
            scores[4] = 88;
    
            Debug.Log("--- 2. scores 배열에 값 할당 후 ---");
            Debug.Log($"scores[0]의 값: {scores[0]}"); // 85 출력
            Debug.Log($"scores[4]의 값: {scores[4]}"); // 88 출력
            Debug.Log(" ");
    
            // === 3. 모든 데이터 확인: 반복문 ===
            //배열의 모든 요소를 순서대로 열어 내용을 확인
            Debug.Log("--- 3-1. for문 이용 ---");
            for (int i = 0; i < scores.Length; i++)
            {
                Debug.Log($"{i}번 사물함의 점수: {scores[i]}");
            }
            Debug.Log(" ");
    
            Debug.Log("--- 3-1. fruits 배열의 모든 과일 확인하기 (foreach) ---");
            foreach (string fruit in fruits)
            {
                Debug.Log($"과일: {fruit}");
            }
            Debug.Log(" ");
    
    
            //=== 정리: 1차원 배열은 언제 사용할까요? ===
            //1. 데이터의 개수가 고정되어 있고, 미리 알 수 있을 때:
            //   - 한 학급의 학생 수가 30명으로 정해져 있을 때, 30칸짜리 학생 점수 배열.
            //   - 요일별 매출 데이터 (7칸짜리 배열).
            //2. 데이터의 순서가 중요하고, 인덱스를 통해 직접 접근해야 할 때:
            //   - 특정 위치의 데이터를 빠르게 읽거나 수정해야 할 때 유용합니다.
            //3. 메모리 사용이 효율적이어야 할 때:
            //   - List<T>보다 약간 더 적은 메모리를 사용하며, 성능 면에서 미세하게 유리할 수 있습니다.
        }
    }
}

