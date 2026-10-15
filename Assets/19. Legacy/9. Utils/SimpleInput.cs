using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Study.Utilities
{
    // PC 플랫폼에서 사용하는 키보드 마우스 인풋들 묶어놓은
    // 유틸클래스를 작성해 봅시다.
    // 목표는 쉽고 직관적으로 사용할 수 있도록 기능들을
    // 묶어 놓는 것입니다.

    // 아래처럼 작업하는 디자인 패턴을 Wrapper 패턴
    // 이라고 부릅니다. Wrapping 한다고 합니다

    // SimpleInput 클래스는 어디서든 호출할 수 있게
    // static(정적, 고정된, 랜드마크) 키워드를 사용하여
    // 선언 합니다

    public static class SimpleInput
    {
        #region 키보드 입력

        public static bool GetKeyDown(Key key)
        {
            return Keyboard.current[key].wasPressedThisFrame;
        }
        public static bool GetKeyUp(Key key)
        {
            return Keyboard.current[key].wasReleasedThisFrame;
        }
        public static bool GetKey(Key key)
        {
            return Keyboard.current[key].isPressed;
        }
        public static bool AnyKeyDown()
        {
            return Keyboard.current.anyKey.wasPressedThisFrame;
        }

        #endregion

        #region 마우스 입력

        /// <summary>
        /// 마우스 버튼 종류를 정의하는 열거형
        /// </summary>
        public enum MouseButton { Left = 0, Right = 1, Middle = 2}

        // 내부에서만 사용하는 함수 입니다.
        private static ButtonControl GetMouseControl(MouseButton button)
        {
            switch (button)
            {
                case MouseButton.Left:
                    return Mouse.current.leftButton;
                case MouseButton.Right:
                    return Mouse.current.rightButton;
                case MouseButton.Middle:
                    return Mouse.current.middleButton;
            }

            return null;
        }

        public static bool GetMouseButtonDown(MouseButton button)
        {
            return GetMouseControl(button).wasPressedThisFrame;
        }

        public static bool GetMouseButtonUp(MouseButton button)
        {
            return GetMouseControl(button).wasReleasedThisFrame;
        }

        public static bool GetMouseButton(MouseButton button)
        {
            return GetMouseControl(button).isPressed;
        }

        public static Vector2 GetMousePosition()
        {
            return Mouse.current.position.ReadValue();
        }

        /// <summary>
        /// 마우스가 한프레임에 이동한 위치 변화량을 반환합니다.
        /// </summary>
        /// <returns></returns>
        public static Vector2 GetMouseDelta()
        {
            return Mouse.current.delta.ReadValue();
        }

        public static float GetMouseScrollDeltaRaw()
        {
            return Mouse.current.scroll.ReadValue().y;
        }

        // 마우스 휠 스크롤을 -1, 0, 1 단위로 반환합니다.
        // 그... 스크롤 오돌도돌한 한 단위 그거
        public static float GetMouseScrollDelta()
        {
            float raw = GetMouseScrollDeltaRaw();
            return Mathf.Approximately(raw, 0f) ? 0f : Mathf.Sign(raw);
            // Mathf.Approximately
            // 0이랑 비교를 하면 근사치로 일정한 범위내에 raw가 있는지
            // 비교를 합니다.
            // 0.99999999 = 1 => 이런 비교를 하는거임
        }

        #endregion

        #region 축 입력 (WASD / 방향기)

        /// <summary>
        /// 수평 축 원시 입력값을 반환합니다 (-1, 0, 1)
        /// A/D 키 또는 좌/우 화살표 키 대응. 
        /// (레거시에서 Input.GetAxisRaw("Horizontal")과 동일합니다)
        /// </summary>
        /// <returns></returns>
        public static float GetAxisHorizontalRaw()
        {
            float axis = 0.0f;

            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) 
                axis += 1.0f;

            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) 
                axis -= 1.0f;

            return axis;
        }

        /// <summary>
        /// 수직 축 원시 입력값을 반환합니다 (-1, 0, 1)
        /// W/S 키 또는 위/아래 화살표 키 대응. 
        /// (레거시에서 Input.GetAxisRaw("Vertical")과 동일합니다)
        /// </summary>
        /// <returns></returns>
        public static float GetAxisVerticalRaw()
        {
            float axis = 0.0f;

            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                axis += 1.0f;

            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                axis -= 1.0f;

            return axis;
        }

        /// <summary>
        /// 수평 / 수직 Raw 입력을 합쳐 정규화된 Vector2로 반환합니다.
        /// 대각선 입력시 속도가 조금 빨라지는(루트2 나오는거) 현상을
        /// 보정하여 반환합니다.
        /// </summary>
        /// <returns></returns>
        public static Vector2 GetMoveAxisRaw()
        {
            Vector2 axis = new Vector2();
            axis.x = GetAxisHorizontalRaw();
            axis.y = GetAxisVerticalRaw();
            
            if (axis.sqrMagnitude > 1f) return axis.normalized;
            return axis;
        }

        #endregion
    }

}