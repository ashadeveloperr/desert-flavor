// ─────────────────────────────────────────────
// BoneWaver — 본 여러 개를 똑같이 흔들어, 웨이트만 다른 리본들이 어떻게 달라지는지 비교하게 한다
// 학습 포인트
//  - 스킨드 메시는 Animator 가 없어도 본 Transform 을 돌리면 그대로 변형됩니다. 애니메이션 클립이 필요한 것이 아닙니다.
//  - 본을 똑같이 돌려도 정점의 웨이트가 다르면 결과가 다릅니다. 그것이 웨이트의 뜻입니다.
//  - 본의 시작 회전에 곱해야 합니다. 대입하면 모델이 처음 자세를 잃습니다.
// 조작
//  - Q : 흔드는 각도 늘리기
//  - A : 흔드는 각도 줄이기
//  - Space : 흔들기 멈추기 / 다시 흔들기
//  - R : 처음 자세로
// ─────────────────────────────────────────────
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study3D.SkinnedMeshes
{
    public class BoneWaver : MonoBehaviour
    {

        [Header("본")]
        [SerializeField] private Transform[] bones;

        [Header("회전")]
        [SerializeField] private Vector3 axis = Vector3.right;
        [SerializeField] private float maxAngle = 35f;
        [SerializeField] private float cyclesPerSecond = 0.5f;

        [Header("조작")]
        [SerializeField] private float anglePerKeyPress = 20f;

        [Header("표시")]
        [SerializeField] private TMP_Text statusText;

        private Quaternion[] startRotations;
        private bool waving = true;
        private float phase;
        // R 키로 되돌릴 기준값. 상수가 아니라 인스펙터에 적힌 시작값입니다 -
        // 강사가 인스펙터에서 맞춰 둔 각도를 R 이 덮어쓰지 않게 하려는 것입니다.
        private float startMaxAngle;

        private void OnValidate()
        {
            maxAngle = Mathf.Clamp(maxAngle, 0f, 80f);
            cyclesPerSecond = Mathf.Clamp(cyclesPerSecond, 0.05f, 5f);
            anglePerKeyPress = Mathf.Max(0.1f, anglePerKeyPress);
            if (axis.sqrMagnitude < 0.0001f) axis = Vector3.right;
        }

        private void Awake()
        {
            startMaxAngle = maxAngle;

            int count = 0;
            if (bones != null) count = bones.Length;
            startRotations = new Quaternion[count];
            for (int i = 0; i < count; i++)
            {
                startRotations[i] = Quaternion.identity;
                if (bones[i] != null) startRotations[i] = bones[i].localRotation;
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.qKey.isPressed) maxAngle += anglePerKeyPress * Time.deltaTime;
            if (keyboard.aKey.isPressed) maxAngle -= anglePerKeyPress * Time.deltaTime;
            maxAngle = Mathf.Clamp(maxAngle, 0f, 80f);

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                if (waving) waving = false;
                else waving = true;
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                phase = 0f;
                maxAngle = startMaxAngle;
                for (int i = 0; i < startRotations.Length; i++)
                {
                    if (bones[i] != null) bones[i].localRotation = startRotations[i];
                }
            }

            if (waving) phase += Time.deltaTime * cyclesPerSecond * Mathf.PI * 2f;

            float angle = Mathf.Sin(phase) * maxAngle;
            for (int i = 0; i < startRotations.Length; i++)
            {
                if (bones[i] == null) continue;
                // 대입하면 본이 원래 가지고 있던 회전이 사라집니다. 시작 회전에 곱해야 처음 자세가 유지됩니다.
                bones[i].localRotation = startRotations[i] * Quaternion.AngleAxis(angle, axis.normalized);
            }

            if (statusText != null)
            {
                string state = "끔";
                if (waving) state = "켬";
                statusText.text = "최대 각 : " + Mathf.RoundToInt(maxAngle) + "도\n" +
                                  "흔들기 : " + state + "\n" +
                                  "Q A 각도 · Space 멈춤 · R 처음으로";
            }
        }
    }
}
