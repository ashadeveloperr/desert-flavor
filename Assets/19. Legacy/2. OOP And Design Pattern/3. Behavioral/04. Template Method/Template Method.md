# Template Method (템플릿 메서드) 패턴

> 작업의 **뼈대(순서)** 는 부모가 고정하고, **일부 단계만** 자식이 채우게 하는 패턴.

---

## 1. Template Method 패턴이란?

부모가 알고리즘의 **전체 흐름**을 정해두고, 그중 달라지는 단계만 `abstract`/`virtual`로 열어
자식이 구현하게 한다. 순서는 고정, 세부만 교체.

---

## 2. 상황

적과 보스의 초기화 절차가 거의 같은데, 스탯 만드는 부분만 다르다. 복붙하면?

```csharp
class Enemy { void Setup() { GetComponents(); Stat = new EnemyStat(); Stat.ResetToFull(); Register(); } }
class Boss  { void Setup() { GetComponents(); Stat = new BossStat();  Stat.ResetToFull(); Register(); } }
// GetComponents/ResetToFull/Register 는 똑같이 복붙. 절차가 바뀌면 두 군데를 똑같이 고쳐야 한다.
```

> **거의 같은 절차를 복붙하고, 딱 한 단계만 다르다. 절차 변경이 여러 곳에 번진다.**

---

## 3. 적용

부모가 절차(템플릿)를 고정하고, **달라지는 단계만** 가상 함수로 연다.

```csharp
public abstract class Enemy
{
    // 템플릿 메서드 : 절차(순서)를 여기서 못 박는다.
    protected void Setup()
    {
        GetComponents();          // 공통
        Stat = CreateStat();      // ← 달라지는 단계 (자식이 결정)
        Stat.ResetToFull();       // 공통
        Register();               // 공통
    }

    protected abstract Stat CreateStat();   // 자식이 반드시 채우는 빈칸
}

public class Boss : Enemy
{
    protected override Stat CreateStat() => new BossStat();   // 이 단계만 바꾼다
}
```

공통 절차가 **한곳(부모)** 에 + 자식은 다른 단계만 구현 + 절차 변경이 한 번에 반영.

---