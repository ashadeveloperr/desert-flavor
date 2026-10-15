// ─────────────────────────────────────────────
// MaterialInstanceDial — URP Lit의 속성 세 개를 키로 바꿔 보며, 인스턴스 머티리얼이 에셋을 건드리지 않는 것을 확인한다
// 학습 포인트
//  - URP Lit의 주요 속성은 _BaseMap(텍스처) · _BaseColor(그 위에 곱해지는 색) · _Metallic · _Smoothness입니다.
//  - _BaseColor는 텍스처를 덮는 색이 아니라 곱하는 색입니다. 흰색이면 텍스처가 그대로 보입니다.
//  - renderer.material을 처음 읽는 순간 Unity가 이 오브젝트 전용 복사본을 만듭니다. 그래서 값을 바꿔도 프로젝트의 .mat 파일은 그대로입니다.
// 조작
//  - 1 : 바꿀 속성을 색으로
//  - 2 : 바꿀 속성을 금속성으로
//  - 3 : 바꿀 속성을 매끄러움으로
//  - Q : 고른 속성 값 늘리기
//  - A : 고른 속성 값 줄이기
//  - R : 처음 값으로
// ─────────────────────────────────────────────
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study3D.ExternalAssets
{
    [RequireComponent(typeof(Renderer))]
    public class MaterialInstanceDial : MonoBehaviour
    {
        public enum Slot
        {
            BaseColor,
            Metallic,
            Smoothness
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        [Header("대상")]
        [SerializeField] private Slot slot = Slot.BaseColor;

        [Header("조작")]
        [SerializeField] private float changePerSecond = 0.6f;

        [Header("표시")]
        [SerializeField] private TMP_Text label;

        private Renderer target;
        private Material instance;
        private Color startColor;
        private float startMetallic;
        private float startSmoothness;
        private float hue;

        private void Awake()
        {
            target = GetComponent<Renderer>();

            // 이 한 줄이 이 오브젝트 전용 복사본을 만듭니다. 그래서 아래에서 값을 바꿔도 프로젝트의 .mat은 그대로입니다.
            instance = target.material;

            if (!instance.HasProperty(BaseColorId))
            {
                Debug.LogWarning("[Study3D] URP Lit 머티리얼이 아닙니다");
                enabled = false;
                return;
            }

            startColor = instance.GetColor(BaseColorId);
            startMetallic = instance.GetFloat(MetallicId);
            startSmoothness = instance.GetFloat(SmoothnessId);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.digit1Key.wasPressedThisFrame) slot = Slot.BaseColor;
            if (keyboard.digit2Key.wasPressedThisFrame) slot = Slot.Metallic;
            if (keyboard.digit3Key.wasPressedThisFrame) slot = Slot.Smoothness;

            if (keyboard.rKey.wasPressedThisFrame)
            {
                // R을 누른 프레임에는 아래 switch를 건너뜁니다. 순서가 바뀌면 되돌린 값이 같은 프레임에
                // 다시 덮여 버립니다. Q를 다시 누르면 색상 회전이 처음부터 시작되므로,
                // hue를 startColor 기준으로 다시 계산할 필요는 없습니다(항상 0으로 되돌립니다).
                hue = 0f;
                instance.SetColor(BaseColorId, startColor);
                instance.SetFloat(MetallicId, startMetallic);
                instance.SetFloat(SmoothnessId, startSmoothness);
            }
            else
            {
                float step = changePerSecond * Time.deltaTime;
                float delta = 0f;
                if (keyboard.qKey.isPressed) delta += step;
                if (keyboard.aKey.isPressed) delta -= step;

                // delta가 0이면 값을 다시 쓰지 않습니다. 매 프레임 재대입하면 아무 키도 안 눌러도
                // 값이 그대로 다시 들어가고, 특히 BaseColor는 hue=0에서 시작해 Play 첫 프레임부터
                // 색이 붉게 물듭니다.
                if (!Mathf.Approximately(delta, 0f))
                {
                    switch (slot)
                    {
                        case Slot.BaseColor:
                            // 색은 0~1 한 축으로 못 바꾸므로 색상(Hue)을 돌립니다.
                            hue = Mathf.Repeat(hue + delta, 1f);
                            instance.SetColor(BaseColorId, Color.HSVToRGB(hue, 0.5f, 1f));
                            break;

                        case Slot.Metallic:
                            instance.SetFloat(MetallicId, Mathf.Clamp01(instance.GetFloat(MetallicId) + delta));
                            break;

                        case Slot.Smoothness:
                            instance.SetFloat(SmoothnessId, Mathf.Clamp01(instance.GetFloat(SmoothnessId) + delta));
                            break;
                    }
                }
            }

            UpdateLabel();
        }

        // 표시 문자열 만들기를 Update에서 떼어 냈습니다. Update는 입력과 값 변경만 봅니다.
        private void UpdateLabel()
        {
            if (label == null) return;

            Color c = instance.GetColor(BaseColorId);
            label.text =
                $"고른 속성 : {slot}\n" +
                $"BaseColor : ({c.r:F2}, {c.g:F2}, {c.b:F2})\n" +
                $"Metallic : {instance.GetFloat(MetallicId):F2}\n" +
                $"Smoothness : {instance.GetFloat(SmoothnessId):F2}";
        }

        private void OnValidate()
        {
            changePerSecond = Mathf.Max(0.01f, changePerSecond);
        }
    }
}
