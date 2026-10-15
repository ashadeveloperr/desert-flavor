# Decorator (데코레이터) 패턴
> 원본을 고치지 않고 `감싸는 껍데기`를 덧씌워, 
> 런타임에 기능을 겹겹이 추가하는 패턴

## Decorator ?
대상 객체를 같은 인터페이스의 장식자(decorator)로
감싸고, 그 위에 또 감싼다. 상속과 달리 런타임에
기능을 붙이거나 뗄 수 있고, 여러 개를 자유롭게 겹칠 수 있다

## 상황
농사 작물에 비료,물,햇빛 보너스를 조합한다.
상속이나 플래그로 처리하면?

```csharp
public class FertilizedCrop : Crop {} 
// 비료를 먹은 작물

public WateredCrop : Crop {}
// 물로 적셔진 작물

public class FertilizedWateredCrop : Crop {}
// 비료 + 물 작물

//=== 이게 싫다면 Crop 이라는 클래스에 플래그들을 넣으면?

class Crop
{
    bool fertilized;
    bool watered;
    bool sunny;
    
    int GetScore()
    {
        int reval = 0;
        if(fertilized) reval += 10;
        if(watered) reval += 5;
        if(sunny) reval += 20;
        // ...
        // ...
        
        return reval;
    }
}
```

## 적용
작물과 장식자가 같은 인터페이스를 구현한다.
장식자는 "안쪽 + 자기보너스"를 반환한다
```csharp
public interface IScorableCrop // 공통 계약
{
    int GetScore();
}

// 작물(원본 클래스)
public class BaseCrop : IScorableCrop
{
    public int baseScore = 10;
    public int GetScore() => baseScrore;
}

// 중첩가능한 상태(장식 클래스)
public abstract class CropDecorator : IScorableCrop
{
    protected IScorableCrop inner;
    protected CropDecorator(ICrop inner)
    {
        this.inner = inner;
    }
    // 인터페이스의 함수를 추상함수(순수가상함수)
    // 로 선언해준다
    public abstract int GetScore();
}

public class Fertilizer : CropDecorator
{
    public override int GetScore()
    {
        // 내부 객체의 GetScore에
        // 본인 보너스를 더해서 반환한다
        return inner.GetScore() + 5;
    }
}

public class Watering : CropDecorator
{
    public override int GetScore()
    {
         // 내부 객체의 GetScore에
        // 본인 보너스를 더해서 반환한다
        return inner.GetScore() + 10;
    }
}

// 객체를 만들고
IScorableCrop baseCrop = new BaseCrop();
// 장식 하나를 추가한다
IScorableCrop fertilizedCrop 
    = new Fertilizer(baseCrop);
int currentScore = fertilizedCrop.GetScore();
// 15나옴

IScorableCrop fertilizedWateredCrop
    = new Watring(fertilizedCrop);
int lastScore = fertilizedWateredCrop.GetScore();
// 25나옴
```
조합 폭발 없이 런타임에 중첩시키고, 해제 할 수 있다.
원본(BaseCrop) 불변 + 새로운 효과는 데코레이터만 추가