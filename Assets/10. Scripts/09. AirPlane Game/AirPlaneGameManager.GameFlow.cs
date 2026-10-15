using UnityEngine;

namespace Study3D.AirPlaneGame
{
    public partial class AirPlaneGameManager : MonoBehaviour
    {
        [Header("GameSettings")]
        [SerializeField] private int airPlaneMaxHp = 3;

        private int airPlaneHp;
        
        private void InitGameFlow()
        {
            airPlaneHp = airPlaneMaxHp;    
        }
        
        // 비행기에 충돌이 일어났을때 반응하는 로직
        public void OnAirPlaneCollision()
        {
            // 1. hp를 감소시킨다
            // 2.1 hp > 0 == true
            //      - 비행기를 리스폰한다
            // 2.1 hp > 0 == false
            //      - 게임 종료 처리를 한다.

            airPlaneHp -= 1;
            
            if (airPlaneHp > 0)
            {
                FlyLog respawnData = GetFlyLog();
                AirPlaneController.LocalAirPlane.Respawn(
                    respawnData.WorldPosition,
                    respawnData.WorldRotation);
            }
            else
            {
                // 게임 오버 처리
                AirPlaneController.LocalAirPlane.Dead();
            }
        }
    }
}