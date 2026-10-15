namespace HelloServer
{
    // 서버와 유니티가 주고받는 글자의 모양.
    // 이 파일은 양쪽에 똑같이 있음
    //
    // 다른 것은 맨 윗줄 namespace 하나뿐. (유니티가 C# 버전이 낮아서 생긴일임)

    public class User
    {
        public string Id { get; set; }
        public string NickName { get; set; }
    }

    // 위치 한 줄. 뿌릴 때 이것만 모아서 보낸다.
    public class PlayerState
    {
        public string Id { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
    }

    public class TypeOnly
    {
        public string Type { get; set; }
    }

    #region 클라이언트 -> 서버

    public class HelloMessage
    {
        public string Type { get; set; } = "hello";
        public string NickName { get; set; }
    }

    public class MoveMessage
    {
        public string Type { get; set; } = "move";
        public string Id { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
    }

    #endregion

    #region 서버 -> 클라이언트

    public class WelcomeMessage
    {
        public string Type { get; set; } = "welcome";

        public string RoomCode { get; set; }

        public User User { get; set; }
        public User[] Users { get; set; }
    }

    public class JoinMessage
    {
        public string Type { get; set; } = "join";
        public User User { get; set; }
    }

    // 누가 나갔다.
    public class LeaveMessage
    {
        public string Type { get; set; } = "leave";
        public string Id { get; set; }
    }

    public class ChatMessage
    {
        public string Type { get; set; } = "chat";
        public string Id { get; set; }
        public string NickName { get; set; }
        public string Text { get; set; }
    }

    // 지금 모두의 위치. 초당 몇 번씩 온다. (횟수는 서버의 appsettings.json)
    public class StateMessage
    {
        public string Type { get; set; } = "state";
        public PlayerState[] States { get; set; }
    }

    #endregion
}
