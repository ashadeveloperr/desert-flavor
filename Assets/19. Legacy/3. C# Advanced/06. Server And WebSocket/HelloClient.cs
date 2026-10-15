using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;

namespace Study.CsharpAdv.FileIO._06._Server_And_WebSocket
{
    public class HelloClient : MonoBehaviour
    {
        public string targetUrl = "http:127.0.0.1:5000/";
        
        public string userName = "Jay";
        public string userMessage = "Hello Server";
        
        
        private void Update()
        {
            if (SimpleInput.GetKeyDown(Key.Digit1))
            {
                GetStringAsync(targetUrl).Forget();
            }

            if (SimpleInput.GetKeyDown(Key.Digit2))
            {
                PostStringAsync(targetUrl).Forget();
            }
        }

        private async UniTask<string> GetStringAsync(string url)
        {
            CancellationToken cts = this.GetCancellationTokenOnDestroy();
            
            // # UnityWebRequest?
            // : Unity에서 HTTP/HTTPS 등의 네트워크 통신을 처리하기 위해 제공되는
            //  모듈형 네트워킹 클래스(자료형) 입니다. 내부의 구조는 크게 두가지로 되어있습니다
            //  - UploadHandler     : Client -> Server로 요청
            //  - DownloadHandler   : Server -> Client로 응답
            //  메시지 자체는 따로 건드릴게 많이 없을 정도로 알아서 갑니다.
            
            UnityWebRequest request = UnityWebRequest.Get(url);
            await request.SendWebRequest().WithCancellation(cts);

            // 성공일때만 처리해준다
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"{targetUrl} 서버로 부터의 응답 : {request.downloadHandler.text}");
                return request.downloadHandler.text;
            }

            return string.Empty;
        }

        [Serializable]
        public struct UserMessage
        {
            public string name;
            public string message;
        }
        
        private async UniTaskVoid PostStringAsync(string url)
        {
            // 데이터 객체를 일단 만들어 준다
            UserMessage postMsg = new UserMessage
            {
                name = userName,
                message = userMessage,
            };
            
            // 데이터 객체를 Json으로 직렬화 해준다.
            string jsonBody = JsonUtility.ToJson(postMsg);
            var destroyToken = this.GetCancellationTokenOnDestroy();
            
            // 새로운 UnityWebRequest 만들어 준다.
            // 첫번째 매개변수 : url(주소)
            // 두번째 매개변수 : 사용할 HTTP 메서드. 이번에는 POST
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            
            // Json으로 직렬화 해놓은것을 byte[]로 바꿔준다.
            // UTF로 변환해줘야 함
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            // request의 uploadHandler에 bodyRaw를 이용한 새 객체를 만들어서 대입해준다
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            // request의 downloadHandler = 다운받을 버퍼 객체를 만들어 준다. 지금 상태에서는 비어있다
            request.downloadHandler = new DownloadHandlerBuffer();
            
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10; // 기본적인 타임아웃은 10초. 10초내에 응답이 없으면 실패로 처리
            
            await request.SendWebRequest().WithCancellation(destroyToken);

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[응답] : {request.downloadHandler.text}");
            }
        }
    }
}