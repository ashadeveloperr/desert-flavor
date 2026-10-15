// ─────────────────────────────────────────────
// SolarPanelLab — 태양광 발전소 문제지 : 풀어야 할 메서드 세 개와 채점 케이스 여섯 개
// 학습 포인트
//  - 이 파일은 문제의 틀입니다. 수강생은 고치지 않고 SolarPanelProblem을 고칩니다.
//  - 무대 스크립트(SolarPanel, SunTracker)는 이 타입으로만 문제를 부르므로, 문제 씬과 답안 씬이 같은 무대를 씁니다.
// 조작
//  - 없음
// ─────────────────────────────────────────────
using UnityEngine;

namespace Study3D.VectorMath
{
    public abstract class SolarPanelLab : ExerciseLab
    {
        public abstract Vector3 DirectionToSun(Vector3 lightForward);
        public abstract float Sunlight(Vector3 panelNormal, Vector3 toSun);
        public abstract int BestDirection(Vector3[] candidates, Vector3 toSun);

        protected override void AddCases()
        {
            CheckVector("빛 방향 (0, -0.87, 0.5) 에서 해 쪽",
                new Vector3(0f, 0.8660254f, -0.5f),
                DirectionToSun(new Vector3(0f, -0.8660254f, 0.5f)),
                "빛이 나아가는 방향의 반대쪽에 해가 있습니다");

            CheckFloat("평평한 패널 · 정오의 해",
                0.8660254f,
                Sunlight(Vector3.up, new Vector3(0f, 0.8660254f, -0.5f)),
                "두 방향이 길이 1이면 내적이 곧 닮은 정도입니다");

            CheckFloat("길이가 1이 아닌 두 벡터",
                0.8660254f,
                Sunlight(new Vector3(0f, 2f, 0f), new Vector3(0f, 8.660254f, -5f)),
                "닮은 정도로 읽으려면 둘 다 정규화합니다");

            CheckFloat("해를 등진 패널",
                0f,
                Sunlight(Vector3.forward, new Vector3(0f, 0.8660254f, -0.5f)),
                "내적이 음수면 0으로 자릅니다");

            CheckInt("해가 동쪽 30도 높이일 때",
                1,
                BestDirection(new[] { Vector3.up, new Vector3(0.7071068f, 0.7071068f, 0f), new Vector3(-0.7071068f, 0.7071068f, 0f) },
                    new Vector3(0.8660254f, 0.5f, 0f)),
                "내적이 가장 큰 후보의 번호를 돌려줍니다");

            CheckInt("모든 후보가 해를 등질 때",
                1,
                BestDirection(new[] { Vector3.right, new Vector3(0.6f, 0.8f, 0f) }, Vector3.left),
                "시작 최댓값을 0이 아니라 음의 무한대로 둡니다");
        }
    }
}
