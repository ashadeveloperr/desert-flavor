# Facade (파사드) 패턴
> 복잡한 내부 시스템을 **단순한 창구 하나**로 감싸, 바깥은 그 창구만 쓰게 하는 패턴.

---

## 1. Facade 패턴이란?

여러 서브시스템의 복잡한 상호작용을, **간단한 상위 인터페이스(창구) 하나**로 감춘다.
바깥에서는 내부가 얼마나 복잡한지 몰라도, 창구에 요청만 하면 된다.

---

## 2. 상황

던전에서 공격이 명중했을 때 처리할 일이 많다. 호출부가 이걸 전부 알면?

```csharp
void OnHit(CombatEntity attacker, CombatEntity target)
{
    int dmg = target.CalculateFinalDamage(10);   // 1) 최종 데미지 계산
    ShowDamagePopup(dmg, target.HeadUpPivot);     // 2) 데미지 팝업
    target.TakeDamage(dmg);                        // 3) 실제 데미지 적용
    NotifyObservers(attacker, target, dmg);        // 4) UI/사운드에 통지
    if (target.IsDead) HandleDeath(target);        // 5) 사망 처리
    // 공격이 일어나는 모든 곳(플레이어, 함정, 투사체...)이 이 5단계를 매번 반복한다.
}
```

> **전투 처리 절차 전체를 호출부마다 알고 반복해, 규칙이 바뀌면 여러 곳을 고쳐야 한다.**

---

## 3. 적용

복잡한 5단계를 **전투 시스템 창구 하나**로 감춘다. 바깥은 "누가 누구에게 이 이벤트" 만 넘긴다.

```csharp
public class CombatSystem
{
    // 창구 : 안에서 계산·팝업·통지·사망을 알아서 조율한다.
    public void Send(CombatEntity attacker, CombatEntity target, CombatEvent e)
    {
        int dmg = target.CalculateFinalDamage(e.Amount);
        ShowPopup(dmg, target.HeadUpPivot);
        target.TakeDamage(dmg);
        NotifyObservers(attacker, target, e);
        // 사망 처리 등도 여기 내부에서
    }
}

// 호출부 : 딱 한 줄. 내부 복잡성은 전혀 몰라도 된다.
combatSystem.Send(player, enemy, new CombatEvent { Amount = 10 });
```

**무엇이 좋아졌나:** 호출부가 **단순해짐** + 전투 규칙이 **한곳(창구)** 에 모임 + 내부 구조를 바깥에서 감춤.

---