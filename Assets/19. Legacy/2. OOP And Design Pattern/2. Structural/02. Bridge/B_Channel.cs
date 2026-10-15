using UnityEngine;

namespace Study_OOP_And_Design_Pattern.Structural.Bridge
{
    // Bridge - 예시 B : 알림(기능) x 전송 채널(구현)
    // 상황 : 알림 종류(긴급/일반)와 전송 채널(이메일/SMS/푸시)등을 조합
    // 포인트 : 무기x속성과 같은 구조. 다른 활용

    public interface IChannel { void Send(string text); }
    
    public class EmailChannel : IChannel
    {
        public void Send(string text) { Debug.Log($"[이메일] {text}"); }
    }
    public class SMSChannel : IChannel
    {
        public void Send(string text) { Debug.Log($"[SMS] {text}"); }
    }
    public class PushChannel : IChannel
    {
        public void Send(string text) { Debug.Log($"[푸시] {text}"); }
    }

    public abstract class Notification
    {
        protected IChannel channel; // 다리 역할
        protected Notification(IChannel c) { this.channel = c; } // 다리 이어줌
        public abstract void Notify(string message);
    }

    public class UrgentNotification : Notification
    {
        public UrgentNotification(IChannel c) : base(c) { }

        public override void Notify(string message)
        {
            channel.Send($"[긴급] : {message}");
        }
    }
    
    public class NormalNotification : Notification
    {
        public NormalNotification(IChannel c) : base(c) { }

        public override void Notify(string message)
        {
            channel.Send($"[일반] : {message}");
        }
    }

    public class NotificationBridgeDemo : MonoBehaviour
    {
        private void Start()
        {
            // UrgentNotification urgent = new UrgentNotification(new PushChannel());
            // urgent.Notify("서버 점검이 시작됩니다. SMS를 확인하세요");
            //
            // NormalNotification normal = new NormalNotification(new SMSChannel());
            // normal.Notify("금일 서버점검은 서버개발자의 잘못으로 " +
            //               "클라이언트 개발자와는 무관합니다. 멍청한 기획자가 테이블 실수를 " +
            //               "저질러 버렸습니다. 클라 개발자는 유저를 사랑합니다.");

            NotifyMaintenance("서버 점검이 시작됩니다. SMS를 확인하세요",
                "금일 서버점검은 서버개발자의 잘못으로 클라이언트 개발자와는 무관합니다. " +
                "멍청한 기획자가 테이블 실수를 저질러 버렸습니다. 클라 개발자는 유저를 사랑합니다.");
        }

        private void NotifyMaintenance(string pushMessage, string smsContent)
        {
            UrgentNotification urgent = new UrgentNotification(new PushChannel());
            urgent.Notify(pushMessage);
            
            NormalNotification normal = new NormalNotification(new SMSChannel());
            normal.Notify(smsContent);
        }
    }
}