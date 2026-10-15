# Command (커맨드) 패턴

> **요청(행동)을 객체로** 만들어, 실행·취소·큐잉·기록을 자유롭게 다루는 패턴.

---

## 1. Command 패턴이란?

"무엇을 해라"라는 요청을 **객체(Command)** 로 감싼다. 요청을 객체로 만들면
나중에 실행하거나, 되돌리거나(undo), 줄 세우거나(queue), 기록할 수 있다.

---

## 2. 상황

농사에서 경작·심기·철거를 즉시 실행하면, 되돌리기(undo)를 넣을 수 없다.

```csharp
void OnClick(Tile tile)
{
    if (tool == Tool.Till)   tile.Till();     // 바로 실행
    else if (tool == Tool.Plant) tile.Plant();
    else if (tool == Tool.Remove) tile.Remove();
    // 실수로 갈아엎었을 때 "되돌리기"를 하려면? 방법이 없다. 무엇을 했는지 기록이 없다.
}
```

> 문제 요약: **행동을 즉시 실행만 하면, 취소·재실행·기록·예약 같은 걸 붙일 수 없다.**

---

## 3. 적용

각 행동을 **명령 객체**로 만든다. `Execute`와 `Undo`를 갖게 하고, 실행한 명령을 스택에 쌓는다.

```csharp
public interface ICommand { void Execute(); void Undo(); }

public class PlantCommand : ICommand
{
    private readonly Tile tile;
    public PlantCommand(Tile tile) { this.tile = tile; }
    public void Execute() => tile.Plant();
    public void Undo()    => tile.Remove();   // 실행의 반대
}

public class FarmInput
{
    private readonly Stack<ICommand> history = new();

    public void Run(ICommand cmd) { cmd.Execute(); history.Push(cmd); }  // 실행 + 기록
    public void Undo() { if (history.Count > 0) history.Pop().Undo(); }  // 되돌리기
}
```

**실행취소(undo)** 가능 + 명령을 큐에 넣어 예약/재생 + 입력과 실제 동작을 분리.

---