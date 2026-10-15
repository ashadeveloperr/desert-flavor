// ─────────────────────────────────────────────
// WindSource — 바람의 방향과 세기를 벡터 하나로 들고, 배 위에 바람 화살표를 그린다
// 학습 포인트
//  - 바람은 "어느 쪽으로, 얼마나 세게"를 함께 가진 벡터입니다. 방향(도)과 세기(m/s)를 곱해 만듭니다(01 §3.4).
//  - 여기서 바람 벡터는 불어 가는 방향입니다. 일기예보의 "서풍"은 불어오는 쪽 이름이라 반대입니다.
// 조작
//  - Left : 바람 방향을 반시계로 돌리기
//  - Right : 바람 방향을 시계로 돌리기
// ─────────────────────────────────────────────
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study3D.VectorMath
{
    public class WindSource : MonoBehaviour
    {
        [Header("바람")]
        [SerializeField] [Range(0f, 360f)] private float windDegrees = 90f;
        [SerializeField] [Range(0f, 15f)] private float windSpeed = 8f;

        [Header("조작")]
        [SerializeField] private float turnDegreesPerSecond = 45f;

        [Header("화살표")]
        [SerializeField] private Transform boat;
        [SerializeField] private VectorArrow windArrow;
        [SerializeField] private float arrowScale = 0.25f;
        [SerializeField] private float arrowHeight = 4.2f;

        private static readonly string[] TowardNames =
        {
            "북쪽으로", "북동쪽으로", "동쪽으로", "남동쪽으로", "남쪽으로", "남서쪽으로", "서쪽으로", "북서쪽으로",
        };

        public Vector3 Wind => Quaternion.AngleAxis(windDegrees, Vector3.up) * Vector3.forward * windSpeed;
        public string TowardName => TowardNames[Mathf.RoundToInt(windDegrees / 45f) % 8];

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            float turn = turnDegreesPerSecond * Time.deltaTime;
            if (keyboard.leftArrowKey.isPressed) windDegrees -= turn;
            if (keyboard.rightArrowKey.isPressed) windDegrees += turn;
            windDegrees = Mathf.Repeat(windDegrees, 360f);
        }

        private void LateUpdate()
        {
            if (boat == null || windArrow == null) return;

            Vector3 w = Wind * arrowScale;
            Vector3 tip = boat.position + Vector3.up * arrowHeight;
            windArrow.Show(tip - w, w);
        }

        private void OnValidate()
        {
            windSpeed = Mathf.Max(0f, windSpeed);
            arrowScale = Mathf.Max(0f, arrowScale);
        }
    }
}
