using UnityEngine;
using UnityEngine.InputSystem;

namespace HelloServer
{
    // 방 코드를 받아 들어가고, 들어간 뒤에는 채팅을 그린다. RoomClient 와 같은 오브젝트에 붙인다.
    [RequireComponent(typeof(RoomClient))]
    public class RoomLobby : MonoBehaviour
    {
        [Header("처음 값")]
        [SerializeField] private string nickName = "이름없음";
        [SerializeField] private string roomCode = "ABCD";

        private RoomClient client;

        private string typing = "";

        private bool chatting;

        private void Awake()
        {
            client = GetComponent<RoomClient>();
        }

        private void OnGUI()
        {
            if (client.Me == null) DrawLobby();
            else DrawRoom();
        }

        private void DrawLobby()
        {
            GUI.Box(new Rect(20, 20, 320, 150), "방에 들어가기");

            GUI.Label(new Rect(35, 50, 70, 22), "이름");
            nickName = GUI.TextField(new Rect(105, 50, 220, 22), nickName, 12);

            GUI.Label(new Rect(35, 80, 70, 22), "방 코드");
            roomCode = GUI.TextField(new Rect(105, 80, 220, 22), roomCode, 8);

            // 붙는 중에는 다시 누르지 못하게 막는다.
            GUI.enabled = client.IsBusy == false;

            if (GUI.Button(new Rect(35, 112, 290, 30), client.IsBusy ? "들어가는 중…" : "들어가기"))
            {
                client.Join(roomCode, nickName);
            }

            GUI.enabled = true;

            GUI.Label(new Rect(20, 178, 700, 22),
                "같은 방 코드를 친 사람끼리만 서로 보인다. 대소문자는 서버가 맞춰 준다.");
        }

        // 들어갔다. 방 코드와 사람 수, 그리고 채팅을 그린다.
        private void DrawRoom()
        {
            GUI.Label(new Rect(20, 20, 700, 22),
                $"방 {client.RoomCode}    나 : {client.Me.NickName}({client.Me.Id})    남 : {client.RemoteCount}명");

            DrawChatLog();
            DrawChatInput();
        }

        private void DrawChatLog()
        {
            GUI.Box(new Rect(20, 46, 380, 220), "");

            int line = 0;

            foreach (string said in client.ChatLog)
            {
                GUI.Label(new Rect(30, 52 + line * 18, 360, 18), said);
                line++;
            }
        }

        private void DrawChatInput()
        {
            // 이 칸에 커서가 있는지 이름으로 알아본다.
            GUI.SetNextControlName("chat");

            typing = GUI.TextField(new Rect(20, 272, 300, 24), typing, 100);

            chatting = GUI.GetNameOfFocusedControl() == "chat";

            bool enter = Event.current.type == EventType.KeyDown
                         && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);

            if (GUI.Button(new Rect(326, 272, 74, 24), "보내기") || (chatting && enter))
            {
                client.Say(typing);
                typing = "";
            }

            GUI.Label(new Rect(20, 300, 700, 22),
                "화살표로 움직인다. 채팅 칸을 누르면 움직이지 않는다.");
        }

        // 채팅을 치는 동안에는 캐릭터를 멈춘다.
        private void Update()
        {
            client.Local.CanMove = chatting == false;

            // Esc 로 채팅 칸에서 빠져나온다.
            if (Keyboard.current.escapeKey.wasPressedThisFrame) GUI.FocusControl(null);
        }
    }
}