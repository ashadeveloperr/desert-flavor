# Factory Method (팩토리 메서드) 패턴
> 무엇을 만들지를 서브클래스가 정하도록, 
> 객체 생성을 `가상 함수`에 위임하는 방법(패턴)

## 1. Factory Method ?
객체를 생성하는 함수를 부모가 가상 함수로 열어두고,
`어떤 구체적 타입을 만들지는 자식이 결정`하는 구조.
사용하는 쪽은 `만들어줘`라고만 하고, 결과를 
추상타입으로 받는다.

## 2. 상황
디펜스 게임에서 웨이브마다 적을 스폰합니다.
생성부(Spawner라고 가정)에서 구체 클래스를
직접 고르면?

```csharp
//아래는 Spawner클래스의 함수 부분입니다
Enemy SpawnEnemy(string type)
{
    // 새로운 적 종류가 생길 때마다 아래의 분기를 계속 수정해야함
    if(type == "Goblin") return new Goblin();
    else if(type == "Orc") return new Orc();
    else if(type == "HobGoblin") return new HobGoblin();
```
> 문제점 : 생성부가 모든 구체 클래스를 알고 있고, 종류가 늘 때마다 생성부를 수정해야함

## 적용
`적을 만든다`는 `가상함수`로 열고, 스포너 종류별로
자식이 무엇을 만들지 정하면 됩니다.

```csharp
public abstract class EnemyFactory : MonoBehaviour
{
    protected abstract Enemy CreateEnemy();
    
    public Enemy Spawn(Vector2 pos)
    {
        Enemy e = CreateEnemy();
        e.transform.position = pos;
        return e
    }
}

public class GoblinFactory : EnemyFactory
{
    protected override Enemy CreateEnemy
    {
        return new Goblin();
    }
}

public class OrcFactory : EnemyFactory
{
    protected override Enemy CreateEnemy
    {
        return new Orc();
    }
}
```













