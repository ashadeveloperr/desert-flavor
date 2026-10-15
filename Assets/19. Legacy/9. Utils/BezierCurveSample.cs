using UnityEngine;

public class BezierCurveSample : MonoBehaviour
{
    #region Fields
    [Header("Anchor Points")]
    [Tooltip("곡선의 시작점입니다.")]
    [SerializeField] private Transform _p0;
    [Tooltip("곡선의 끝점입니다.")]
    [SerializeField] private Transform _p3;

    [Header("Control Direction Points")]
    [Tooltip("시작점에서 뻗어나가는 방향을 결정하는 핸들입니다.")]
    [SerializeField] private Transform _p1Handle;
    [Tooltip("끝점에서 뻗어나가는 방향을 결정하는 핸들입니다.")]
    [SerializeField] private Transform _p2Handle;

    [Header("Bezier Settings")]
    [Tooltip("인스펙터에서 곡선의 곡률(기울기 부풀림 정도)을 제어합니다.")]
    [SerializeField] private float _curvature = 1.0f;

    [Tooltip("곡선 위를 이동하는 원의 진행도입니다. (0 = 시작, 1 = 끝)")]
    [SerializeField][Range(0f, 1f)] private float _progress = 0.0f;

    [Header("Debug Display Settings")]
    [SerializeField][Range(10, 100)] private int _segmentCount = 50;
    [SerializeField] private Color _curveColor = Color.green;
    [SerializeField] private Color _lineColor = Color.gray;
    [SerializeField] private Color _movableObjectColor = Color.cyan;

    [Space]
    [SerializeField] private float _anchorRadius = 0.12f;
    [SerializeField] private float _handleRadius = 0.08f;
    [SerializeField] private float _movableObjectRadius = 0.18f;
    #endregion

    #region Unity Gizmos Events
    private void OnDrawGizmos()
    {
        // 필수 트랜스폼 컴포넌트 missing 체크
        if (_p0 == null || _p3 == null || _p1Handle == null || _p2Handle == null)
            return;

        // 1. 기본 위치 및 가중치(곡률)가 적용된 제어점 연산
        Vector2 p0Pos = _p0.position;
        Vector2 p3Pos = _p3.position;

        // Handle 트랜스폼들의 방향 벡터를 구한 뒤, _curvature(곡률)만큼 길이를 곱해 제어점 생성
        Vector2 p1Direction = ((Vector2)_p1Handle.position - p0Pos).normalized;
        Vector2 p2Direction = ((Vector2)_p2Handle.position - p3Pos).normalized;

        Vector2 p1Pos = p0Pos + p1Direction * _curvature;
        Vector2 p2Pos = p3Pos + p2Direction * _curvature;

        // 2. 디버깅 가이드라인 및 곡선 그리기
        DrawControlLines(p0Pos, p1Pos, p2Pos, p3Pos);
        DrawBezierCurve(p0Pos, p1Pos, p2Pos, p3Pos);
        DrawControlPoints(p0Pos, p1Pos, p2Pos, p3Pos);

        // 3. 진행도(_progress)에 따른 원 도형 시각화
        DrawMovableObject(p0Pos, p1Pos, p2Pos, p3Pos);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// 제어선 및 핸들 가이드라인을 시각화합니다.
    /// </summary>
    private void DrawControlLines(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
    {
        Gizmos.color = _lineColor;
        Gizmos.DrawLine(p0, p1);
        Gizmos.DrawLine(p3, p2);
    }

    /// <summary>
    /// 설정된 세그먼트 수만큼 베지어 곡선을 정밀하게 그립니다.
    /// </summary>
    private void DrawBezierCurve(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
    {
        Gizmos.color = _curveColor;
        Vector2 previousPoint = p0;

        for (int i = 1; i <= _segmentCount; i++)
        {
            float t = i / (float)_segmentCount;
            Vector2 currentPoint = CalculateCubicBezierPoint(t, p0, p1, p2, p3);

            Gizmos.DrawLine(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }
    }

    /// <summary>
    /// 각 앵커 포인트와 곡률이 계산된 제어 가상 포인트를 화면에 표시합니다.
    /// </summary>
    private void DrawControlPoints(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
    {
        // 시작점과 끝점 (Anchor)
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(p0, _anchorRadius);
        Gizmos.DrawSphere(p3, _anchorRadius);

        // 곡률 가중치가 계산된 실제 제어점 위치 (Control Points)
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(p1, _handleRadius);
        Gizmos.DrawSphere(p2, _handleRadius);
    }

    /// <summary>
    /// 인스펙터의 _progress 값(0~1)에 따라 곡선 위를 움직이는 원 도형을 시각화합니다.
    /// </summary>
    private void DrawMovableObject(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
    {
        Vector2 targetPosition = CalculateCubicBezierPoint(_progress, p0, p1, p2, p3);

        Gizmos.color = _movableObjectColor;
        Gizmos.DrawSphere(targetPosition, _movableObjectRadius);
    }

    /// <summary>
    /// 매개변수 t에 따른 3차 베지어 곡선상의 위치를 계산하는 코어 함수
    /// </summary>
    private Vector2 CalculateCubicBezierPoint(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        Vector2 p = uuu * p0;
        p += 3 * uu * t * p1;
        p += 3 * u * tt * p2;
        p += ttt * p3;

        return p;
    }
    #endregion
}

