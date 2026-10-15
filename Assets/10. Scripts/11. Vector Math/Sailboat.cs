// ─────────────────────────────────────────────
// Sailboat — 바람과 돛 각도로 추진력을 구해 배를 앞으로 움직이고, 돛 법선·돛 힘·추진력을 화살표와 HUD로 보여 준다
// 학습 포인트
//  - 돛 법선은 sailPivot.forward입니다. 돛을 돌리면 법선이 같이 돕니다.
//  - 배는 앞 방향으로만 움직입니다. 옆바람을 받아도 옆으로 밀리지 않는 것은 용골 덕분이라고 단순화했습니다.
//  - 맞바람 구역에서는 돛이 펄럭여(돛 천만 흔들림) 추진력이 0이 됩니다.
// 조작
//  - A : 배를 왼쪽으로 돌리기
//  - D : 배를 오른쪽으로 돌리기
//  - Q : 돛을 왼쪽으로 돌리기
//  - E : 돛을 오른쪽으로 돌리기
//  - R : 처음 자리로
// ─────────────────────────────────────────────
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study3D.VectorMath
{
    public class Sailboat : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private SailboatLab lab;
        [SerializeField] private WindSource wind;

        [Header("배")]
        [SerializeField] private Transform sailPivot;
        [SerializeField] private Transform sailCloth;
        [SerializeField] private float headingDegrees;
        [SerializeField] [Range(-90f, 90f)] private float sailAngle = 30f;

        [Header("조작")]
        [SerializeField] private float turnDegreesPerSecond = 40f;
        [SerializeField] private float sailDegreesPerSecond = 60f;

        [Header("움직임")]
        [SerializeField] private float driveGain = 0.4f;
        [SerializeField] private float drag = 0.8f;
        [SerializeField] [Range(10f, 80f)] private float noGoHalfAngle = 45f;
        [SerializeField] private float seaHalfSize = 90f;

        [Header("화살표")]
        [SerializeField] private VectorArrow sailNormalArrow;
        [SerializeField] private VectorArrow sailForceArrow;
        [SerializeField] private VectorArrow driveArrow;
        [SerializeField] private float arrowScale = 0.25f;

        [Header("표시")]
        [SerializeField] private TMP_Text readoutText;

        private float speed;
        private Vector3 startPosition;
        private float startHeadingDegrees;
        private float startSailAngle;

        private void Awake()
        {
            startPosition = transform.position;
            startHeadingDegrees = headingDegrees;
            startSailAngle = sailAngle;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            float turn = turnDegreesPerSecond * Time.deltaTime;
            if (keyboard.aKey.isPressed) headingDegrees -= turn;
            if (keyboard.dKey.isPressed) headingDegrees += turn;

            float sailTurn = sailDegreesPerSecond * Time.deltaTime;
            if (keyboard.qKey.isPressed) sailAngle -= sailTurn;
            if (keyboard.eKey.isPressed) sailAngle += sailTurn;
            sailAngle = Mathf.Clamp(sailAngle, -90f, 90f);

            if (keyboard.rKey.wasPressedThisFrame)
            {
                transform.position = startPosition;
                headingDegrees = startHeadingDegrees;
                sailAngle = startSailAngle;
                speed = 0f;
            }

            if (lab == null || wind == null || sailPivot == null) return;

            transform.rotation = Quaternion.AngleAxis(headingDegrees, Vector3.up);
            sailPivot.localRotation = Quaternion.AngleAxis(sailAngle, Vector3.up);

            Vector3 forward = transform.forward;
            Vector3 sailNormal = sailPivot.forward;
            Vector3 w = wind.Wind;

            Rigidbody body = null;
            body.angularVelocity = Vector3.one;

            Debug.Log($"angularVelocity : " +
                      $"x의 각도 : {body.angularVelocity.x * Mathf.Rad2Deg}"
                      + $"y의 각도 : {body.angularVelocity.y * Mathf.Rad2Deg}");
                      

            bool noGo = lab.IsInNoGoZone(forward, w, noGoHalfAngle);

            float windOnSail = Safe(lab.WindOnSail(w, sailNormal));
            Vector3 sailForce = sailNormal * windOnSail;

            float drive = noGo ? 0f : Safe(lab.DriveAlongKeel(sailForce, forward));

            speed += (driveGain * drive - drag * speed) * Time.deltaTime;
            speed = Mathf.Max(0f, speed);

            Vector3 p = transform.position + forward * speed * Time.deltaTime;
            p.x = Mathf.Clamp(p.x, -seaHalfSize, seaHalfSize);
            p.z = Mathf.Clamp(p.z, -seaHalfSize, seaHalfSize);
            transform.position = p;

            if (sailCloth != null)
            {
                sailCloth.localRotation = noGo
                    ? Quaternion.AngleAxis(Mathf.Sin(Time.time * 18f) * 12f, Vector3.up)
                    : Quaternion.identity;
            }

            Vector3 mast = sailPivot.position;
            if (sailNormalArrow != null) sailNormalArrow.Show(mast, sailNormal * 1.5f);
            if (sailForceArrow != null) sailForceArrow.Show(mast + Vector3.up * 0.8f, sailForce * arrowScale);
            if (driveArrow != null) driveArrow.Show(transform.position + Vector3.up * 0.9f, forward * drive * arrowScale);

            WriteReadout(windOnSail, drive, noGo);
        }

        private void WriteReadout(float windOnSail, float drive, bool noGo)
        {
            if (readoutText == null || wind == null) return;

            string line = $"바람 : {wind.Wind.magnitude:F1} m/s, {wind.TowardName} 붑니다\n" +
                $"배 방향 : {Mathf.Repeat(headingDegrees, 360f):F0}도 · 돛 각도 : {sailAngle:F0}도\n" +
                $"돛이 받는 바람 : {windOnSail:F2}\n" +
                $"앞으로 미는 몫 : {drive:F2}\n" +
                $"속력 : {speed:F2} m/s\n" +
                (noGo ? "맞바람 구역 : 들어감 (돛이 펄럭여 힘을 못 받음)" : "맞바람 구역 : 아님");

            readoutText.text = line;
        }

        private static float Safe(float v)
        {
            return float.IsNaN(v) || float.IsInfinity(v) ? 0f : v;
        }

        private void OnValidate()
        {
            sailAngle = Mathf.Clamp(sailAngle, -90f, 90f);

            if (sailPivot != null)
            {
                transform.rotation = Quaternion.AngleAxis(headingDegrees, Vector3.up);
                sailPivot.localRotation = Quaternion.AngleAxis(sailAngle, Vector3.up);
            }
        }
    }
}
