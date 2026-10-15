# Observer (옵저버) 패턴

> 어떤 객체의 상태가 변하면, **구독 중인 여러 객체에게 자동으로 알리는** 1:N 통지 패턴.

---

## 1. Observer 패턴이란?

알리는 쪽(주체)이 "변화가 생기면 알려줘"라고 등록한 **구독자들**에게 변화를 자동 통지한다.
주체는 구독자가 누구인지·무엇을 하는지 몰라도 된다.

---

## 2. 상황

플레이어 HP가 바뀌면 체력바·사운드·업적이 반응해야 한다. Player가 직접 다 부르면?

```csharp
void TakeDamage(int dmg)
{
    hp -= dmg;
    hpBarUI.Refresh(hp);         // Player가 UI를 직접 안다
    soundManager.PlayHurt();     // 사운드도 직접
    achievement.CheckLowHp(hp);  // 업적도 직접
    // 반응할 대상이 늘 때마다 Player를 계속 고쳐야 한다. (강한 결합)
}
```

> **알리는 쪽이 반응하는 쪽을 전부 직접 알아, 반응 대상이 늘 때마다 수정된다.**

---

## 3. 적용

"변화를 받아볼 수 있다"는 **인터페이스**를 정하고, 구독자를 리스트로 관리해 통지한다.

```csharp
public interface IHpObserver { void OnHpChanged(int hp, int maxHp); }   // 구독자 계약

public class Player
{
    private readonly List<IHpObserver> observers = new();
    public void Subscribe(IHpObserver o)   { if (observers.Contains(o) == false) observers.Add(o); }
    public void Unsubscribe(IHpObserver o) => observers.Remove(o);

    private void Notify() { foreach (var o in observers) o.OnHpChanged(hp, maxHp); }

    public void TakeDamage(int dmg) { hp -= dmg; Notify(); }   // 누가 듣는지 몰라도 된다
}

// 반응하는 쪽 : 각자 알아서
public class HpBarUI : MonoBehaviour, IHpObserver
{
    public void OnHpChanged(int hp, int max) { /* 체력바 갱신 */ }
}
```

알리는 쪽과 반응하는 쪽이 **분리(느슨한 결합)** + 새 반응은 구독자만 추가 + 1:N 통지.

---