using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Study3D.AirPlaneGame
{
    public partial class AirPlaneGameManager : MonoBehaviour
    {
        // 비행기의 주행기록을 남겨서 특정한 시점으로 복원하는 기능을 만들어 봅시다.
        private struct FlyLog
        {
            public Vector3 WorldPosition;
            public Quaternion WorldRotation;
        }
        
        // List가 더 좋지 않을까?
        // - 고스트 모드도 잘 맹글 수 있겠죠?
        private Queue<FlyLog> flyLogQueue = new Queue<FlyLog>();
        
        private void InitFlyLog()
        {
            LoopRecord().Forget();
        }

        private async UniTaskVoid LoopRecord()
        {
            while (gameObject.activeInHierarchy)
            {
                await UniTask.WaitForSeconds(0.1f);          // 0.1초 기다렸다가
                
                // 기록해준다
                FlyLog flyLog = new()
                {
                    WorldPosition = AirPlaneController.LocalAirPlane.transform.position,
                    WorldRotation = AirPlaneController.LocalAirPlane.transform.rotation
                };
                
                flyLogQueue.Enqueue(flyLog);
                
                if (flyLogQueue.Count > 30)
                {
                    var log = flyLogQueue.Dequeue(); // 30개를 초과하면 빼준다
                    Debug.Log(log.WorldPosition);
                }
            }
        }

        private FlyLog GetFlyLog()
        {
            return flyLogQueue.Dequeue();
        }
    }
}