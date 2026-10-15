// ─────────────────────────────────────────────
// ExerciseLab — 문제 메서드를 정해진 입력으로 불러 기대값과 비교하고, 통과 개수와 첫 실패를 화면에 찍는다
// 학습 포인트
//  - 채점 케이스는 입력과 기대값만 적습니다. 정답 공식은 들어 있지 않습니다.
//  - 케이스마다 흔한 실수 하나를 잡도록 골랐고, 실패하면 그 실수에 맞춘 힌트를 보여 줍니다.
// 조작
//  - 없음
// ─────────────────────────────────────────────
using TMPro;
using UnityEngine;

namespace Study3D.VectorMath
{
    public abstract class ExerciseLab : MonoBehaviour
    {
        [Header("채점")]
        [SerializeField] private TMP_Text checkText;
        [SerializeField] private float tolerance = 0.01f;

        private int passed;
        private int total;
        private string firstFailure;

        protected virtual void Start()
        {
            RunSelfCheck();
        }

        public void RunSelfCheck()
        {
            passed = 0;
            total = 0;
            firstFailure = null;

            try
            {
                AddCases();
            }
            catch (System.Exception e)
            {
                firstFailure = $"케이스 {total + 1} 실행 중 오류\n{e.GetType().Name} : {e.Message}";
                Debug.LogException(e);
            }

            string summary = firstFailure == null
                ? $"채점 : {passed} / {total} 통과\n모든 케이스를 통과했습니다"
                : $"채점 : {passed} / {total} 통과\n{firstFailure}";

            if (checkText != null)
            {
                checkText.text = summary;
            }

            Debug.Log($"[채점] {GetType().Name} : {passed} / {total} 통과");
        }

        protected abstract void AddCases();

        protected void CheckFloat(string label, float expected, float actual, string hint)
        {
            bool ok = Mathf.Abs(expected - actual) <= tolerance;
            Record(label, ok, expected.ToString("F2"), actual.ToString("F2"), hint);
        }

        protected void CheckVector(string label, Vector3 expected, Vector3 actual, string hint)
        {
            bool ok = (expected - actual).magnitude <= tolerance;
            Record(label, ok, expected.ToString("F2"), actual.ToString("F2"), hint);
        }

        protected void CheckInt(string label, int expected, int actual, string hint)
        {
            bool ok = expected == actual;
            Record(label, ok, expected.ToString(), actual.ToString(), hint);
        }

        protected void CheckBool(string label, bool expected, bool actual, string hint)
        {
            bool ok = expected == actual;
            Record(label, ok, expected ? "참" : "거짓", actual ? "참" : "거짓", hint);
        }

        private void Record(string label, bool ok, string expectedText, string actualText, string hint)
        {
            total++;

            if (ok)
            {
                passed++;
                Debug.Log($"[채점] 통과 · 케이스 {total} {label}");
            }
            else
            {
                Debug.LogWarning($"[채점] 실패 · 케이스 {total} {label} · 기대 {expectedText} · 내 값 {actualText} · 힌트 : {hint}");

                if (firstFailure == null)
                {
                    firstFailure = $"실패 : 케이스 {total} · {label}\n기대 {expectedText}  ·  내 값 {actualText}\n힌트 : {hint}";
                }
            }
        }
    }
}
