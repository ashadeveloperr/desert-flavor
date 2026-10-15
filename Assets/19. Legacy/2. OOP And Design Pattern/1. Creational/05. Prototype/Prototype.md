# Prototype (프로토타입) 패턴
새로 만들지 않고 `기존 원본을 복제(Clone)`해서 
객체를 찍어내는 패턴.

## Prototype ?
이미 설정을 마친 원본(prototype)을 두고,
필요할 때마다 그것을 `복제`해 새 인스턴스
를 만든다. 복잡한 초기 설정을 매번 반복
하지 않고, `완성된 견본`을 도장 찍듯 찍어낸다.

> Unity의 프리팹(Prefab)이 바로 프로토타입.

## 상황
적을 스폰할 때마다 
수많은 초기 설정을 코드로 반복하면?

```csharp
Enemy SpawnEleteGoiblin()
{
    Enemy e = new Enemy();
    e.Name = "정예 고블린";
    e.MaxHp = 300;
    e.Hp = 300;
    e.Speed = 2.5f;
    e.Skills.Add("날뛰기");
    e.Skills.Add("난동부리기");
    e.Resistances = new[] 
    {
        Element.Fire,
        Element.Ice
    }
    return e;
}
```
> 복잡한 설정을 생성할 때마다 통쨰로 반복한다.
>값 하나가 바뀌면 여러곳을 고쳐야 한다.

---
## 적용
완성된 원본을 이용해서 복제하여 처리한다.
```csharp
// Struct는 값복사가 자동으로 일어나기에 클래스만 다룹니다.
transform.postion = Vector3.zero;

// Struct는 대입만 해주면 바로 사본을 이용해서 사용 가능
Player.LocalPlayer.transform.poistion = transform.position 
    
//기존의 target을 이용해서 두번째 타겟을 만들고 싶다면 아래처럼 사용할수 없음
Transform target = Player.LocalPlayer.transform;
Transform target2 = target; 
// target2는 Player.LocalPlayer.transform의 주소값

public class Enemy
{
    public string Name;
    public int MaxHp;
    public int Hp;
    public List<string> Skills = new();
    
    public Enemy Clone()
    {
        // .MemberwiseClone() : 현재 오브젝트의 단순 복사본(얕은 복사)
        // 을 만듭니다.
        Enemy copy = (Enemy)this.MemberwiseClone();
        copy.Skills = new List<string>(this.Skills); // 참조형은 따로 복사 해줘야 안전하다.
        // new List<T>(List<T> prototype) 
        // : 매개변수를 참고하여 해당 개체의 
        //  내용을 복사하여 추가 후, 반환합니다.
        
        // 아래처럼 해도 상관 없음. 동일함
        List<string> newSkills = new List<string>();
        
        for(int i = 0; i < Skills.Count; ++i)
        {
            newSkills.Add(Skills[i]);
        }
        
        copy.Skills = newSkills;
    }
}

// 원본(견본)은 한번만 설정해둔다
Enemy eliteGoblin = new Enemy {..., ...,};

// 스폰은 복제 한줄로
Enemy a = eliteGoblin.Clone();
Enemy b = eliteGoblin.Clone();

```

- 설정을 한 곳(원본)에 모으는 효과
- 스폰이 간단하게 구현이 된다
- 런타임에 견본을 바꿔가며 변형 생성이 가능하다

## 예시
 Unity의 `Instantiate(GameObject)`이 프로토 타입 패턴이다.
해당 함수 안에 깊은복사, 얕은 복사 처리들이 유니티에서 설정한 로직대로 
수행되어 런타임에 복사본이 생성이 된다.

 보통 프로젝트에서 런타임에 자주 생성되는 객체들의 생성 로직을 모아두어서 처리한다.




