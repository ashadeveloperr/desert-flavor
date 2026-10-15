// ─────────────────────────────────────────────
// SkinnedMeshReadout — 스킨드 메시의 본·바인드 포즈·블렌드셰이프 개수를 숫자로 찍는다
// 학습 포인트
//  - bones 와 bindposes 는 같은 개수, 같은 순서여야 합니다. 숫자로 확인할 수 있습니다.
//  - 계층에 있는 본 개수와 bones.Length 는 다를 수 있습니다. 메시를 변형하는 본만 bones 에 들어갑니다.
//  - 블렌드셰이프는 메시가 들고 있는 "변형 모양" 목록입니다. 개수가 0이면 그 모델에는 없다는 뜻입니다.
// 조작
//  - 없음
// ─────────────────────────────────────────────
using TMPro;
using UnityEngine;

namespace Study3D.SkinnedMeshes
{
    public class SkinnedMeshReadout : MonoBehaviour
    {
        [Header("대상")]
        [SerializeField] private SkinnedMeshRenderer target;

        [Header("표시")]
        [SerializeField] private TMP_Text label;
        [SerializeField] private string title = "";

        [Header("표시 항목")]
        [SerializeField] private bool countHierarchyBones = true;

        private bool done;

        // 씬 00 의 리본은 SimpleSkinnedRibbon.Start 에서 메시를 만듭니다.
        // 같은 Start 안에서 읽으면 실행 순서에 따라 비어 보일 수 있어, 첫 Update 에서 한 번만 읽습니다.
        private void Update()
        {
            if (done) return;
            done = true;
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

            string text = "";
            if (title.Length > 0) text += title + "\n";

            // sharedMesh 는 읽기만 합니다. 고치면 에셋이 바뀝니다.
            Mesh mesh = target.sharedMesh;
            if (mesh == null)
            {
                text += "메시가 없습니다";
                label.text = text;
                return;
            }

            text += "bones : " + target.bones.Length + "개 · bindposes : " + mesh.bindposes.Length + "개\n";

            if (countHierarchyBones)
            {
                // 자기 자신도 포함해서 세므로 본 개수가 아니라 Transform 개수라고 적습니다.
                Transform root = target.transform.parent;
                if (target.rootBone != null) root = target.rootBone;
                int count = 0;
                if (root != null) count = root.GetComponentsInChildren<Transform>(true).Length;
                string note = "";
                if (count != target.bones.Length) note = " (bones와 다릅니다)";
                text += "계층의 Transform : " + count + "개" + note + "\n";
            }

            text += "블렌드셰이프 : " + mesh.blendShapeCount + "개 · 정점 : " + mesh.vertexCount + "개";
            label.text = text;
        }
    }
}
