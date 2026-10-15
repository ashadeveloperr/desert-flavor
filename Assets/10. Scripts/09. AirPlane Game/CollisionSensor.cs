using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace Study3D.AirPlaneGame
{
    // 단순한 충돌을 감지하는 컴포넌트입니다.
    // UnityEvent로 등록된 public 함수들을 호출 할 수 있습니다.
    public class CollisionSensor : MonoBehaviour
    {
        [Header("Properties")]
        [SerializeField] private LayerMask layerMask;
        [SerializeField] private string tag;
        
        [Header("Events")]
        public UnityEvent onEnter;
        public UnityEvent onExit;

        private bool CheckProperties(GameObject other)
        {
            // 마스크 검사
            if (layerMask.Contains(other.gameObject) == false) return false;
            
            // tag 없다면 check 반환
            if (string.IsNullOrEmpty(tag)) return true;
            return other.CompareTag(tag);
        }
        
        private void OnCollisionEnter(Collision collision)
        {
            if (CheckProperties(collision.gameObject)) onEnter.Invoke(); 
        }

        private void OnCollisionExit(Collision collision)
        {
            if (CheckProperties(collision.gameObject)) onExit.Invoke();
        }
    }
}