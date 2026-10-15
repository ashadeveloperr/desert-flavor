using System;
using UnityEngine;

namespace Study_DataStructure
{
    public class Study_Array2D : MonoBehaviour
    {
        public void Start()
        {
            Array2D();
        }

        // # 2차원 배열
        // 2차원 배열은 "바둑판", "엑셀 시트", "스프레드 시트" 입니다.
        // 1. 행(Row)과 열(Column)의 구조 : 데이터가 가로(행), 세로(열)로 정렬된
        //   격자 모양으로 저장됩니다.
        // 2. 고정된 크기 : 1차원 배열과 마찬 가지로, 배열을 만들 때 행과 열의 크기를
        //   미리 정해야 합니다 (낙장불입)
        // 3. 같은 종류만 보관 : 모든 칸에는 같은 종류의 물건(데이터)만 보관 가능.

        public void Array2D()
        {
            // 1. 2차원 배열 생성 및 초기화 : 행과 열의 크기를 지정하거나
            //   값을 바로 할당.

            // 다른언어의 "new int[3][3]"이 아님을 유의
            // 1-1 크기만 지정하고 나중에 값 할당.
            int[,] gameBoard = new int[3, 5]; // => 3x3 크기의 바둑판이 생김. 초기값 = 0

            Debug.Log("===1-1 크기만 지정하고 나중에 값 할당.===");
            Debug.Log($"gameBoard의 행 개수 : {gameBoard.GetLength(0)}");
            Debug.Log($"gameBoard의 열 개수 : {gameBoard.GetLength(1)}");
            Debug.Log($"gameBoard의 [0,0] 값 : {gameBoard[0, 0]}"); // 0 출력됨
            Debug.Log("");

            // 1-2 생성과 동시에 값 할당 (string형 2행 3열 배열)
            string[,] characters =
            {
                { "전사", "마법사", "궁수"},
                { "힐러", "탱커", "도적"}
            };

            Debug.Log($"characters [0, 0] 값 : {characters[0, 0]}");
            Debug.Log($"characters [0, 1] 값 : {characters[0, 1]}");
            Debug.Log($"characters [0, 2] 값 : {characters[0, 2]}");
            Debug.Log($"characters [1, 0] 값 : {characters[1, 0]}");
            Debug.Log($"characters [1, 1] 값 : {characters[1, 1]}");
            Debug.Log($"characters [1, 2] 값 : {characters[1, 2]}");
            Debug.Log("");

            // 재할당
            characters[1, 0] = "서포터";
            Debug.Log($"characters [1, 0] 값 : {characters[1, 0]}");

            // 2. 모든 데이터 확인 : 순회, 중첩 반복문
            Debug.Log("===2. 모든 데이터 확인 : 순회, 중첩 반복문===");

            string log = "로그창 클릭\n---\n"; // => 잘보이게 할려고 해놓은거임

            //{ "전사", "마법사", "궁수"},
            //{ "힐러", "탱커", "도적"}

            for (int row = 0; row < characters.GetLength(0); row++)
            {
                for(int col = 0; col < characters.GetLength(1); col++)
                {
                    log += $"{characters[row, col]},";
                }

                log += "\n";
            }

            Debug.Log(log);

            foreach(string character in characters)
            {
                Debug.Log(character);
            }

            // # 정리 : 2차원 배열은 언제 사용할까요?

            // 1. 격자 형태의 데이터를 표현할 때 :
            // - 게임 맵 (예: RPG 게임의 필드, 지뢰 찾기 보드)
            // - 이미지 데이터 (픽셀의 RPG 값).
            // - 수학의 행렬(Matrix) 연산.

            // 2. 행과 열로 구성된 테이블 형태의 데이터를 저장할 떄:
            // - 간 단한 표 형태의 데이터 (예: 학생들 점수표 등등)
            
            // 3. 고정된 크기의 2차원 공간이 필요할 때:
            // - 1차원 배열과 마찬가지로, 크기가 미리 정해져 있고
            //  변경될 일이 없을때 효율적 입니다.

        }
    }
}