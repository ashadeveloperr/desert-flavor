using System;
using System.Collections;
using UnityEngine;

namespace Study_OOP_And_Design_Pattern.Creational
{
    // Factory Method - 예시 B : 아이템 드롭
    // 상황 : 몬스터마다 다른 "드롭 테이블"이 적용되어
    //      런타임에 아이템을 만들어서 드롭해야 한다.

    public abstract class Item { public abstract string Name { get; } }
    public class Potion : Item { public override string Name => "Potion"; }
    public class Sword : Item { public override string Name => "Sword"; }
    public class Gold : Item { public override string Name => "Gold"; }

    public abstract class LootTable
    {
        protected abstract Item CreateDrop();

        protected int RollDice()
        {
            return UnityEngine.Random.Range(0, 100);   
        }
        
        public Item Drop()
        {
            Item item = CreateDrop();
            Debug.Log($"[ItemDrop] {item.Name} !");
            return item;
        }
    }
    
    public class GoblinLoot : LootTable
    {
        protected override Item CreateDrop()
        {
            return (RollDice() < 70) ? new Gold() : new Potion();
        }
    }

    public class OrcLoot : LootTable
    {
        protected override Item CreateDrop()
        {
            int randNum = RollDice();
            
            if (randNum < 50) return new Gold();
            else if (randNum < 80) return new Potion();
            else return new Sword();
        }
    }

    public class LootDemo : MonoBehaviour
    {
        private LootTable table;
        
        private void Start()
        {
            // 상황 : 오크가 죽었음 아이템 5개를 뿌려야함

            LootTable table = SelectLootTable();
            
            for (int i = 0; i < 5; i++)
            {
                table.Drop();
            }
        }

        private LootTable SelectLootTable()
        {
            int randNum = UnityEngine.Random.Range(0, 3);
            
            switch (randNum)
            {
                case 0:
                    return new GoblinLoot();
                case 1:
                    return new OrcLoot();
                case 2:
                    return new DungeonLoot();
                default:
                    return new GoblinLoot();
            }
        }
    }
    
    // ----- 던전 보상 추가 부분

    public class DungeonLoot : LootTable
    {
        protected override Item CreateDrop()
        {
            return RollDice() < 70 ? new Gold() : new Sword();
        }
    }
}