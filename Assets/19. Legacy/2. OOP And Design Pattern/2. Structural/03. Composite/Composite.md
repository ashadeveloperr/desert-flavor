# Composite (컴포지트) 패턴

> **개별 객체**와 **객체 묶음**을 똑같은 방식으로 다루게 해주는 트리 구조 패턴.

---

## 1. Composite 패턴이란?

잎(개별)과 가지(묶음)를 **같은 인터페이스**로 다뤄, 트리 전체를 하나처럼 조작한다.
"이게 하나짜리냐 묶음이냐"를 사용하는 쪽이 구분하지 않아도 된다.

---

## 2. 상황

농장에서 밭 한 칸과 밭 그룹(구역)을 물 주기 처리한다. 개별과 묶음을 나눠 처리하면?

```csharp
void Water(object target)
{
    if (target is FarmTile tile)         tile.Water();
    else if (target is FarmTile[] group) // 그룹이면 반복
        foreach (var t in group) t.Water();
    // 그룹 안에 그룹(구역 안의 구역)이 생기면? 중첩 처리가 지옥이 된다.
}
```

**개별과 묶음을 다르게 처리하면, 중첩될수록 분기가 감당 안 된다.**

---

## 3. 적용

잎과 묶음이 **같은 인터페이스**를 구현한다. 묶음은 자식들에게 **재귀적으로 위임**한다.

```csharp
public interface IFarmComponent { void Water(); }        // 공통 계약

// 잎(개별) — 실제 일을 한다
public class FarmTile : IFarmComponent
{
    public void Water() => Debug.Log("한 칸에 물 주기");
}

// 가지(묶음) — 자식들에게 넘긴다
public class FarmGroup : IFarmComponent
{
    private readonly List<IFarmComponent> children = new();   // 잎이든 묶음이든 담는다
    public void Add(IFarmComponent c) => children.Add(c);

    public void Water()                                       // 자식 전부에게 재귀 위임
    {
        foreach (var c in children) c.Water();
    }
}

// 사용부 : 한 칸이든, 구역이든, 구역 안의 구역이든 — 똑같이 Water() 한 번.
IFarmComponent farm = BuildFarm();
farm.Water();
```

개별/묶음 **분기 제거** + 몇 겹 중첩이든 재귀로 자연스럽게 처리 + 트리 확장 자유.

---

> 다음 문서: **Decorator** — 객체를 감싸(장식) 기능을 런타임에 덧붙이는 패턴. (농사 예제 계속)
