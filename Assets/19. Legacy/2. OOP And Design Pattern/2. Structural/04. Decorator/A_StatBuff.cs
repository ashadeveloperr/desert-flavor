using System;
using System.Collections.Generic;
using UnityEngine;

namespace Study_OOP_And_Design_Pattern.Structural.Decorator
{
    // Decorator - 예시 A : 스탯 버프 중첩
    // 상황 : 기본 스탯 위에 버프/디버프를 런타임에 겹겹이 덧씌운다.

    public interface IStat 
    {
        int Attack { get; } int Hp { get; } int Speed { get; }
        Stat GetStat(Stat stat);
    }

    public class Stat : IStat
    {
        public int Attack { get; private set; }
        public int Hp { get; private set;}
        public int Speed { get; private set;}

        public Stat(int atk, int hp, int speed)
        {
            Attack = atk;
            Hp = hp;
            Speed = speed;
        }
        
        public Stat GetStat(Stat current) { return this; }
        public static IStat operator +(Stat a, Stat b)
        {
            Stat reval = 
                new Stat(a.Attack +  b.Attack, 
                    a.Hp + b.Hp, 
                    a.Speed + b.Speed);
            return reval;
        }
    }
    
    public class DecoHandler
    {
        public IStat GetBase() => baseStat;
        public IStat GetAdded() => Calculate();
        public IStat GetCurrent() => (baseStat + (Calculate() as Stat));  
        
        private Stat baseStat;
        private Stat addedStat;
        
        // (baseStat)를 조회가능하게 함수로 맹글어 놓는게 좋음
        // (addedStat)를 조회가능하게 함수로 맹글어 놓는게 좋음
        // (baseStat + addedStat)를 조회가능하게 함수로 맹글어 놓는게 좋음
        
        private List<IStat> buffList = new List<IStat>();

        public DecoHandler(Stat baseStat)
        {
            this.baseStat = baseStat;
        }
        
        private IStat Calculate()
        {
            Stat stat = new Stat(0,0,0);

            for (int i = 0; i < buffList.Count; i++)
                stat = buffList[i].GetStat(stat);
             
            return stat;
        }
        
        // 버프 추가는 단순하게 구현
        public void Add(IStat buffDebuff) 
        {
            buffList.Add(buffDebuff);
        }

        // 지울때는 타입검사를 해서 먼저 들어온 타입이 없어지도록 설정
        public void Remove(Type type)
        {
            for (int i = 0; i < buffList.Count; i++)
            {
                if(buffList[i].GetType() == type) 
                    buffList.RemoveAt(i);
            }
        }
    }
    
    public abstract class StatDecorator : IStat
    {
        public abstract int Attack { get; }
        public abstract int Hp { get; }
        public abstract int Speed { get; }

        public abstract Stat GetStat(Stat stat);
    }
    
    public class AttackBoost : StatDecorator
    {
        public override int Attack { get; } = 5;
        public override int Hp { get; }
        public override int Speed { get; }
        
        public override Stat GetStat(Stat stat)
        {
            return new Stat(stat.Attack + Attack, stat.Hp + Hp, stat.Speed + Speed);
        }
    }

    public class AttackDecrease : StatDecorator
    {
        public override int Attack { get; } = -5;
        public override int Hp { get; }
        public override int Speed { get; }
        
        public override Stat GetStat(Stat stat)
        {
            return new Stat(stat.Attack + Attack, stat.Hp + Hp, stat.Speed + Speed);
        }
    }
    
    public class HpBoost : StatDecorator
    {
        public override int Attack { get; }
        public override int Hp { get; } = 100;
        public override int Speed { get; }
        
        public override Stat GetStat(Stat stat)
        {
            return new Stat(stat.Attack + Attack, stat.Hp + Hp, stat.Speed + Speed);
        }
    }
    
    public class StatBuffDemo : MonoBehaviour
    {
        private void Start()
        {
            Stat baseStat = new Stat(10,100,1);
            DecoHandler handler = new DecoHandler(baseStat);
        
            handler.Add(new AttackBoost());
            handler.Add(new AttackBoost());
            IStat added = handler.GetAdded();
            Debug.Log($"[Added_1] {added.Attack},  {added.Hp}, {added.Speed}");
            //====================
            handler.Add(new HpBoost());
            handler.Add(new HpBoost());
            added = handler.GetAdded();
            Debug.Log($"[Added_2] {added.Attack},  {added.Hp}, {added.Speed}");
            //====================
            handler.Remove(typeof(AttackBoost)); // 10 -> 5로 감소
            added = handler.GetAdded();
            Debug.Log($"[Added_3] {added.Attack},  {added.Hp}, {added.Speed}");
        
            //====================
            IStat current = handler.GetCurrent();
            Debug.Log($"{current.Attack},  {current.Hp}, {current.Speed}");
        }
    }
}