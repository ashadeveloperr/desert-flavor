# Bridge (브릿지) 패턴
**기능 축**과 **구현 축**을 분리해, 둘을 상속이 아닌 **합성(다리)** 으로 잇는 패턴.

---

## 1. Bridge ?

서로 독립적으로 변하는 두 축(예: 무기 종류 × 속성)을 각각의 계층으로 분리하고,
한쪽이 다른 쪽을 **필드로 참조(합성)** 해서 연결한다. 상속으로 조합하면 클래스가 폭발하는 걸 막는다.

---

## 2. 상황

무기 종류(검·활)와 속성(화염·빙결)을 **상속만으로** 조합하면?

```csharp
class FireSword : Sword { }
class IceSword  : Sword { }
class FireBow   : Bow   { }
class IceBow    : Bow   { }
// 무기 3종 × 속성 4종 = 12개 클래스. 속성 하나 추가하면 무기 수만큼 또 늘어난다. (조합 폭발)
```

> **두 축을 상속으로 곱하면 클래스가 (종류 × 속성)만큼 폭발한다.**

---

## 3. 적용

무기(기능 축)가 속성(구현 축)을 **필드로 참조**한다. 상속의 곱셈이 합성의 덧셈으로 바뀐다.

```csharp
public interface IEnchantment { void ApplyEffect(); }   // 구현 축
public class FireEnchantment : IEnchantment { public void ApplyEffect() => Debug.Log("화염!"); }
public class IceEnchantment  : IEnchantment { public void ApplyEffect() => Debug.Log("빙결!"); }

public abstract class Weapon                            // 기능 축
{
    protected IEnchantment enchantment;                 // ← 다리(bridge): 구현을 참조
    public void SetEnchantment(IEnchantment e) => enchantment = e;
    public abstract void Attack();
}

public class Sword : Weapon
{
    public override void Attack()
    {
        Debug.Log("베기");
        enchantment?.ApplyEffect();                     // 어떤 속성이든 갈아 끼울 수 있다
    }
}
```

**구조**
```
   Weapon (기능)  ──참조(다리)──▶  IEnchantment (구현)
     ├ Sword                         ├ FireEnchantment
     └ Bow                           └ IceEnchantment
   3종 + 4종 = 7클래스   (상속이면 3×4 = 12클래스)
```

클래스 수가 **곱셈 → 덧셈** + 두 축을 **독립적으로 확장** + 런타임에 속성 교체 가능.

---
