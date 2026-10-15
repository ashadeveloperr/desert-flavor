using UnityEngine;
using System.Collections.Generic;

namespace Study_OOP_And_Design_Pattern.Creational.Prototype
{
    // ============================================================
    //  Prototype — 예시 A : 적 복제 (얕은 복사 vs 깊은 복사)
    //  상황 : 복잡하게 설정한 '견본' 적을 복제해 여러 마리 스폰.

    public class Enemy
    {
        public string Name;
        public int Hp;
        public List<string> Buffs = new List<string>(); 

        public Enemy Clone()
        {
            Enemy copy = (Enemy)MemberwiseClone();          // 얕은 복사
            copy.Buffs = new List<string>(this.Buffs);      // 참조 필드는 새로 만들어 깊은 복사
            return copy;
        }
        public override string ToString() => $"{Name}(HP {Hp}, 버프:{string.Join("/", Buffs)})";
    }

    public class EnemyCloneDemo : MonoBehaviour
    {
        private void Start()
        {
            Enemy proto = new Enemy { Name = "정예 고블린", Hp = 300 };
            proto.Buffs.Add("분노");

            Enemy a = proto.Clone();
            Enemy b = proto.Clone();
            
            b.Hp = 1; b.Buffs.Add("방어막");   // 복제본만 변함

            Debug.Log($"[Prototype A] 원본 : {proto}");   
            Debug.Log($"[Prototype A] 복제b: {b}");        
        }
    }
}