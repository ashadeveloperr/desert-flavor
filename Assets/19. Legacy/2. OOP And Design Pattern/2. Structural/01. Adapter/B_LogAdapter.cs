using UnityEngine;

namespace Study_OOP_And_Design_Pattern.Structural
{
    // Adapter - 예시 B : 로그 라이브러리 어댑터
    // 상황 : Unity 로그와 외부 파일 로그 라이브러리의 호출법이 다른데, 하나로 쓰고 싶다.
    //      유니티 로그를 남기면서 동시에 외부 파일에도 똑같은 내용을 남겨보자

    public class UnityLogger
    {
        public void PrintLog(string message) { Debug.Log($"[Unity] {message}"); }
    }
    public class ExternalFileLog 
    {
        // 실제로는 파일에 로그 한줄을 쓰는것으로 해야하지만, 대체하겠습니다.
        public void WriteLog(int criticalLevel, string message) 
        { Debug.Log($"[File] L : {criticalLevel}, MSG : {message}"); }
    }

    public interface ILogAdapter
    {
        // 매개변수의 갯수가 더 많은 쪽으로 맞춰준다.
        void WriteLog(int criticalLevel, string message);
    }
    
    public class UnityLogAdapter : ILogAdapter
    {
        private UnityLogger Logger { get; set; } = new(); 

        public void WriteLog(int criticalLevel, string message)
        {
            Logger.PrintLog(message);
        }
    }

    public class FileLogAdapter : ILogAdapter
    {
        private ExternalFileLog Logger { get; set; } = new();
        
        public void WriteLog(int criticalLevel, string message)
        {
            Logger.WriteLog(criticalLevel, message);
        }
    }

    public class User
    {
        public static string ID { get; set; }
    }
    
    public class LogDemo : MonoBehaviour
    {
        private ILogAdapter[] LogAdapters;

        private void Awake()
        {
            LogAdapters = new ILogAdapter[]
            {
                new UnityLogAdapter(),
                new FileLogAdapter(),
                new WebLogAdapter(User.ID)
            };
        }
        
        private void Start()
        {
            Log(0, "게임 시작!");
        }

        private void Log(int criticalLevel, string message)
        {
            foreach (var adapter in LogAdapters)
                adapter.WriteLog(criticalLevel, message);
        }
    }
    
    //------------------------ 추가한다면? --------------
    // 크리티컬 3이상인 로그는 웹서버로 발송해서 사내에 보관하는 기능을
    // 추가해달라고 요청이 들어왔다고 가정해봅시다.
    
    // 서버 프로그래머가 아래처럼 API를 호출하면된다고 전달해줌
    public class WebLogAPI
    {
        public void SendLog(string userID, string message)
        {
            // 원래는 Request코드여야 하지만, Debug.Log로 대체
            Debug.Log($"[File] userID : {userID}, MSG : {message}");
        }
    }
    
    public class WebLogAdapter : ILogAdapter
    {
        public string UserID { get; private set; }
        private WebLogAPI Logger { get; set; } = new();
        
        public WebLogAdapter(string userID)
        {
            UserID = userID;
        }
        
        public void WriteLog(int criticalLevel, string message)
        {
            if (criticalLevel < 3) return;
            Logger.SendLog(UserID, message);
        }
    }
}