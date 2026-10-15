using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Jay.MathLecture
{
    /// <summary>
    /// 라인렌더러로 기초 수학 도형을 그리는 강의용 컴포넌트.
    /// - 모든 좌표는 이 오브젝트의 "로컬 XY 평면" 기준이다. (오브젝트를 옮기면 도형도 같이 움직인다)
    /// - 도형마다 자식 오브젝트(LineRenderer)가 하나씩 생성되므로 여러 도형을 겹쳐서 비교할 수 있다.
    /// - [ContextMenu]는 매개변수를 받을 수 없으므로 입력값은 인스펙터 필드로 받는다.
    ///   컴포넌트 우측 상단 ⋮ 메뉴(또는 컴포넌트 헤더 우클릭)에서 실행한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class MathShapeRenderer : MonoBehaviour
    {
        #region Nested Types

        /// <summary>
        /// 삼각함수 그래프 설정.  y = A · f( (2π / T) · (x − h) ) + k
        /// </summary>
        [Serializable]
        public class WaveSettings
        {
            [field: SerializeField, Tooltip("진폭 A : 가운데 선에서 위/아래로 얼마나 올라가는가")]
            public float Amplitude { get; private set; } = 1f;

            [field: SerializeField, Min(0.01f), Tooltip("주기 T : 한 번 오르내리는 데 필요한 x 길이 (기본 2π)")]
            public float Period { get; private set; } = 2f * Mathf.PI;

            [field: SerializeField, Tooltip("x축 평행이동 h : +면 오른쪽으로 이동")]
            public float HorizontalShift { get; private set; }

            [field: SerializeField, Tooltip("y축 평행이동 k : +면 위쪽으로 이동")]
            public float VerticalShift { get; private set; }

            [field: SerializeField]
            public Color Color { get; private set; } = Color.cyan;

            public WaveSettings() { }

            public WaveSettings(Color color) => Color = color;

            /// <summary>각진동수 ω = 2π / T</summary>
            public float AngularFrequency => 2f * Mathf.PI / Period;

            public float Evaluate(Func<float, float> trigFunction, float x) =>
                Amplitude * trigFunction(AngularFrequency * (x - HorizontalShift)) + VerticalShift;
        }

        #endregion

        #region Constants

        private const string ShapePrefix = "Shape_";
        private const string AxisXName = ShapePrefix + "AxisX";
        private const string AxisYName = ShapePrefix + "AxisY";
        private const string StraightLineName = ShapePrefix + "Line";
        private const string RightTriangleName = ShapePrefix + "RightTriangle";
        private const string CircleName = ShapePrefix + "Circle";
        private const string SineName = ShapePrefix + "Sine";
        private const string CosineName = ShapePrefix + "Cosine";
        private const string ParabolaName = ShapePrefix + "Parabola";

        #endregion

        #region Inspector Fields

        [Header("공통 렌더링")]
        [SerializeField, Tooltip("비워두면 Sprites/Default 셰이더로 자동 생성")]
        private Material lineMaterial;
        [SerializeField, Min(0.001f)] private float lineWidth = 0.05f;
        [SerializeField, Range(4, 512), Tooltip("곡선을 몇 개의 점으로 나눠 그릴지 (적을수록 각져 보임)")]
        private int resolution = 128;

        [Header("좌표축 / 격자")]
        [SerializeField, Min(1f)] private float axisLength = 10f;
        [SerializeField] private Color axisColor = new(0.6f, 0.6f, 0.6f, 1f);
        [SerializeField] private bool showGrid = true;
        [SerializeField, Min(0.1f)] private float gridSpacing = 1f;
        [SerializeField] private Color gridColor = new(1f, 1f, 1f, 0.12f);

        [Header("직선")]
        [SerializeField, Min(0f)] private float straightLineLength = 5f;
        [SerializeField] private Color straightLineColor = Color.white;

        [Header("직각 삼각형 (직각은 원점)")]
        [SerializeField, Min(0f)] private float triangleBase = 4f;
        [SerializeField, Min(0f)] private float triangleHeight = 3f;
        [SerializeField] private Color triangleColor = Color.yellow;

        [Header("원 (중심은 원점)")]
        [SerializeField, Min(0f)] private float circleRadius = 3f;
        [SerializeField] private Color circleColor = Color.green;

        [Header("삼각함수 그래프  y = A·f(2π/T·(x−h)) + k")]
        [SerializeField] private Vector2 waveXRange = new(-10f, 10f);
        [SerializeField] private WaveSettings sine = new(Color.cyan);
        [SerializeField] private WaveSettings cosine = new(new Color(1f, 0.5f, 0f));

        [Header("이차함수 포물선  y = ax² + bx + c")]
        [SerializeField] private float quadraticA = 1f;
        [SerializeField] private float quadraticB;
        [SerializeField] private float quadraticC = -4f;
        [SerializeField] private Vector2 parabolaXRange = new(-5f, 5f);
        [SerializeField] private Color parabolaColor = Color.magenta;

        #endregion

        #region Cache

        private Material defaultMaterial;
        private Vector2? parabolaVertex;
        private Vector2[] parabolaRoots = Array.Empty<Vector2>();

        private Material LineMaterial => lineMaterial != null ? lineMaterial : GetOrCreateDefaultMaterial();

        #endregion

        #region Context Menu : Shapes

        [ContextMenu("Math/Draw All")]
        public void DrawAll()
        {
            DrawAxes();
            DrawStraightLine();
            DrawRightTriangle();
            DrawCircle();
            DrawSineGraph();
            DrawCosineGraph();
            DrawParabola();
        }

        [ContextMenu("Math/Axes (X, Y)")]
        public void DrawAxes()
        {
            Draw(AxisXName, new[] { new Vector3(-axisLength, 0f), new Vector3(axisLength, 0f) }, axisColor);
            Draw(AxisYName, new[] { new Vector3(0f, -axisLength), new Vector3(0f, axisLength) }, axisColor);
        }

        /// <summary>원점에서 +x 방향으로 길이만큼 뻗는 직선(선분).</summary>
        [ContextMenu("Math/Straight Line")]
        public void DrawStraightLine() =>
            Draw(StraightLineName, new[] { Vector3.zero, new Vector3(straightLineLength, 0f) }, straightLineColor);

        /// <summary>원점을 직각으로 하는 직각 삼각형. 빗변과 두 예각을 콘솔에 출력한다.</summary>
        [ContextMenu("Math/Right Triangle")]
        public void DrawRightTriangle()
        {
            var points = new[]
            {
                Vector3.zero,                        // 직각 꼭짓점
                new Vector3(triangleBase, 0f),       // 밑변 끝
                new Vector3(0f, triangleHeight)      // 높이 끝
            };
            Draw(RightTriangleName, points, triangleColor, loop: true);
            LogTriangleInfo();
        }

        /// <summary>원의 매개변수 방정식 (r·cosθ, r·sinθ) 으로 점을 찍는다.</summary>
        [ContextMenu("Math/Circle")]
        public void DrawCircle()
        {
            var points = Enumerable.Range(0, resolution)
                .Select(i => 2f * Mathf.PI * i / resolution)
                .Select(theta => new Vector3(circleRadius * Mathf.Cos(theta), circleRadius * Mathf.Sin(theta)));

            Draw(CircleName, points, circleColor, loop: true);
        }

        [ContextMenu("Math/Sine Graph")]
        public void DrawSineGraph() =>
            Draw(SineName, SampleFunction(waveXRange, x => sine.Evaluate(Mathf.Sin, x)), sine.Color);

        [ContextMenu("Math/Cosine Graph")]
        public void DrawCosineGraph() =>
            Draw(CosineName, SampleFunction(waveXRange, x => cosine.Evaluate(Mathf.Cos, x)), cosine.Color);

        /// <summary>y = ax² + bx + c 를 그리고 꼭짓점·판별식·근을 계산해 기즈모와 로그로 보여준다.</summary>
        [ContextMenu("Math/Parabola")]
        public void DrawParabola()
        {
            Draw(ParabolaName, SampleFunction(parabolaXRange, EvaluateQuadratic), parabolaColor);
            AnalyzeQuadratic();
        }

        #endregion

        #region Context Menu : Utility

        [ContextMenu("Math/Clear All")]
        public void ClearAll()
        {
            transform.Cast<Transform>()
                .Where(child => child.name.StartsWith(ShapePrefix))
                .ToList()                       // 순회 중 삭제를 피하기 위해 먼저 목록으로 복사
                .ForEach(child => DestroySafely(child.gameObject));

            parabolaVertex = null;
            parabolaRoots = Array.Empty<Vector2>();
        }

        #endregion

        #region Math

        private float EvaluateQuadratic(float x) => quadraticA * x * x + quadraticB * x + quadraticC;

        /// <summary>x 범위를 resolution 등분해서 (x, f(x)) 점 목록을 만든다.</summary>
        private IEnumerable<Vector3> SampleFunction(Vector2 xRange, Func<float, float> function) =>
            Enumerable.Range(0, resolution + 1)
                .Select(i => Mathf.Lerp(xRange.x, xRange.y, (float)i / resolution))
                .Select(x => new Vector3(x, function(x)));

        private void LogTriangleInfo()
        {
            var hypotenuse = Mathf.Sqrt(triangleBase * triangleBase + triangleHeight * triangleHeight);
            var angleAtBaseEnd = Mathf.Atan2(triangleHeight, triangleBase) * Mathf.Rad2Deg;
            var angleAtHeightEnd = 90f - angleAtBaseEnd;

            Debug.Log(
                $"[직각 삼각형] 밑변 a = {triangleBase:F2}, 높이 b = {triangleHeight:F2}\n" +
                $"빗변 c = √(a² + b²) = √({triangleBase * triangleBase:F2} + {triangleHeight * triangleHeight:F2}) = {hypotenuse:F3}\n" +
                $"각도 : 직각 90° / 밑변 끝 {angleAtBaseEnd:F2}° / 높이 끝 {angleAtHeightEnd:F2}° (합 180°)\n" +
                $"sinθ = b/c = {triangleHeight / hypotenuse:F3}, cosθ = a/c = {triangleBase / hypotenuse:F3}, tanθ = b/a = {(triangleBase > 0f ? triangleHeight / triangleBase : float.PositiveInfinity):F3}",
                this);
        }

        private void AnalyzeQuadratic()
        {
            if (Mathf.Approximately(quadraticA, 0f))
            {
                parabolaVertex = null;
                parabolaRoots = Array.Empty<Vector2>();
                Debug.LogWarning("[포물선] a = 0 이면 이차함수가 아니라 직선(일차함수)입니다.", this);
                return;
            }

            // 꼭짓점 : x = −b / 2a
            var vertexX = -quadraticB / (2f * quadraticA);
            parabolaVertex = new Vector2(vertexX, EvaluateQuadratic(vertexX));

            // 판별식 D = b² − 4ac  →  근의 개수 결정
            var discriminant = quadraticB * quadraticB - 4f * quadraticA * quadraticC;
            parabolaRoots = CalculateRoots(discriminant)
                .Select(x => new Vector2(x, 0f))
                .ToArray();

            var rootText = parabolaRoots.Length switch
            {
                0 => "실근 없음 (x축과 만나지 않음)",
                1 => $"중근 x = {parabolaRoots[0].x:F3} (x축에 접함)",
                _ => $"x = {parabolaRoots[0].x:F3}, {parabolaRoots[1].x:F3} (x축과 두 점에서 만남)"
            };

            Debug.Log(
                $"[포물선] y = {quadraticA}x² + {quadraticB}x + {quadraticC}\n" +
                $"꼭짓점 = ({parabolaVertex.Value.x:F3}, {parabolaVertex.Value.y:F3}), {(quadraticA > 0f ? "아래로 볼록 (최솟값)" : "위로 볼록 (최댓값)")}\n" +
                $"판별식 D = b² − 4ac = {discriminant:F3} → {rootText}",
                this);
        }

        /// <summary>근의 공식 x = (−b ± √D) / 2a</summary>
        private IEnumerable<float> CalculateRoots(float discriminant)
        {
            if (discriminant < 0f)
                yield break;

            var twoA = 2f * quadraticA;

            if (Mathf.Approximately(discriminant, 0f))
            {
                yield return -quadraticB / twoA;
                yield break;
            }

            var sqrtD = Mathf.Sqrt(discriminant);
            yield return (-quadraticB - sqrtD) / twoA;
            yield return (-quadraticB + sqrtD) / twoA;
        }

        #endregion

        #region LineRenderer

        private void Draw(string shapeName, IEnumerable<Vector3> points, Color color, bool loop = false)
        {
            var line = GetOrCreateLine(shapeName);
            var positions = points.ToArray();

            line.positionCount = positions.Length;
            line.SetPositions(positions);
            line.loop = loop;
            line.startColor = line.endColor = color;
            line.startWidth = line.endWidth = lineWidth;

            MarkDirty(line);
        }

        private LineRenderer GetOrCreateLine(string shapeName)
        {
            var child = transform.Find(shapeName);
            if (child == null)
            {
                child = new GameObject(shapeName).transform;
                child.SetParent(transform, worldPositionStays: false);
            }

            if (!child.TryGetComponent(out LineRenderer line))
                line = child.gameObject.AddComponent<LineRenderer>();

            line.useWorldSpace = false;         // 부모 Transform을 따라 움직이도록 로컬 좌표 사용
            line.sharedMaterial = LineMaterial; // 에디터에서 머티리얼 인스턴스 누수 방지
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            return line;
        }

        private Material GetOrCreateDefaultMaterial()
        {
            if (defaultMaterial != null)
                return defaultMaterial;

            defaultMaterial = new Material(Shader.Find("Sprites/Default"))
            {
                name = "MathShape_Default (Runtime)",
                hideFlags = HideFlags.DontSave
            };
            return defaultMaterial;
        }

        private static void DestroySafely(UnityEngine.Object target)
        {
            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        private static void MarkDirty(UnityEngine.Object target)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorUtility.SetDirty(target);
#endif
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;

            if (showGrid)
                DrawGridGizmo();

            if (transform.Find(ParabolaName) != null)
                DrawParabolaMarkers();
        }

        private void DrawGridGizmo()
        {
            Gizmos.color = gridColor;
            var lineCount = Mathf.FloorToInt(axisLength / gridSpacing);

            foreach (var offset in Enumerable.Range(-lineCount, lineCount * 2 + 1).Select(i => i * gridSpacing))
            {
                Gizmos.DrawLine(new Vector3(offset, -axisLength), new Vector3(offset, axisLength));
                Gizmos.DrawLine(new Vector3(-axisLength, offset), new Vector3(axisLength, offset));
            }
        }

        private void DrawParabolaMarkers()
        {
            var markerSize = lineWidth * 3f;

            if (parabolaVertex.HasValue)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(parabolaVertex.Value, markerSize);
            }

            Gizmos.color = Color.white;
            foreach (var root in parabolaRoots)
                Gizmos.DrawWireSphere(root, markerSize);
        }

        #endregion

        #region Unity Callbacks

        private void OnDestroy()
        {
            if (defaultMaterial != null)
                DestroySafely(defaultMaterial);
        }

        #endregion
    }
}
