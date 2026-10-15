using UnityEngine;

namespace Study_OOP_And_Design_Pattern.Structural.Bridge
{
    // Bridge - 예시 A : 무기(기능축) x 속성(구현축)
    // 상황 : 무기 종류와 속성을 조합. 상속으로 곱하면 클래스가 폭발함.
    // 형태 : 무기(추상클래스)가 속성(인터페이스)을 '필드로 참조(다리)'
    //       (상속의 곱셉을 합성의 덧셈으로 바꿀수가 있다)

    public interface IEnchant { void Apply(); }
    public class FireEnchant : IEnchant { public void Apply() { Debug.Log("Fire Enchant"); } }
    public class IceEnchant :  IEnchant { public void Apply() { Debug.Log("Ice Enchant"); } }


    public abstract class Weapon
    {
        protected IEnchant enchant;
        
        // 속성은 외부에서 주입받는다.
        
        /// <summary>
        /// 매개변수 e의 null을 허용합니다.
        /// </summary>
        /// <param name="null 허용"></param>
        public void SetEnchant(IEnchant e) => enchant = e;
        public abstract void Attack();
    }
    
    public class Sword : Weapon { public override void Attack() 
        { Debug.Log("[Sword] Attacked"); enchant?.Apply(); } }
    public class Bow : Weapon { public override void Attack() 
        { Debug.Log("[Bow] Attacked"); enchant?.Apply();} }

    public class WeaponBrideDemo : MonoBehaviour
    {
        private Sword sword = new Sword();
        private Bow bow = new Bow(); 
        
        private void Start()
        {
            sword.SetEnchant(new FireEnchant());
            sword.Attack();
            
            sword.SetEnchant(new IceEnchant());
            sword.Attack();
            
            sword.SetEnchant(null);
            sword.Attack();
        }
    }
}