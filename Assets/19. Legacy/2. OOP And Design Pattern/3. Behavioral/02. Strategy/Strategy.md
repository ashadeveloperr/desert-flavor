# Strategy (전략) 패턴
> **알고리즘(전략)** 을 객체로 만들어, 런타임에 갈아 끼울 수 있게 하는 패턴.

---

## 1. Strategy 패턴 ?

하나의 작업을 처리하는 여러 방법(알고리즘)을 각각 클래스로 분리하고, 사용하는 객체가
그중 하나를 **참조**해서 위임한다. 조건에 따라 참조만 바꾸면 동작이 통째로 달라진다.

---

## 2. 상황

디펜스 타워가 여러 적 중 하나를 조준한다. 조준 방식이 여러 가지인데 `if`로 처리하면?

``` csharp
Enemy PickTarget(List<Enemy> enemies)
{
    if (mode == "Nearest")   return FindNearest(enemies);
    else if (mode == "Strongest") return FindStrongest(enemies);
    else if (mode == "First")     return FindFirst(enemies);
    // 조준 방식이 늘 때마다 이 분기와 타워 코드를 계속 고친다.
}
```

> **동작 방식이 여러 개인데 한 곳에 `if`로 뭉쳐, 방식이 늘수록 수정 지옥이 된다.**

---

## 3. 적용

각 조준 방식을 **전략 클래스**로 분리하고, 타워가 전략을 **참조**해서 위임한다.

```csharp
public interface ITargetingStrategy { Enemy Pick(List<Enemy> enemies); }

public class NearestStrategy   : ITargetingStrategy { public Enemy Pick(List<Enemy> e) => /* 최근접 */; }
public class StrongestStrategy : ITargetingStrategy { public Enemy Pick(List<Enemy> e) => /* 최강 */; }

public class Tower
{
    private ITargetingStrategy strategy = new NearestStrategy();   // 기본 전략
    public void SetStrategy(ITargetingStrategy s) => strategy = s; // 런타임 교체

    public void Fire(List<Enemy> enemies)
    {
        Enemy target = strategy.Pick(enemies);   // 어떤 방식인지 몰라도 된다
        // target에게 발사
    }
}
```

타워 코드에서 `if` 제거 + 새 조준 방식은 **전략 클래스만 추가**(OCP) + 런타임 교체.

---

## 흔한 실수 / 주의점

- **State와 헷갈리기(중요).** 코드 모양이 비슷하다(둘 다 인터페이스를 참조).
  **State**는 "상태가 스스로 다음 상태로 **전이**"하는 것, **Strategy**는 "바깥이 **알고리즘을 골라 끼우는**" 것.
  전이가 있으면 State, 그냥 방식 교체면 Strategy.
- **전략에 상태를 잔뜩 넣기.** 전략은 보통 "입력 → 결과"의 순수 계산에 가깝다. 무거운 상태는 밖에 둔다.
- **전략이 하나뿐.** 방식이 하나면 굳이 분리할 필요 없다.

---