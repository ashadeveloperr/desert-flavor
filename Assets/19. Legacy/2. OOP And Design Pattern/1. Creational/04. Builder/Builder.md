# Builder (빌더) 패턴
> 복잡한 객체를 `단계별로 조립`해서, 같은 절차로
> 다양한 결과물을 만드는 방법

---

## 1. Builder ?
객체를 한번에 생성자로 찍어내지 않고, `여러 단계로
 나눠서 조립`한다. 선택 옵션이 많은 객체를 
`부품을 하나씩 붙이듯` 만들며, 코드가 문장처럼
읽히는 구조를 갖는다.

## 2. 상황
옵션이 많은 무기를 생성자로 만들면 매개변수가 폭발한다

```csharp
// 매개변수가 10개... 순서 외우기도, 읽기도 어려움
var sword = new Sword("Doomfang", 3000, ElementType.Fire, Grade.Unique,
        true, false, 2, null, "옵션A", "옵션B")
// "일부만 설정"하려고 생성자를 여러개 만들면
// 이번엔 생성자 자체가 폭발합니다.
```
> `옵션이 많아질수록 생성자가 길고 애매해져`, 
> 무엇을 넘기는지 알기 어렵다

---

## 3. 적용
단계별 함수를 만들고, 각 함수가 `자기 자신(this)을
반환`해 체인으로 잇는다

```csharp
public class SwordBuilder
{
    private Sword = sword = new Sword(); //미완성 상태로 시작
    
    public SwordBuilder SetGrade(Grade g)
    {
        sword.Grade = g;
        return this;
    }
    
    public SwordBuilder SetName(string n)
    {
        sword.Name = n;
        return this;
    }
    
    public SwordBuilder AddOption(string o)
    {
        sword.Options.Add(o);
        return this;
    }
    
    public Sword Build()
    {
        return sword;
    }
}    
 
// 사용부
Sword longSword = new SwordBuilder()
    .SetGrade(Grade.Unique)
    .SetName("Long Sword")
    .AddOption("+50% Critical Damage")
    .Build();
    
Sword gungnir = new SwordBuilder()
    .SetGrade(Grade.Legendary)
    .SetName("Gungnir")
    .AddOption("+150% Critical Damage")
    .AddOption("+100% Normal Damage")
    .AddOption("+50% Hp Regen")
    .Build();
```
> 필요한 옵션만 골라 조립하게 되어
> `가독성`이 상승한다.
> 같은 빌더를 사용해서 `다양한 결과물을 생성`할 수 있다




