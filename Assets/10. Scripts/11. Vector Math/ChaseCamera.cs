// ─────────────────────────────────────────────
// ChaseCamera — 대상 위치에 고정 오프셋을 더한 곳으로 부드럽게 따라가며 대상을 바라본다
// 학습 포인트
//  - 오프셋을 대상의 회전과 무관하게 월드 기준으로 더합니다. 배가 돌아도 화면의 동서남북이 그대로라 바람 방향을 읽기 쉽습니다.
//  - 따라가기는 1 - Exp(-k * dt) 비율로 섞습니다(프레임에 무관한 감쇠 — 09 함정 7).
// 조작
//  - 없음
// ─────────────────────────────────────────────
using UnityEngine;

namespace Study3D.VectorMath
{
    public class ChaseCamera : MonoBehaviour
    {
        [Header("대상")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 9f, -12f);

        [Header("따라가기")]
        [SerializeField] private float sharpness = 5f;
        [SerializeField] private float lookHeight = 1.5f;

        private void LateUpdate()
        {
            if (target == null) return;

            transform.position = Vector3.Lerp(transform.position, target.position + offset, 1f - Mathf.Exp(-sharpness * Time.deltaTime));
            transform.LookAt(target.position + Vector3.up * lookHeight);
        }
    }
}
