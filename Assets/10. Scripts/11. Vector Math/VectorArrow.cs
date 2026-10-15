// ─────────────────────────────────────────────
// VectorArrow — 시작점과 벡터를 받아 Game 뷰에 보이는 화살표로 그린다
// 학습 포인트
//  - 벡터는 시작점이 없는 양입니다. 그래서 "어디서부터 그릴지"를 따로 넘깁니다(01 벡터).
//  - 화살표 몸통은 원기둥, 머리는 원뿔입니다. 길이가 바뀌면 몸통만 늘고 머리 크기는 그대로입니다.
// 조작
//  - 없음
// ─────────────────────────────────────────────
using UnityEngine;

namespace Study3D.VectorMath
{
    public class VectorArrow : MonoBehaviour
    {
        [Header("부품")]
        [SerializeField] private Transform shaft;
        [SerializeField] private Transform head;

        [Header("모양")]
        [SerializeField] private float thickness = 0.06f;
        [SerializeField] private float headLength = 0.25f;
        [SerializeField] private float headWidth = 0.18f;
        [SerializeField] private float hideBelow = 0.02f;

        public void Show(Vector3 origin, Vector3 vector)
        {
            if (shaft == null || head == null) return;

            float length = vector.magnitude;
            if (float.IsNaN(length) || length < hideBelow)
            {
                SetParts(false);
                return;
            }

            SetParts(true);

            transform.position = origin;
            transform.rotation = Quaternion.FromToRotation(Vector3.up, vector / length);

            float headLen = Mathf.Min(headLength, length * 0.5f);
            float shaftLen = length - headLen;

            shaft.localPosition = new Vector3(0f, shaftLen * 0.5f, 0f);
            shaft.localScale = new Vector3(thickness, shaftLen * 0.5f, thickness);

            head.localPosition = new Vector3(0f, shaftLen + headLen * 0.5f, 0f);
            head.localScale = new Vector3(headWidth, headLen, headWidth);
        }

        public void Hide()
        {
            SetParts(false);
        }

        private void SetParts(bool visible)
        {
            if (shaft.gameObject.activeSelf != visible)
            {
                shaft.gameObject.SetActive(visible);
            }

            if (head.gameObject.activeSelf != visible)
            {
                head.gameObject.SetActive(visible);
            }
        }
    }
}
