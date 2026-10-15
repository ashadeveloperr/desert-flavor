
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Study_OOP_And_Design_Pattern.Structural.Composite
{
    // Composite - 예시 B : 제네릭 트리
    // 아이디어 : 담는 값의 타입만 다른 트리를, 제네릭 TreeNode<T> 하나로
    //           재사용 해보자!

    // 스스로 Leaf가 될수도 있고, Composite가 될 수도 있음.
    // T감싸는 객체를 만드는게 핵심.
    public class TreeNode<T>
    {
        public T Value;
        public List<TreeNode<T>> Children = new List<TreeNode<T>>();
        public TreeNode(T value) { this.Value = value; }

        public TreeNode<T> Add(T value)
        {
            TreeNode<T> child = new TreeNode<T>(value);
            Children.Add(child);
            
            // 만든 자식을 반환해서 계속 추가하게 해줌
            // 이거는 구조마다 조금 다르게 사용
            return child; 
        }

        // Action?
        // : "함수를 가리키는 변수" 값이 아니라 함수(메서드를) 보관했다가
        //  나중에 호출하는 방식입니다. 함수의 구체적인 내용은 나중에 줄게!
        // EX) 1. Action<int>에 담을 수 있는 함수는?
        //      => void Print(int number), void AddInt(int number)
        //     2. Action<int, int>에 담을 수 있는 함수는?
        //      => void Sum(int, int), void Minus(int, int)
        
        // 트리 전체에 같은 작업을 적용한다.
        public void ForEach(Action<T, int> action, int depth = 0)
        {
            action(Value, depth);

            for (int i = 0; i < Children.Count; ++i)
            {
                Children[i].ForEach(action, depth + 1);
            }
        }
    }
    
    public class GenericTreeDemo : MonoBehaviour
    {
        private void Start()
        {
            TreeNode<string> root = new TreeNode<string>("사장");
            
            TreeNode<string> dev = new TreeNode<string>("개발 팀장");
            dev.Add("클라이언트 A");
            dev.Add("클라이언트 B");
            dev.Add("서버 A");
            
            TreeNode<string> gameDesign = new TreeNode<string>("기획 팀장");
            gameDesign.Add("시스템 기획자 A");
            gameDesign.Add("시스템 기획자 B");
            gameDesign.Add("밸런스 기획자 A");
            gameDesign.Add("콘텐츠 기획자 A");
            gameDesign.Add("콘텐츠 기획자 B");
            gameDesign.Add("콘텐츠 기획자 C");
            gameDesign.Add("레벨 디자이너 A");
            
            TreeNode<string> art = new TreeNode<string>("아트 팀장");
            art.Add("아트 디렉터");
            art.Add("Environment Artist A");
            art.Add("Environment Artist B");
            art.Add("Character Artist B");
            art.Add("Character Artist B");

            root.Children.Add(dev);
            root.Children.Add(gameDesign);
            root.Children.Add(art);
            
            root.ForEach(PrintPosition,0);
            Debug.Log("======================");
            root.ForEach(Print,0);
        }

        private void PrintPosition(string str, int depth)
        {
            string empty = new string(' ', depth * 2);
            Debug.Log($"{empty} - {str}");
        }
        
        private void Print(string str, int depth)
        {
            string empty = new string(' ', depth * 2);
            Debug.Log($"{empty} - {depth}");
        }
    }
}