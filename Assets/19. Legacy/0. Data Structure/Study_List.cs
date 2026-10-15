using System;
using System.Collections.Generic;
using UnityEngine;
// # System.Collections.Generic?
// - C#에서 지원하는 자료구조 도구들을 사용할 수 있는 네임스페이스 입니다

namespace Study_DataStructure
{
    public class Study_List : MonoBehaviour
    {
        private void Start()
        {
            List();
        }

        // # List<T>는 "타입이 정해진 마법의 쇼핑백" : 동적 배열
        // 1. 크기는 자유자재 : 처음에는 비어있지만, 물건(데이터)을 넣을수록 쇼핑백이
        //   알아서 늘어납니다. 반대로 물건을 빼면 크기가 줄어듭니다. 배열(Array)처럼
        //   크기를 미리 정해둘 필요가 없음.
        // 2. 한 종류만 담는 규칙 : 이 쇼핑백은 특별해서, "과일만 담는 쇼핑백", "장난감"
        //   만 담은 쇼핑백 처럼 한종류의 물건만 담기로 약속해야 합니다. 만약, 숫자를
        //   담는 List를 만들었다면 문자열이나 다른 타입의 데이터는 넣을 수 없습니다.


        private void List()
        {
            // # 1. List<T>의 생성 및 선언 : 어떤 종류의 데이터를 담을지 알려주기
            // - T는 타입(자료형)을 의미합니다. 타입 매개변수라고 부릅니다.
            List<string> fruitBasket = new List<string>();

            Debug.Log("1. List 생성 직후");
            Debug.Log($"쇼핑백 안의 과일 개수 : {fruitBasket.Count}");
            // .Count
            // : 리스트 객체가 현재 가지고 있는 데이터 갯수를 반환합니다.
            Debug.Log("");

            // # 2. 데이터 추가 : .Add()
            // 데이터를 추가해봅시다
            fruitBasket.Add("사과");
            fruitBasket.Add("딸기");
            fruitBasket.Add("바나나");

            Debug.Log("2. 과일 3개 추가 후");
            Debug.Log($"쇼핑백 안의 과일 개수 : {fruitBasket.Count}");
            Debug.Log("");

            // # 3. 데이터 접근 : 인덱스(index) 사용
            // List 안의 데이터들은 배열과 마찬가지로 0번부터 순서대로 번호표를
            // 받습니다. 이 번호를 "index"라고 부릅니다.
            // fruitBasket[0]은 첫번째 데이터, 
            // fruitBasket[1]은 두번째 데이터, 배열과 동일합니다.

            Debug.Log("3. 과일에 접근해보기");
            Debug.Log($"fruitBasket[1] = {fruitBasket[1]}");
            Debug.Log("");

            // # 4. 모든 데이터 조회 : 반복문

            Debug.Log("4. 모든 과일 출력하기");
            for (int i = 0; i < fruitBasket.Count; ++i)
            {
                Debug.Log($"fruitBasket[{i}] = {fruitBasket[i]}");
            }
            Debug.Log("");

            // # 5. 데이터 삭제 : .Remove() / .RemoveAt()
            // 5.1 "바나나"를 삭제해 봅시다

            Debug.Log("5.1. 바나나 삭제");
            fruitBasket.Remove("바나나");
            for (int i = 0; i < fruitBasket.Count; ++i)
            {
                Debug.Log($"fruitBasket[{i}] = {fruitBasket[i]}");
            }
            Debug.Log("");

            // 5.2 0번 인덱스의 요소를 삭제해봅시다.
            Debug.Log("5.2. 0번 삭제");
            fruitBasket.RemoveAt(0);
            for (int i = 0; i < fruitBasket.Count; ++i)
            {
                Debug.Log($"fruitBasket[{i}] = {fruitBasket[i]}");
            }
            Debug.Log("");


            Debug.Log("=== 새로운 데이터 2개 추가 ===");
            fruitBasket.Add("파인애플");
            fruitBasket.Add("배");

            for (int i = 0; i < fruitBasket.Count; ++i)
            {
                Debug.Log($"fruitBasket[{i}] = {fruitBasket[i]}");
            }
            Debug.Log("");

            Debug.Log("=== 1번 인덱스에 블루베리 추가 ===");
            // .Insert() : 특정 인덱스에 값을 추가하고 요소들을 뒤로 한칸 밀어냅니다
            fruitBasket.Insert(1, "블루베리");

            for (int i = 0; i < fruitBasket.Count; ++i)
            {
                Debug.Log($"fruitBasket[{i}] = {fruitBasket[i]}");
            }
            Debug.Log("");

            // # 6. 데이터 검색 : Contains()
            // List안에 특정 데이터가 있는지 확인할 수 있습니다.
            // 존재하면 true를 없다면 false를 반환합니다 

            string name = "사과";

            if(fruitBasket.Contains(name))
            {
                Debug.Log($"List 안에 {name}이 있습니다!");
            }
            else
            {
                Debug.Log($"List 안에 {name}이 없습니다!");
            }
            Debug.Log("");

            // 정리 : List<T>는 언제 사용할까요?
            
            // 1. 데이터의 개수가 정해져 있지 않을 때 :
            //   프로그램 실행 중에 데이터가 계속 추가되거나 삭제될 가능성이 있다면
            //   List<T>가 배열(Array)보다 훨씬 편리함.
            
            // 2. 순서가 중요 할 때:
            //   List<T>는 데이터가 들어온 순서대로 차곡차곡 관리 됩니다. 특정 인덱스에
            //   삽입/삭제를 할때도 순서대로 관리 됩니다.
        }
    }

}