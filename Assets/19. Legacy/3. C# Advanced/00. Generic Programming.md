# Generic (제네릭) - `<T>`의 정체
> `타입을 나중에 정하는 코드.` 하나의 클래스/메서드로
> 여러 타입을 안전하게 다룬다.
 
## 1. 제네릭이란?
 클래스나 메서드를 만들 때 타입을 고정하지 않고 `T`
 라는 `빈칸`으로 비워두었다가, 사용하는 순간
 실제 타입을 끼워 넣는 문법. `List<T>`의 `T`가
 바로 그 빈칸 입니다.
 
> 우체국 택배 박스는 안에 책이 들어가든 신발이 들어가든
> `상자 자체는 하나의 규격`이다. `무엇을 담느냐`는
> 보내는 사람이 포장할 때 정해진다. 제네릭은 `규격 상자`
> 이고, `<T>`는 `이번 상자에 무엇을 담을지` 입니다.
> `List<int>`, `Dictionary<string, Item>`가
> 전부 이 상자입니다.

## 2. 상황(사용 시나리오)
타입만 다르고 로직은 똑같은 스택을 만든다고 합시다.
제네릭이 없으면 타입마다 복사해야 합니다.

```csharp
public class IntStack 
{
    public void Push(int v){...}
    public int Pop(){...}
}

public class StringStack 
{
    public void Push(string v){...}
    public string Pop(){...}
}
```
`object`로 담으면 하나로 합쳐짐. 하지만
이번엔 안정성을 잃습니다.

```csharp
public class ObjectStack
{
    public void Push(Object v){...}
    public Object Pop(){...}
}

ObjectStack stack = new ObjectStack();
stack.Push(10);
stack.Push("Jay");

string s = (stack.Pop()) as string
```

## 3. 적용 방안
`T`라는 빈칸을 만들어서 두 상황을 해결한다

```csharp
public class Stack<T>
{
    private T[] items = new();
    public void Push(T v){...}
    public T Pop(){...}
}

Stack<int> numbers = new Stack<int>();
numbers.Push(10);
int n = numbers.Pop();

Stack<string> names = new Stack<string>();
names.Push("Slime");
string s = names.Pop();
```

## 4. 제약 조건 - `where T :`
`T`가 아무 타입이면 `T`의 기능을 호출 할 수 없다.
`이런 타입만 허용`이라는 조건을 걸어 기능을 열어준다.

```csharp
// T는 반드시 MonoBehaviour여야 한다
// => 그래서 T를 컴포넌트처럼 다룰 수 있는 것 
public abstract class SingletonBase<T> where T : MonoBehaviour
{
    protected static T instance;
}

```
자주 쓰는 제약
- `where T : MonoBehaviour` (특정 부모 클래스)
- `where T : class` (참조형, 레퍼런스 타입)
- `where T : new()` (기본 생성자 보유 형태)

## 5. 제네릭 프로그래밍의 장점
- 중복 제거, 재사용성 증가
- 컴파일 타입 안전
- 캐스팅/박싱 비용 제거












