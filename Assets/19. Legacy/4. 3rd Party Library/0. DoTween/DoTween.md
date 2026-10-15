# DoTween - 코드로 만드는 연출
> 이동, 회전, 페이드 같은 `애니메이션(보간)`을
> 코드 한줄로 만드는 트위닝 라이브러리


## 1. DoTween ?
DoTween은 `값을 시작에서 끝까지 시간에 따라
부드럽게 바꾸는` 트위닝 라이브러리 입니다.
- `트위닝(in-betweening)` : 시작값과 끝값만 주면 `사이를 자동으로 채워 준다`
  (애니메이터가 키 프레임 2개 사이를 그리듯, 보간을 적용하듯)
- `이징(easing)` : 그 변화의 속도 곡선(느리다가 빨라지기, 튕기기 등등)

우리는 아래의 내용처럼 보간을 이용하여 연출을 구현해 왔다.
```csharp
// 보간 코루틴
// 1초 동안 목표지점으로 이동하는 코루틴
IEnumerator MoveCoroutine(Vector3 endPos)
{
    Vector3 start = transform.position;
    float currentTime = 0.0f;
    float endTime = 1.0f;
    
    while(true)
    {
        if(currenTime > endTime) break;
        transform.position = 
            Vector3.Lerp(start, endPos, currentTIme / endTime);
        yield return null;
    }
    
    transform.position = endPos;
}
```
## 2. 적용
`using DG.Tweening` 네임스페이스 선언 후 
아래처럼 작성한다.
```csharp
// 1초 동안 목표지점으로 이동
transform.DOMove(endPos, 1f)
    .SetEase(Ease.OutQuad);
```

### 2.1 시퀀스
```csharp
Sequence seq = DOTween.Sequence();
seq.Append(transform.DOMove(target, 1.0f)); //이동
seq.Join(spriteRenderer.DOFade(0f,1f)); //동시에 페이드
seq.AppendInterval(0.5f); //0.5f간 대기
seq.AppendCallback(()=> Debug.Log("연출 끝"));
```

### 2.2 루프
```csharp
transform.DOMoveY(2.0f, 0.5f)
    .SetLoops(2, LoopType.Yoyo) // 왕복 2번
    .OnComplte(()=>Debug.Log("왕복 완료"))
```
### 2.3 요약
> 장점 : 연출을 선언적으로 한줄로 실행시킬 수 있다.
> 이징, 시퀀스, 콜백등을 조합해서 다양한 연출을 만들 수 있다
> 코루틴사용하지 않아도 된다.

