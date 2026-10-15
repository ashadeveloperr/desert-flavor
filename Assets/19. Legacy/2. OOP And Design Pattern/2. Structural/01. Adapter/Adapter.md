# Adapter (어댑터) 패턴
호환되지 않는 인터페이스를 **우리가 원하는 형태로 변환**해주는
중간 껍데기

## Adapter ?
서로 맞지 않는 두 인터페이스(클래스, 기능 연결 부 등) 사이에 `변환기`를 두어서
기존 코드를 고치지 않고 이어 붙인다.

---
## 상황
게임 상점에 외부 결제 API를 붙인다. 그런데 API마다
사용법이 제각각이다. (IAP 연동)

```csharp
// 아래의 예시는 보석 결제 시스템 예시입니다.

// 카카오 : 한번 요청으로 끝나는 간단한 API
if(user.platform == KaKao)
{
    kakaoAPI.Request(itemCode, userId);
}

// 라인 : 인증 토큰을 받고, 그 다음 결제가 진행되는 두 단계 구조
if(user.platform == Line)
{
    string token = lineAPI.GetAuthToken(userId);
    lineAPI.ExecutePayment(token, productId, price)
}

// 상점 코드가 API마다 다른 절차를 전부 알아야 한다.
// 연동할 플랫폼이 늘면 상점도 계속 뜯어 고쳐야 한다
```

## 적용
우리가 원하는 공통 인터페이스를 정하고, API별 어댑터가
그 형태로 변환한다.

```csharp

public interface IPaymentAdapter // 우리가 원하는 통일 된 창구
{
    void Pay(string userId, string itemId, int price);
}

public class KakaoPayAdapter : IPaymentAdapter
{
    void Pay(string userId, string itemId, int price)
    {
        kakaoAPI.Request(itemId, userId);
    }
}

public class LinePayAdapter : IPaymentAdapter
{
    void Pay(string userId, string itemId, int price)
    {
         string token = lineAPI.GetAuthToken(userId);
         lineAPI.ExecutePayment(token, itemId, price)
    }
}

// 로그인 코드

IPaymentAdapter payment =  user.platform == kakao ?
    new KakaoPayAdapter() : new LinePayAdpater();

// 아래는 상점 코드 (구매정보 생략)
public struct PurchaseInfo
{
    public string UserID;
    public string ProductID;
    public int Price;
}

public void Purchase(IPaymentAdapter payment, PurchaseInfo info)
{
    payment.Pay(info.UserID, info.ProductID, info.Price);
}

// 구매부
IPaymentAdapter payment = User.Payment;

PurchaseInfo info = new();
info.UserID = User.UserID;
info.ProductID = Shop.Bucket.Item[0].ID;
info.Price = Shop.Bucket.Item[0].Price;

Shop.Purchase(payment, info);
```

- 핵심 코드(상점)가 외부 API 변화로 부터 보호됨. 외부 의존성 제거
- 새 결제수단은 어댑터 객체만 추가하면됨

---

## 예시
`Utils/SimpleInput.cs` : 어댑터(래퍼, Wraaper). 
New Input System의 장황한 호출을 쓰기 쉽게 변환해 놓은
객체. New Input System의 디테일한 설정을 몰라도
동작 하게끔 되어있음. (해당 어댑터 클래스가 게임의 핵심 코드를 보호하는 구조)

> `Adapter`는 `우리가 원하는 형태로 감싼다`는 점에서 
> `Wraaper`와 개념적으로 거의 유사함. 같은 말이라고 봐도 거의 상관 없을 정도
> 실무에서는 SDK, API등을 바로 게임로직에 넣지않고 한번 감싸서 넣음.