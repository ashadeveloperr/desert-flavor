using System;
using System.Collections.Generic;
using UnityEngine;

namespace Study_DataStructure
{
    public class Study_Hashset : MonoBehaviour
    {
        private void Start()
        {
            StudyHashset();
        }

        // HashSet<T> : "중복이 없는 주머니"
        // - 중복을 허용하지 않는 컬렉션.
        // - 이 주머니에는 같은 물건을 두 번 넣을 수 없습니다. 이미 있는 물건을
        //  또 넣으려고 한다면, 그냥 무시됩니다. (오류가 나지 않고, 추가도 안됨)
        // - 주머니 안의 물건들은 특별한 순서 없이 마구 섞여 있습니다.
        // - 빠른 검색 및 존재 여부 확인이 가능합니다. 특정 물건이 주머니 안에
        //  있는지 없는지를 매우 빠르게 확인할 수 있습니다.

        private void StudyHashset()
        {
            // 1. HashSet 생성 : 타입 정의해주기
            // string(문자열)만 담을 수 있는 Hashset을 만들어 봅시다.
            HashSet<string> uniqueColors = new HashSet<string>();

            Debug.Log($" 1. HashSet 생성 : 타입 정의해주기");
            Debug.Log($" 주머니 안에 색깔 개수 : {uniqueColors.Count}");
            Debug.Log("");

            // 2. 데이터 추가 : .Add()
            uniqueColors.Add("빨강");
            uniqueColors.Add("초록");
            uniqueColors.Add("파랑");

            Debug.Log(" 2. 색깔 3개 추가 후");
            Debug.Log($" 주머니 안에 색깔 개수 : {uniqueColors.Count}");
            Debug.Log("");

            // 중복된 데이터 추가 시도
            Debug.Log(" 2.1 중복된 데이터 추가 시도 (빨강)");
            bool addedRed = uniqueColors.Add("빨강");
            Debug.Log($"'빨강'이 추가 되었나요? {addedRed}"); // False 출력
            Debug.Log($" 주머니 안에 색깔 개수 : {uniqueColors.Count}");
            Debug.Log("");

            // 3. 데이터 존재 여부 확인 : .Contains()
            Debug.Log(" 3. 특정 색깔이 있는지 확인하기");
            Debug.Log($"'파랑이 있나요?' : {uniqueColors.Contains("파랑")}"); // True
            Debug.Log($"'노랑이 있나요?' : {uniqueColors.Contains("노랑")}"); // False
            Debug.Log("");

            // 4. 데이터 삭제 : .Remove()
            Debug.Log("4.1 없는 색깔 삭제 시도 (노랑)");
            bool removedYellow = uniqueColors.Remove("노랑");
            Debug.Log($"'노랑'이 삭제되었나요? : {removedYellow}"); // false
            Debug.Log($" 주머니 안에 색깔 개수 : {uniqueColors.Count}");
            Debug.Log("");

            Debug.Log("4.2 파랑 색깔 삭제 ");
            bool removedBlue = uniqueColors.Remove("파랑");
            Debug.Log($"'파랑'이 삭제되었나요? : {removedBlue}"); // true
            Debug.Log($" 주머니 안에 색깔 개수 : {uniqueColors.Count}");
            Debug.Log("");

            // 5. 모든 데이터 요소 확인 : 반복문 (Foreach)
            Debug.Log($"5. 모든 데이터 요소 확인 : 반복문 (Foreach)");
            foreach (string color in uniqueColors)
            {
                Debug.Log($"색깔 : {color}");
            }

            Debug.Log($"====================================");

            // 6. 집합 연산 (UnionWith, IntersectWith)
            // - HashSet의 강력한 기능 중 하나는 집합 연산입니다.
            HashSet<string> moreColor =
                new HashSet<string> { "초록", "노랑", "보라" };

            Debug.Log($"6. 다른 색깔 주머니와 합치기");
            uniqueColors.UnionWith(moreColor);

            foreach (string color in uniqueColors)
            {
                Debug.Log($"6. 색깔 : {color}");
            }
            Debug.Log("");

            HashSet<string> commonColors = 
                new HashSet<string> { "빨강", "파랑", "검정" };
                                    // 빨강, 초록, 노랑, 보라

            uniqueColors.IntersectWith(commonColors);

            foreach (string color in uniqueColors)
            {
                Debug.Log($" 공통된 색깔 : {color}");
            }
            Debug.Log("");
        }

        // # 정리 : HashSet<T>은 언제 사용할까요?
        // 1. 중복 없는 고유한 데이터 목록이 필요할 때 : 
        //  - 웹사이트 방문자 목록, 태그 목록, 고유한 사용자 id 목록 등
        // 2. 특정 데이터가 컬렉션에 존재하는지 빠르게 확인해야할 때 :
        //  - 대량의 데이터 중에서 특정 항목의 포함 여부를 자주 검사해야 할 경우
        //   List<T>보다 훨씬 빠릅니다.
        // 3. 집합 연산 (합집합, 교집합, 차집합, 등)을 수행해야 할 때 : 
        // - 두 그룹 간의 공통점이나 차이점을 찾을 떄 유용합니다. 

    }
}