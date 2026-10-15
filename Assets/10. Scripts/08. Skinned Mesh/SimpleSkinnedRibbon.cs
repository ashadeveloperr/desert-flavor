// ─────────────────────────────────────────────
// SimpleSkinnedRibbon — 정점·웨이트·바인드 포즈를 코드로 만들어 본 2개로 움직이는 리본을 세운다
// 학습 포인트
//  - 스킨드 메시는 메시 하나에 본 배열 · 정점별 웨이트 · 바인드 포즈 세 가지가 더 붙은 것입니다. 세 개가 인덱스로 짝이 맞아야 합니다.
//  - 웨이트는 "이 정점이 어느 본을 얼마나 따라가는가"입니다. 0이면 안 따라가고 1이면 그대로 따라갑니다.
//  - 바인드 포즈는 "본이 처음 어디 있었는지"를 적어 둔 행렬입니다. 그래서 본을 움직이면 그 차이만큼 정점이 움직입니다.
// 조작
//  - 없음
// ─────────────────────────────────────────────
using UnityEngine;

namespace Study3D.SkinnedMeshes
{
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public class SimpleSkinnedRibbon : MonoBehaviour
    {
        [Header("본")]
        [SerializeField] private Transform boneA;
        [SerializeField] private Transform boneB;

        [Header("모양")]
        [SerializeField] private float length = 2.5f;
        [SerializeField] private float width = 0.6f;

        [Header("웨이트")]
        [SerializeField] private float weightOnB = 0.5f;

        private void OnValidate()
        {
            length = Mathf.Max(0.1f, length);
            width = Mathf.Max(0.1f, width);
            weightOnB = Mathf.Clamp01(weightOnB);
        }

        // 메시는 한 번만 만들면 되므로 Update 가 없습니다.
        private void Start()
        {
            if (boneA == null || boneB == null)
            {
                Debug.LogError("[Study3D] SimpleSkinnedRibbon: boneA 와 boneB 를 연결하세요.", this);
                enabled = false;
                return;
            }

            // 로컬 좌표 정점 6개: 줄 0(z=0), 줄 1(z=length/2), 줄 2(z=length), 각 줄에 왼쪽·오른쪽
            float half = width * 0.5f;
            float mid = length * 0.5f;
            var verts = new[]
            {
                new Vector3(-half, 0f, 0f), new Vector3(half, 0f, 0f),
                new Vector3(-half, 0f, mid), new Vector3(half, 0f, mid),
                new Vector3(-half, 0f, length), new Vector3(half, 0f, length)
            };

            // +Y 에서 내려다볼 때 시계 방향으로 감아야 앞면이 보입니다(01. Rendering 의 SimpleQuadMesh 와 같은 규칙).
            var tris = new[] { 0, 2, 1, 1, 2, 3, 2, 4, 3, 3, 4, 5 };

            // 웨이트 합은 항상 1 이어야 합니다. 쓰지 않는 본 자리는 weight 0 으로 둡니다.
            var weights = new BoneWeight[6];
            weights[0] = MakeWeight(1f, 0f);
            weights[1] = MakeWeight(1f, 0f);
            // 가운데 줄은 끝 줄의 절반만 따라갑니다. 뿌리에서 멀어질수록 더 따라가는 자연스러운 분배입니다.
            float midWeight = weightOnB * 0.5f;
            weights[2] = MakeWeight(1f - midWeight, midWeight);
            weights[3] = MakeWeight(1f - midWeight, midWeight);
            // 끝 줄도 weightOnB 를 그대로 씁니다. 세 리본의 차이가 여기서 가장 크게 보입니다
            // (끝 줄을 셋 다 1 로 고정하면 끝이 똑같이 움직여 웨이트 차이가 묻힙니다).
            weights[4] = MakeWeight(1f - weightOnB, weightOnB);
            weights[5] = MakeWeight(1f - weightOnB, weightOnB);

            // 바인드 포즈 = 본이 처음 있던 자리를 적어 두는 것. 본을 이미 배치한 뒤라서 지금 자리를 그대로 읽습니다.
            // bones 배열과 같은 순서(0 = A, 1 = B)여야 합니다.
            var binds = new Matrix4x4[2];
            binds[0] = boneA.worldToLocalMatrix * transform.localToWorldMatrix;
            binds[1] = boneB.worldToLocalMatrix * transform.localToWorldMatrix;

            // 새로 만든 Mesh 는 에셋이 아니므로 sharedMesh 에 넣어도 파일이 바뀌지 않습니다.
            // (FBX 가 들고 온 sharedMesh 를 고치는 것과 다릅니다. BlendShapeDial 참고)
            var mesh = new Mesh();
            mesh.name = "Skinned Ribbon";
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.boneWeights = weights;
            mesh.bindposes = binds;
            // 바인드 포즈 자세 기준으로 계산합니다. 본을 많이 꺾으면 음영이 어색할 수 있고, 그것이 스키닝의 한계입니다.
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var smr = GetComponent<SkinnedMeshRenderer>();
            smr.bones = new[] { boneA, boneB };
            smr.rootBone = boneA;
            smr.sharedMesh = mesh;
            // 메시를 런타임에 넣으므로 씬에 저장된 바운드가 비어 있습니다.
            // 그대로 두면 절두체 컬링으로 리본이 안 보일 수 있어, 매 프레임 바운드를 갱신하게 켭니다.
            smr.updateWhenOffscreen = true;
        }

        // 합은 항상 1 이고, **큰 가중치를 0번 슬롯에** 넣습니다.
        // 품질 설정(QualitySettings.skinWeights)이 본 1개로 내려가면 0번 슬롯만 쓰이기 때문입니다.
        private static BoneWeight MakeWeight(float onA, float onB)
        {
            var w = new BoneWeight();
            if (onA >= onB)
            {
                w.boneIndex0 = 0; w.weight0 = onA;
                w.boneIndex1 = 1; w.weight1 = onB;
            }
            else
            {
                w.boneIndex0 = 1; w.weight0 = onB;
                w.boneIndex1 = 0; w.weight1 = onA;
            }

            return w;
        }
    }
}
