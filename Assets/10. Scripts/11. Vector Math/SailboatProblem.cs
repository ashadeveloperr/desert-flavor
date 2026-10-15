// ─────────────────────────────────────────────
// SailboatProblem — [문제] 돛단배와 바람 : 이 파일의 TODO 세 곳을 채웁니다
// 학습 포인트
//  - 내적의 둘째 얼굴 '그림자 길이' : 기준 방향만 길이 1이면 내적은 다른 벡터가 그쪽으로 뻗은 길이입니다 (03 §3.3)
//  - 그림자를 두 번 : 바람 -> 돛이 받는 힘 -> 배를 앞으로 미는 몫
//  - 각도 판정은 반각의 코사인과 내적을 비교합니다. 각이 작을수록 내적이 큽니다 (03 §3.5)
// 조작
//  - 없음 (씬 조작은 화면 왼쪽 위 안내를 봅니다)
// 확인
//  - Play 하면 왼쪽 아래 '채점' 패널에 통과 개수가 나옵니다. 0 / 7 을 7 / 7 로 만드세요.
//  - 콘솔(Console) 창에는 케이스 일곱 개의 결과가 모두 찍힙니다.
// ─────────────────────────────────────────────
using UnityEngine;

namespace Study3D.VectorMath
{
    public class SailboatProblem : SailboatLab
    {
        // TODO 1 — 바람이 돛을 수직으로 미는 세기 (m/s)
        // wind : 바람이 '불어 가는' 방향과 세기(m/s). sailNormal : 돛 면에 수직인 방향 (길이 1 보장 없음)
        // 지금은 0 을 돌려줍니다. 그래서 배가 움직이지 않습니다.
        // 힌트 : 결과에 바람 세기가 그대로 남아야 합니다. 어느 쪽 벡터만 길이 1로 맞춰야 할까요?
        public override float WindOnSail(Vector3 wind, Vector3 sailNormal)
        {
            // wind 세기가 있음
            // sail의 정면은 바람의 정면과 동일함.
            float dotProduct = Vector3.Dot(wind, sailNormal.normalized);
            return dotProduct;
        }

        // TODO 2 — 돛이 받은 힘 중 배를 앞으로 미는 몫
        // 배는 용골 때문에 앞뒤로만 나아갑니다. 옆으로 미는 몫은 용골이 막아 줍니다.
        // 지금은 0 을 돌려줍니다.
        public override float DriveAlongKeel(Vector3 sailForce, Vector3 boatForward)
        {
            // sailForce는 돛 자체가 받는 바람의 세기를 의미함
            // boatForward는 배가 나아가는 방향
            // sailForce와 boardForward 내적해서 그 결과에 따라 boat나 나아가는
            // 속도가 결정이 되겠군요
            return Vector3.Dot(sailForce, boatForward.normalized);
        }

        // TODO 3 — 맞바람 구역인가
        // 뱃머리가 바람이 '불어오는' 쪽에서 noGoHalfAngle(도) 안쪽을 보고 있으면 돛이 펄럭여 힘을 못 받습니다.
        // 지금은 늘 false 를 돌려줍니다.
        // 힌트 1 : 바람이 불어오는 쪽은 wind 와 어떤 관계인가요?
        // 힌트 2 : 각도(도)를 내적과 비교하려면 무엇으로 바꿔야 하나요? 부등호 방향도 확인하세요.
        public override bool IsInNoGoZone(Vector3 boatForward, Vector3 wind, float noGoHalfAngle)
        {
            // 맞바람 판정 앵글을 코사인값으로 전환을 해줍니다.
            float cos = Mathf.Cos(noGoHalfAngle * Mathf.Deg2Rad);
            // 바람이 불어가는 방향에서 불어오는 방향이 되도록 뒤집어 줍니다
            Vector3 fromWind = -wind;
            float dotProduct = Vector3.Dot(boatForward, fromWind);
            bool result = dotProduct > cos;
            
            return result;
        }
    }
}
