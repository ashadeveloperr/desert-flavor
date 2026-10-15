# Abstract Factory (추상 팩토리) 패턴
> 단일 객체가 아니라 서로 어울리는 객체 `세트`를
> 통째로 만들어서 공급하는 패턴


## 1. Abstract Factory?
서로 연관된 여러 객체(테마, 세트)를
`일관되게 묶어서` 생성하는 팩토리.
요청하는 쪽은 구체화된 클래스를 모른 채,
추상 패토리에 `이 테마 세트 하나 주세요`라고
요청한다.

## 2. 상황
디펜스에서 웨이브 테마(숲/용암)에 따라 
적,투사체,배경등을 바꾼다. 만약 하드 코딩으로
짝을 맞추면?

```csharp
if(theme == "Forest")
{
    SpawnEnemy(new ForestGoblin());
    SetProjectile(new LeafArrow());
    SetBackground(new ForestBg())
}
else if(theme == "Lava")
{
    SpawnEnemy(new LavaGolem());
    SetProjectile(new Fireball());
    SetBackground(new LavaBg()) 
}
```
> `세트 구성 요소`를 일일이 추가하다보면
> 실수도 많이 일어날 뿐더러 해당 절차를 수행하는
> 클래스의 의존성이 높아지게 된다. 의존성이 높아지게 
> 되면 결국 유지보수와 확장성이 떨어지게 된다.

## 3. 적용

```csharp
public abstract class WaveFactory
{
    public abstract Enemy CreateEnemy();
    public abstract Projectile CrateProjectile();
    public abstract Background CrateBackground();
}

public class ForestWaveFactory : WaveFactory
{
    public override Enemy CreateEnemy() => new ForestGoblin();
    public override Projectile CreateProjectile => new LeafArraw();
    public override Background CreateBackground => new ForestBg();
}

public class LavaWaveFactory : WaveFactory
{
    public override Enemy CreateEnemy() => new LavaGolem();
    public override Projectile CreateProjectile => new FireBall();
    public override Background CreateBackground => new LavaBg();
}
```
> 세트 일관성 보장 + 새 테마 추가시 새로운 `추상 팩토리 클래스`만 추가하면 됨
> (사용부 불변)
