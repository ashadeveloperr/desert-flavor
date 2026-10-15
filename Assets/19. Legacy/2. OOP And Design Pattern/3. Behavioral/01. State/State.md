# State (상태) 패턴
> 객체의 **상태마다 행동을 별도 클래스로 분리**해서, 
> 상태가 바뀌면 행동도 통째로 갈아끼우는 패턴.

---

## 1. State 패턴이란?
하나의 객체가 여러 상태를 가질 때, 각 상태의 행동을 **상태별 클래스**로 나눠 담고,
현재 상태 객체에게 일을 위임한다. 상태가 바뀌면 "현재 상태 객체"만 교체하면 된다.

---

## 2. 상황

통합예제 *Pattern Dungeon* 의 슬라임 몬스터를 만든다고 하자.
슬라임은 **대기 → 추격 → 공격 → 사망** 상태를 오간다.
패턴을 모르면 보통 `enum` + 거대한 `switch`로 짠다.

```csharp
public enum SlimeState { Idle, Chase, Attack, Dead }

public class Slime : MonoBehaviour
{
    private SlimeState state = SlimeState.Idle;

    private void Update()
    {
        // 상태가 늘어날수록 이 switch가 계속 부풀어 오른다.
        switch (state)
        {
            case SlimeState.Idle:
                // 대기 애니메이션, 플레이어가 가까우면 Chase로
                if (IsPlayerNear()) state = SlimeState.Chase;
                break;

            case SlimeState.Chase:
                MoveTowardPlayer();
                if (IsPlayerInAttackRange()) state = SlimeState.Attack;
                if (IsPlayerNear() == false)          state = SlimeState.Idle;
                break;

            case SlimeState.Attack:
                AttackPlayer();
                if (IsPlayerInAttackRange() == false) state = SlimeState.Chase;
                break;

            case SlimeState.Dead:
                // ...
                break;
        }
    }
}
```

- **한 파일이 계속 비대해진다.** 상태(빙결·경직·분열…)가 늘 때마다 `case`가 붙고, `Update` 하나에 모든 상태의 로직이 뒤엉킨다.
- **"진입/이탈 시 한 번만" 할 일을 넣기 어렵다.** (예: 공격 상태 진입 시 이동 멈춤, 이탈 시 원상복귀)
- **다른 몬스터가 같은 상태를 재사용할 수 없다.** Chase 로직이 Slime 안에 갇혀 있다.
- 상태 하나 고치려고 전체가 든 이 파일을 다시 건드려야 한다.

> **상태별 행동이 한 덩어리로 뭉쳐 있어, 늘어날수록 손대기 무서워진다.**

---

## 3. 패턴 적용

각 상태를 **클래스 하나씩**으로 쪼갠다. 공통 뼈대는 추상 클래스(또는 인터페이스)로 강제한다.

```csharp
// 모든 상태의 공통 약속. Enter / Update / Exit 3박자.
public abstract class SlimeState
{
    protected Slime Owner { get; }          // 상태가 조종할 본체(주입받음)
    protected SlimeState(Slime owner) { Owner = owner; }

    public virtual void Enter() { }         // 이 상태로 "들어온 순간" 1번
    public abstract void Update();          // 머무는 동안 매 프레임 (반드시 구현)
    public virtual void Exit() { }          // 이 상태를 "떠나는 순간" 1번
}

public class ChaseState : SlimeState
{
    public ChaseState(Slime owner) : base(owner) { }

    public override void Update()
    {
        Owner.MoveTowardPlayer();
        if (Owner.IsPlayerInAttackRange()) Owner.ChangeState(Owner.Attack);
        else if (Owner.IsPlayerNear() == false)    Owner.ChangeState(Owner.Idle);
    }
}

public class AttackState : SlimeState
{
    public AttackState(Slime owner) : base(owner) { }

    public override void Enter() => Owner.StopMove();  // 진입 시 딱 한 번 멈춘다
    public override void Update()
    {
        Owner.AttackPlayer();
        if (Owner.IsPlayerInAttackRange() == false) Owner.ChangeState(Owner.Chase);
    }
}
```

본체(Slime)는 이제 **현재 상태에게 위임만** 한다. 거대한 switch가 사라진다.

```csharp
public class Slime : MonoBehaviour
{
    public SlimeState Idle, Chase, Attack, Dead;   // 상태 객체들
    private SlimeState current;

    private void Awake()
    {
        Idle = new IdleState(this);
        Chase = new ChaseState(this);
        Attack = new AttackState(this);
        Dead = new DeadState(this);
        ChangeState(Idle);
    }

    // 상태 전이는 반드시 이 한 곳을 통한다 → Exit/Enter가 짝을 이뤄 호출된다.
    public void ChangeState(SlimeState next)
    {
        current?.Exit();
        current = next;
        current.Enter();
    }

    private void Update() => current.Update();   // 딱 한 줄!
}
```

| Before (enum+switch) | After (State 패턴) |
|---|---|
| 상태 추가 = 거대 switch 수정 | 상태 추가 = **새 클래스 하나 추가**(기존 코드 안 건드림) |
| 진입/이탈 1회 처리 어려움 | `Enter()` / `Exit()`로 깔끔하게 분리 |
| 로직이 Slime에 갇힘 | 상태 클래스를 다른 몬스터가 **재사용** 가능 |
| `Update`가 모든 상태를 앎 | `Update`는 `current.Update()` 한 줄 |

---