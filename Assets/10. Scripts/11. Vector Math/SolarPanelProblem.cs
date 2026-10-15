// ─────────────────────────────────────────────
// SolarPanelProblem — [문제] 태양광 발전소 : 이 파일의 TODO 세 곳을 채웁니다
// 학습 포인트
//  - 내적의 첫째 얼굴 '닮은 정도' : 두 벡터를 모두 길이 1로 맞추면 내적은 -1 ~ 1 입니다 (03 §3.2)
//  - 음수는 '뒤쪽' : 해가 패널 뒤에 있으면 내적이 음수입니다 (03 §3.4)
//  - 여럿 중 가장 닮은 방향 고르기 = 내적의 최댓값 (03 §3.4)
// 조작
//  - 없음 (씬 조작은 화면 왼쪽 위 안내를 봅니다)
// 확인
//  - Play 하면 왼쪽 아래 '채점' 패널에 통과 개수가 나옵니다. 0 / 6 을 6 / 6 으로 만드세요.
//  - 콘솔(Console) 창에는 케이스 여섯 개의 결과가 모두 찍힙니다.
// ─────────────────────────────────────────────

using System.Numerics;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace Study3D.VectorMath
{
    public class SolarPanelProblem : SolarPanelLab
    {
        // TODO 1 — 해가 있는 쪽 방향
        // lightForward 는 Directional Light 의 transform.forward, 즉 빛이 '나아가는' 방향입니다.
        // 지금은 받은 값을 그대로 돌려줍니다. 그래서 해를 등진 패널이 발전합니다.
        public override Vector3 DirectionToSun(Vector3 lightForward)
        {
            // 해가 바라보는 방향을 뒤집어줘야 해를 바라보는 방향을 알수있죠
            return -lightForward;
        }

        // TODO 2 — 패널이 해를 얼마나 정면으로 받는가 (0 ~ 1)
        // panelNormal : 패널 면에 수직인 방향, toSun : 패널에서 해 쪽 방향. 둘 다 길이가 1이라는 보장은 없습니다.
        // 지금은 해가 어디 있든 1(최대)을 돌려줍니다.
        // 힌트 1 : 두 벡터가 얼마나 '같은 쪽'을 보는지 재려면 두 벡터가 어떤 길이여야 하나요?
        // 힌트 2 : 해가 패널 뒤에 있을 때 발전량이 음수가 되면 안 됩니다.
        public override float Sunlight(Vector3 panelNormal, Vector3 toSun)
        {
            // 내적을 InnerProduct 라고 하는데
            // DotProduct라고 부르는 내적은 각 벡터의 단위 길이가 1인 내적을 DotProduct라고 부를겁니다.\
            float dotProduct = Vector3.Dot(panelNormal.normalized, toSun.normalized);
            // 해가 패널뒤에 있으면 음수가 나온다 라는 말이죠?
            return Mathf.Max(0, dotProduct);
        }

        // TODO 3 — 후보 방향 중 해와 가장 닮은 방향의 번호
        // candidates 의 방향은 모두 길이 1 입니다. 지금은 늘 0번(평평)을 고릅니다.
        // 힌트 : 모든 후보가 해를 등지고 있어도(내적이 전부 음수) 그중 가장 큰 것을 골라야 합니다.
        public override int BestDirection(Vector3[] candidates, Vector3 toSun)
        {
            int bestIndex = -1;
            float bestDotResult = float.MinValue;

            for (int i = 0; i < candidates.Length; ++i)
            {
                float dotProduct = Vector3.Dot(candidates[i], toSun);
                
                if(dotProduct > bestDotResult)
                {
                    bestIndex = i;
                    bestDotResult = dotProduct; 
                }
            }
            
            return bestIndex;
        }
    }
}
