// ─────────────────────────────────────────────
// ShaderNameLabel — 대상 렌더러가 쓰는 머티리얼과 셰이더의 이름을 라벨에 그대로 찍는다
// 학습 포인트
//  - 머티리얼은 "어떤 셰이더를 어떤 값으로 쓸지"를 담은 에셋입니다. 셰이더 이름을 보면 그 머티리얼이 어느 렌더 파이프라인용인지 알 수 있습니다.
//  - URP 프로젝트에서 Standard(Built-in용) 셰이더를 쓰는 머티리얼은 화면에 분홍색으로 나옵니다. URP가 그 셰이더를 해석하지 못하기 때문입니다.
//  - 상용 에셋을 받으면 가장 먼저 이 이름을 확인합니다.
// 조작
//  - 없음
// ─────────────────────────────────────────────
using TMPro;
using UnityEngine;

namespace Study3D.ExternalAssets
{
    public class ShaderNameLabel : MonoBehaviour
    {
        [Header("대상")]
        [SerializeField] private Renderer target;

        [Header("표시")]
        [SerializeField] private TMP_Text label;
        [SerializeField] private string title = "";
        [SerializeField] private bool refreshEverySecond = false;

        private float elapsed;

        private void Start()
        {
            Refresh();
        }

        private void Update()
        {
            if (!refreshEverySecond) return;

            elapsed += Time.deltaTime;
            if (elapsed < 1f) return;

            elapsed = 0f;
            Refresh();
        }

        private void Refresh()
        {
            if (label == null) return;
            if (target == null)
            {
                label.text = "렌더러가 연결되지 않았습니다";
                return;
            }

            // 읽기만 하므로 sharedMaterial이 맞습니다. material을 읽으면 이 오브젝트 전용 복사본이 쓸데없이 생깁니다.
            Material m = target.sharedMaterial;
            if (m == null)
            {
                label.text = "머티리얼이 비어 있습니다";
                return;
            }

            string text = "";
            if (title.Length > 0)
            {
                text += title + "\n";
            }

            // 3D 라벨은 세로 가운데 정렬이라 줄이 많으면 위아래 라벨과 겹칩니다. 그래서 3줄로 맞췄습니다.
            text += $"머티리얼 : {m.name}\n";
            text += $"셰이더 : {m.shader.name} · 슬롯 {target.sharedMaterials.Length}";

            label.text = text;
        }
    }
}
