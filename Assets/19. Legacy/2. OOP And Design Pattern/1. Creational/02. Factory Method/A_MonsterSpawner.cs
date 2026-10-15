using System;
using UnityEngine;

namespace Study_OOP_And_Design_Pattern.Creational
{
    // Factory Method - 예시 A : 적 생성 (정석, 다형성 유지)
    // 상황 : 스포너 종류에 따라 다른 적을 만든다.
    // 형태 : 부모가 "만든다는 절차"만, 무엇을 만들지는 자식이 결정(가상 함수)

    public abstract class Monster : MonoBehaviour
    {
        public abstract string Name { get; }
        public float Speed { get; set; }
        public float Size { get; set; }

        public void Introduce() => Debug.Log($"[Factory A] {Name} 등장!");
    }

    public class Goblin : Monster { public override string Name => "Goblin"; }

    public class Orc : Monster
    {
        public override string Name => "Orc";
        public string Equipment = "";
    }

    public abstract class MonsterSpawner
    {
        protected abstract Monster CreateMonster();

        public Monster Spawn(Vector3 position)
        {
            Monster monster = CreateMonster();
            monster.transform.position = position;
            return monster;
        }
    }
    
    public class GoblinSpawner : MonsterSpawner
    {
        public float MinSpeed = 0;
        public float MaxSpeed = 5;
        
        protected override Monster CreateMonster()
        {
            GameObject gameObject = new GameObject("Goblin");
            Goblin monster = gameObject.AddComponent<Goblin>();
            monster.Speed = RollSpeed(MinSpeed, MaxSpeed);
            monster.Size = 1;
            return monster;
        }

        private float RollSpeed(float min, float max)
        {
            return UnityEngine.Random.Range(min, max);
        }
    }

    public class OrcSpawner : MonsterSpawner
    {
        public int MinSize = 1;
        public int MaxSize = 5;
        
        protected override Monster CreateMonster()
        {
            GameObject gameObject = new GameObject("Orc");
            Orc orc = gameObject.AddComponent<Orc>();
            orc.Size = RollSize(MinSize, MaxSize);
            orc.Equipment = RollEquipment();
            return orc;
        }
        
        private float RollSize(int min, int max)
        {
            return UnityEngine.Random.Range(min, max);
        }

        private string RollEquipment()
        {
            string[] items = { "양손도끼", "양손검", "양손 창" };
            int randIndex = UnityEngine.Random.Range(0, items.Length);
            return items[randIndex];
        }
        
    }
    
    public class MonsterFactoryDemo : MonoBehaviour
    {
        private MonsterSpawner monsterSpawner;
        
        private void Start()
        {
            SelectMonsterSpawner();
            Monster monster = monsterSpawner.Spawn(transform.position);
            monster.Introduce();
        }

        private void SelectMonsterSpawner()
        {
            int randNum = UnityEngine.Random.Range(0, 2);

            switch (randNum)
            {
                case 0:
                    monsterSpawner =  new GoblinSpawner();
                    break;
                case 1:
                    monsterSpawner =  new OrcSpawner();
                    break;
                case 2:
                    monsterSpawner =  new TrollSpawner();
                    break;
            }
        }
    }
    
    // ------------------ Troll 추가시 코드 ------------------

    public class Troll : Monster
    {
        public override string Name =>  "Troll";
        public string Equipment = "";
    }
    
    public class TrollSpawner : MonsterSpawner
    {
        protected override Monster CreateMonster()
        {
            GameObject gameObject = new GameObject("Troll");
            Troll troll = gameObject.AddComponent<Troll>();
            troll.Equipment = "몽둥이";
            return troll;
        }
    }
}