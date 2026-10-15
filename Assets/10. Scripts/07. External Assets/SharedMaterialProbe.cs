// ─────────────────────────────────────────────
// SharedMaterialProbe — material과 sharedMaterial의 차이를 두 오브젝트로 직접 보여 준다
// 학습 포인트
//  - renderer.material은 이 오브젝트 전용 복사본을 만듭니다. 같은 머티리얼을 쓰는 다른 오브젝트는 그대로입니다.
//  - renderer.sharedMaterial은 프로젝트의 머티리얼 에셋 자체입니다. 값을 바꾸면 그 머티리얼을 쓰는 오브젝트가 전부 바뀌고, Play를 멈춘 뒤에도 파일에 남습니다.
//  - 그래서 런타임에 색을 바꿀 때는 거의 항상 material을 씁니다.
// 조작
//  - Z : material로 왼쪽 오브젝트만 바꾸기
//  - X : sharedMaterial로 둘 다 바꾸기
//  - R : 처음 색으로
// ─────────────────────────────────────────────
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study3D.ExternalAssets
{
    public class SharedMaterialProbe : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("대상")]
        [SerializeField] private Renderer left;
        [SerializeField] private Renderer right;

        [Header("값")]
        [SerializeField] private Color tint = new Color(0.95f, 0.35f, 0.25f);

        [Header("표시")]
        [SerializeField] private TMP_Text label;

        private Material assetMaterial;
        private Color assetStartColor;
        private bool assetChanged;

        private void Awake()
        {
            if (left == null || right == null) return;

            // left.material에 한 번이라도 접근하면 Unity가 렌더러의 머티리얼을 복사본으로 바꿔치므로,
            // 그 뒤에는 left.sharedMaterial도 에셋이 아니라 그 복사본을 돌려줍니다. 그래서 에셋 참조를
            // Z를 누르기 전인 여기서 미리 붙잡아 둡니다.
            assetMaterial = left.sharedMaterial;
            if (assetMaterial != null)
            {
                assetStartColor = assetMaterial.GetColor(BaseColorId);
            }
        }

        private void Update()
        {
            if (left == null || right == null || assetMaterial == null) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.zKey.wasPressedThisFrame)
            {
                // shared가 아니라 material로 뭔가 값을 수정하면
                // 자동으로 인스턴싱 됨
                
                left.material.SetColor(BaseColorId, tint);
            }

            if (keyboard.xKey.wasPressedThisFrame)
            {
                // 일부러 에셋을 건드리는 줄입니다. 실제 코드에서는 이렇게 쓰지 않습니다.
                // 이 씬의 목적이 material과 sharedMaterial의 차이를 보여 주는 것이라 남겨 둡니다.
                assetMaterial.SetColor(BaseColorId, tint);
                assetChanged = true;
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                left.material.SetColor(BaseColorId, assetStartColor);
                RestoreAsset();
            }

            if (label != null)
            {
                Color assetColor = assetMaterial.GetColor(BaseColorId);
                label.text =
                    "Z : material (왼쪽만)\n" +
                    "X : sharedMaterial (둘 다)\n" +
                    $"에셋 색 : ({assetColor.r:F2}, {assetColor.g:F2}, {assetColor.b:F2})\n" +
                    $"에셋이 바뀐 상태 : {(assetChanged ? "예" : "아니오")}";
            }
        }

        private void RestoreAsset()
        {
            if (assetChanged && assetMaterial != null)
            {
                assetMaterial.SetColor(BaseColorId, assetStartColor);
                assetChanged = false;
            }
        }

        private void OnDisable()
        {
            // Play를 멈출 때 머티리얼 파일을 원래 색으로 되돌립니다.
            // 되돌리지 않으면 프로젝트 파일이 바뀐 채 남습니다 — 그것이 sharedMaterial의 위험이고, 여기서 되돌리는 것 자체가 학습 포인트입니다.
            RestoreAsset();
        }
    }
}
