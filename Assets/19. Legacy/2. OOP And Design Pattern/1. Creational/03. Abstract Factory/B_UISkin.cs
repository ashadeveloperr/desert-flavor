using UnityEngine;

namespace Study_OOP_And_Design_Pattern.Creational
{
    // abstract Factory - 예시 B : UI 스킨(테마) 세트
    // 상황 : 다크/라이트 테마에 따라 버튼 + 패널을 "한 세트로" 만든다.

    // 이번 파트는 클래스명의 겹침 현상을 피하기 위해 인터페이스로 하겠습니다
    public interface IButton { string Style(); }
    public interface IPanel { string Style(); }

    public class DarkButton : IButton { public string Style() => "어두운 버튼"; }
    public class DarkPanel : IPanel { public string Style() => "어두운 패널"; }
    
    public class LightButton : IButton { public string Style() => "밝은 버튼";}
    public class LightPanel : IPanel { public string Style() => "밝은 패널"; }

    public abstract class UiThemeFactory
    {
        public abstract IButton CreateButton();
        public abstract IPanel CreatePanel();
    }
    
    public class DarkThemeFactory : UiThemeFactory
    {
        public override IButton CreateButton() => new DarkButton();
        public override IPanel CreatePanel() => new DarkPanel();
    }
    
    public class LightThemeFactory : UiThemeFactory
    {
        public override IButton CreateButton() => new LightButton();
        public override IPanel CreatePanel() => new LightPanel();
    }

    public class UiSkinDemo : MonoBehaviour
    {
        private IButton currentButton;
        private IPanel currentPanel;
        
        private void OnClickChangeTheme(int index)
        {
            UiThemeFactory factory;
            
            switch (index)
            {
                case 0:
                    factory = new DarkThemeFactory();
                    break;
                case 1:
                    factory = new LightThemeFactory();
                    break;
                default:
                    factory = new LightThemeFactory();
                    break;
            }
            
            BuildUi(factory);
        }

        private void BuildUi(UiThemeFactory factory)
        {
            // 실제로는 이미지나 색상, 폰트등을 변경해준다
            currentButton = factory.CreateButton();
            currentPanel = factory.CreatePanel();
        }
    }
}