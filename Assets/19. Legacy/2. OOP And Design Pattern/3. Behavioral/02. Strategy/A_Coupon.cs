using UnityEngine;

namespace Study_OOP_And_Design_Pattern.Behavioral
{
    // Strategy - 스타벅스 쿠폰 사용 상황
    // : 강사의 아침에 맞이하는 상황을 즉석에서 모델링을 했음
    
    public class Item
    {
        public string Name;
        public string Category;
        public int Price;

        public Item(string name, string category, int price)
        {
            Name = name;
            Category = category;
            Price = price;
        }

        public override string ToString()
        {
            return $"{Name} , {Category}, {Price}";
        }
    }

    public interface ICoupon
    {
        bool IsUsed { get; }
        Item SetDiscount(Item targetItem);
    }

    public class OneMoreCoffee : ICoupon
    {
        // 지정된 커피를 60% 할인된 가격으로 구매할 수 있음

        public bool IsUsed { get; private set; }
        public string TargetName { get; private set; }

        public OneMoreCoffee(string targetName)
        {
            IsUsed = false;
            TargetName = targetName;
        }

        public Item SetDiscount(Item targetItem)
        {
            if (IsUsed) return targetItem;
            if (targetItem.Name.Equals(TargetName) == false) 
                return targetItem;
            IsUsed = true;
            
            targetItem.Price = Mathf.FloorToInt(targetItem.Price * 0.4f);
            return targetItem;
        }
    }
    
    public class OnePlusOneCoffee : ICoupon
    {
        // 커피 하나를 사면 똑같은 커피를 한잔 더 줌
        public bool IsUsed { get; private set; } = false;

        public Item SetDiscount(Item targetItem)
        {
            if (IsUsed) return targetItem;
            IsUsed = true;
            targetItem.Name = $"{targetItem.Name},{targetItem.Name}";
            return targetItem;
        }
    }

    public class SummerFood : ICoupon
    {
        // 푸드 계열을 30%할인해줌
        public bool IsUsed { get; private set; } = false;

        public Item SetDiscount(Item targetItem)
        {
            if(IsUsed) return targetItem;
            if(targetItem.Category.Equals("식품") == false) 
                return targetItem;
            
            IsUsed = true;
            targetItem.Price = Mathf.FloorToInt(targetItem.Price * 0.7f);
            return targetItem;
        }
    }

    public class DiscountDemo : MonoBehaviour
    {
        private void Start()
        {
            Item[] bucket = new[]
            {
                new Item("콜드브루", "커피", 6100),
                new Item("오늘의커피", "커피", 5800),
                new Item("아메리카노", "커피", 6100),
                new Item("BLT 샌드위치", "식품", 4800),
            };

            ICoupon[] coupons = new ICoupon[]
            {
                new OneMoreCoffee("콜드브루"),
                new OneMoreCoffee("아메리카노"),
                new OnePlusOneCoffee(),
                new OnePlusOneCoffee(),
                new SummerFood(),
            };

            for (int i = 0; i < bucket.Length; i++)
            {
                foreach (var coupon in coupons)
                {
                    bucket[i] = coupon.SetDiscount(bucket[i]);
                }
            }

            for (int i = 0; i < bucket.Length; i++)
            {
                Debug.Log(bucket[i]);
            }
        }
    }
}