using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Study_OOP_And_Design_Pattern.Creational
{
    // Abstract Factory - 예시 A : 던전 테마 세트
    // 상황 : 테마에 따라 적 + 투사체 등을 "한 세트로 일관되게 만든다"

    public class ForestGoblin : Monster { public override string Name => "ForestGoblin"; }
    public class LavaGolem : Monster { public override string Name => "LavaGolem"; }
    
    public class ForestGoblinSpawner : MonsterSpawner
    {
        protected override Monster CreateMonster() => new GameObject().AddComponent<ForestGoblin>();
    }
    
    public class LavaGolemSpawner : MonsterSpawner
    {
        protected override Monster CreateMonster() => new GameObject().AddComponent<LavaGolem>();
    }

    public abstract class Projectile { public abstract string Name { get; } }

    public class LeafArrow : Projectile { public override string Name => "LeafArrow"; }
    public class FireBall : Projectile {public override string Name => "Fireball"; }

    public abstract class Background { public abstract string Name { get; } }
    
    public class Forest : Background { public override string Name => "Forest"; }
    public class Lava : Background { public override string Name => "Lava"; }

    public abstract class DungeonTheme
    {
        public abstract Monster CreateMonster();
        public abstract Projectile CreateProjectile();
        public abstract Background CreateBackground();
        public abstract Item[] CreateReward();
    }
    
    public class ForestDungeon : DungeonTheme
    {
        public override Monster CreateMonster() { return new ForestGoblinSpawner().Spawn(Vector3.zero); }
        public override Projectile CreateProjectile() { return new FireBall(); }
        public override Background CreateBackground() { return new Forest(); }
        public override Item[] CreateReward()
        {
            Item[] reward = new Item[10];
            for (int i = 0; i < reward.Length; i++)
            {
                reward[i] = new GoblinLoot().Drop();
            }

            return reward;
        }
    }

    public class DungeonThemeDemo : MonoBehaviour
    {
        private void Start()
        {
            DungeonTheme currentTheme = SelectTheme();
            Monster monster = currentTheme.CreateMonster();
            Projectile projectile = currentTheme.CreateProjectile();
            Background background = currentTheme.CreateBackground();
            Item[] reward = currentTheme.CreateReward();
        }

        private DungeonTheme SelectTheme()
        {
            int randNum = Random.Range(0, 2);
            if (randNum == 0) return new ForestDungeon();
            else return new LavaDungeon();
        }
    }
    
    // --------- Lava Dungeon 추가시 -------------
    
    public class LavaDungeon : DungeonTheme
    {
        public override Monster CreateMonster()
        {
            return new LavaGolemSpawner().Spawn(Vector3.zero);
        }

        public override Projectile CreateProjectile()
        {
            return  new FireBall();
        }

        public override Background CreateBackground()
        {
            return new Lava();
        }

        public override Item[] CreateReward()
        {
            Item[] items = new Item[20];

            for (int i = 0; i < items.Length; i++)
            {
                items[i] = new OrcLoot().Drop();
            }

            return items;
        }
    }
}