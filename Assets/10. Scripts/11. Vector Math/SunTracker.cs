// ─────────────────────────────────────────────
// SunTracker — 후보 방향 아홉 개 중 해와 가장 닮은 방향을 골라 패널을 그쪽으로 천천히 돌린다
// 학습 포인트
//  - "가장 닮은 방향"은 내적이 가장 큰 방향입니다. 각도를 계산하지 않고 내적끼리만 비교합니다(03 §3.4 표 마지막 줄).
//  - 후보는 평평(위) 하나와, 여덟 방위로 45도 기울인 방향 여덟 개입니다.
// 조작
//  - 없음
// ─────────────────────────────────────────────
using UnityEngine;

namespace Study3D.VectorMath
{
    public class SunTracker : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private SolarPanelLab lab;
        [SerializeField] private SunCycle sunCycle;

        [Header("후보")]
        [SerializeField] [Range(0f, 80f)] private float tiltDegrees = 45f;

        [Header("회전")]
        [SerializeField] private float turnDegreesPerSecond = 90f;

        private static readonly string[] CandidateNames =
        {
            "평평", "북", "북동", "동", "남동", "남", "남서", "서", "북서",
        };

        private Vector3[] candidates;
        private bool warnedIndex;

        public int CurrentIndex { get; private set; }
        public string CurrentName { get; private set; } = CandidateNames[0];

        private void Awake()
        {
            BuildCandidates();
        }

        private void BuildCandidates()
        {
            candidates = new Vector3[9];
            candidates[0] = Vector3.up;

            for (int i = 0; i < 8; i++)
            {
                float az = i * 45f;
                Vector3 tilted = Quaternion.AngleAxis(tiltDegrees, Vector3.right) * Vector3.up;
                candidates[i + 1] = Quaternion.AngleAxis(az, Vector3.up) * tilted;
            }
        }

        private void LateUpdate()
        {
            if (lab == null || sunCycle == null) return;

            int index = 0;

            if (sunCycle.IsDaytime)
            {
                Vector3 toSun = lab.DirectionToSun(sunCycle.LightForward);
                index = lab.BestDirection(candidates, toSun);
            }

            // 학생 코드가 범위 밖 번호를 돌려주면 배열에서 예외가 납니다. 평평(0번)으로 두고 경고는 한 번만 찍습니다.
            if (index < 0 || index >= candidates.Length)
            {
                if (!warnedIndex)
                {
                    Debug.LogWarning("[SunTracker] BestDirection 이 범위 밖 번호를 돌려주었습니다: " + index);
                    warnedIndex = true;
                }

                index = 0;
            }

            CurrentIndex = index;
            CurrentName = CandidateNames[index];

            Quaternion target = Quaternion.FromToRotation(Vector3.up, candidates[index]);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnDegreesPerSecond * Time.deltaTime);
        }
    }
}
