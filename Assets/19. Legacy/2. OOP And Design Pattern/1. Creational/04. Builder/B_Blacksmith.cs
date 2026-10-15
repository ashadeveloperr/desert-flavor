using System;
using System.Collections.Generic;
using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace Study_OOP_And_Design_Pattern.Creational.Builder
{
    // Builder - 예시 : Director 포함 빌더
    // 상황 : 등급별 '정해진 순서(레시피)'로 검을 만든다. Director의
    //      멤버변수를 활용하여 다양한 객체를 생성한다.

    public enum ElementType { None = 0, Fire, Ice, Leaf, }
    public enum Grade {Normal, Magic, Unique, Legendary}
    
    public class Sword
    {
        public string Name = "미완성 도끼";
        public int Atk = 1;
        public ElementType ElementType = ElementType.None;
        public Grade Grade = Grade.Normal;
        public List<string> Options = new List<string>();
        
        public override string ToString()
        {
            string reVal;
            // 아래의 switch 문은 패턴 매칭식 스위치 문이라고 합니다

            string colorCode = Grade switch
            {
                Grade.Normal => "#FFFFFF",
                Grade.Magic => "#4EA5F5",
                Grade.Unique => "#F5D64E",
                Grade.Legendary => "#C74EF5",
                _ => "#FFFFFF"
            };

            string optionText = Options != null && Options.Count > 0
                ? string.Join("\n    - ", Options)
                : "없음";

            return $"<color={colorCode}><b>[{Grade}] {Name}</b></color>\n" +
                   $"<b>공격력:</b> {Atk}\n" +
                   $"<b>속성:</b> {ElementType}\n" +
                   $"<b>옵션:</b>\n    - {optionText}";
            
            // 위의 스위치 문은 아래와 동일
            // string color;
            //
            // switch (Grade)
            // {
            //     case Grade.Normal:
            //         color = "#FFFFFF";
            //         break;
            //     case Grade.Magic:
            //         color = "#4EA5F5";
            //         break;
            //     case Grade.Unique:
            //         color = "#F5D64E";
            //         break;
            //     case Grade.Legendary:
            //         color = "#C74EF5";
            //         break;
            //     default:
            //         color = "#FFFFFF";
            //         break;
            // }
            
            
            return base.ToString();
        }
    }

    public class SwordBuilder
    {
        private Sword sword;

        public SwordBuilder() { sword = new Sword(); }
        public SwordBuilder(Sword sword) { this.sword = sword; }

        public Sword Build() { return sword; }

        protected SwordBuilder SetName(string name) 
        { sword.Name = name; return this; }
        
        protected SwordBuilder SetAtk(int atk) 
        { sword.Atk = atk; return this; }

        protected SwordBuilder SetGrade(Grade grade) 
        { sword.Grade = grade; return this; }

        protected SwordBuilder SetElementType(ElementType elementType) 
        { sword.ElementType = elementType; return this; }

        protected SwordBuilder AddOption(string option) 
        { sword.Options.Add(option); return this; }
    }
    
    public class Blacksmith : SwordBuilder
    {
        public ElementType ElementType = ElementType.Ice;
        public int ChanceOfLegendary = 10;
        public int ChanceOfUnique = 20;

        public int MinAtk = 10;
        public int MaxAtk = 50;

        public string[] LegendaryNames =
        {
            "궁니르", "엑스칼리버", "서리한"
        };
        
        private Grade RollGrade()
        {
            int roll = Random.Range(0, 100);
            // 두개 더해야지 Unique 확률이 20%가 됨
            
            if (roll < ChanceOfLegendary) { return Grade.Legendary; }
            else if (roll  < ChanceOfUnique + ChanceOfLegendary) { return Grade.Unique; }
            else { return Grade.Normal; }
        }

        private string GetName(Grade grade)
        {
            int randomIndex = Random.Range(0, LegendaryNames.Length);

            return grade switch
            {
                Grade.Legendary => LegendaryNames[randomIndex],
                Grade.Unique => "유니트 롱소드",
                _ => "롱소드"
            };
        }
        
        public Sword MakeSword()
        {
            Grade grade = RollGrade();
            string name =  GetName(grade);
            int atk = Random.Range(MinAtk, MaxAtk);
            
            SetName(name);
            SetAtk(atk);
            SetElementType(ElementType);
            SetGrade(grade);

            int optionCount = 0;
            if (grade >= Grade.Unique) optionCount++;
            if (grade >= Grade.Legendary) optionCount++;
            for (int i = 0; i < optionCount; i++) { AddOption("+50% 공격력 증가"); }

            return Build();
        }
    }

    
}