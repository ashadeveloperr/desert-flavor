using UnityEngine;

namespace Study_OOP_And_Design_Pattern.Structural
{
    public class KakaoPayApi
    {
        public void Request(string item, string userId)
        {
            Debug.Log($"[KakaoAPI] {userId}가 {item} 결제");  
        } 
    }
    public class LinePayApi
    {
        public string Auth(string userId) => "LINE_TOKEN";
        public void Execute(string token, string item, int amount) => Debug.Log($"[LineAPI] {item} {amount}원 결제({token})");
    }

    public interface IPayment { void Pay(string userId, string item, int price); }

    public class KakaoAdapter : IPayment
    {
        private readonly KakaoPayApi api = new KakaoPayApi();
        public void Pay(string userId, string item, int price) => api.Request(item, userId); // 카카오 방식으로 변환
    }
    public class LineAdapter : IPayment
    {
        private readonly LinePayApi api = new LinePayApi();
        public void Pay(string userId, string item, int price)
        {
            string token = api.Auth(userId);   // 라인은 인증 먼저
            api.Execute(token, item, price);
        }
    }

    public class PaymentDemo : MonoBehaviour
    {
        private void Start()
        {
            Purchase(new KakaoAdapter());   // 상점은 IPayment만 안다
            Purchase(new LineAdapter());
        }

        private void Purchase(IPayment pay)
        {
            pay.Pay("Jay", "검001", 1000);  
        } 
    }
}