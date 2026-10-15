// ─────────────────────────────────────────────
// SailboatLab — 돛단배 문제지 : 풀어야 할 메서드 세 개와 채점 케이스 일곱 개
// 학습 포인트
//  - 이 파일은 문제의 틀입니다. 수강생은 고치지 않고 SailboatProblem을 고칩니다.
//  - 무대 스크립트(Sailboat)는 이 타입으로만 문제를 부르므로, 문제 씬과 답안 씬이 같은 무대를 씁니다.
// 조작
//  - 없음
// ─────────────────────────────────────────────
using UnityEngine;

namespace Study3D.VectorMath
{
    public abstract class SailboatLab : ExerciseLab
    {
        public abstract float WindOnSail(Vector3 wind, Vector3 sailNormal);
        public abstract float DriveAlongKeel(Vector3 sailForce, Vector3 boatForward);
        public abstract bool IsInNoGoZone(Vector3 boatForward, Vector3 wind, float noGoHalfAngle);

        protected override void AddCases()
        {
            CheckFloat("바람 8 m/s · 돛 법선 45도",
                5.656854f,
                WindOnSail(new Vector3(8f, 0f, 0f), new Vector3(0.7071068f, 0f, 0.7071068f)),
                "바람은 정규화하지 않습니다 (세기가 결과에 남아야 함)");

            CheckFloat("돛 법선의 길이가 2",
                8f,
                WindOnSail(new Vector3(8f, 0f, 0f), new Vector3(2f, 0f, 0f)),
                "기준 방향(돛 법선)만 길이 1로 맞춥니다");

            CheckFloat("돛 뒷면으로 부는 바람",
                -6f,
                WindOnSail(new Vector3(-6f, 0f, 0f), Vector3.right),
                "음수를 자르지 않습니다 (반대쪽에서 민다는 뜻)");

            CheckFloat("돛 힘 (4, 0, 4) · 배 앞 +z",
                4f,
                DriveAlongKeel(new Vector3(4f, 0f, 4f), Vector3.forward),
                "힘을 배의 앞 방향에 드리운 그림자입니다");

            CheckFloat("배 앞 방향의 길이가 3",
                4f,
                DriveAlongKeel(new Vector3(4f, 0f, 4f), new Vector3(0f, 0f, 3f)),
                "기준 방향(배 앞)을 길이 1로 맞춥니다");

            CheckBool("정면에서 불어오는 바람",
                true,
                IsInNoGoZone(Vector3.forward, new Vector3(0f, 0f, -8f), 45f),
                "바람이 불어오는 쪽은 -wind 입니다");

            CheckBool("40도 앞에서 불어오는 바람",
                true,
                IsInNoGoZone(Vector3.forward, new Vector3(5.142301f, 0f, -6.128356f), 45f),
                "각이 작을수록 내적이 큽니다 (부등호 방향)");
        }
    }
}
