using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HelloServer
{
    // 내가 조종하는 캐릭터. 씬에 하나 놓고 RoomClient 에 연결한다.
    public class LocalPlayer : MonoBehaviour
    {
        [Header("모양")]
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private TMP_Text label;

        [Header("움직임")]
        [SerializeField] private float speed = 5f;

        public bool CanMove { get; set; } = true;

        public void Setup(User user, Color color)
        {
            label.text = $"{user.NickName} ({user.Id})";
            body.color = color;
        }

        private void Update()
        {
            if (CanMove == false) return;

            Keyboard keyboard = Keyboard.current;

            float x = (keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.leftArrowKey.isPressed ? 1 : 0);
            float y = (keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.downArrowKey.isPressed ? 1 : 0);

            transform.Translate(new Vector3(x, y) * (speed * Time.deltaTime));
        }
    }
}
