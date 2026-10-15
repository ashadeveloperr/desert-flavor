using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Study3D.ThreeDPhysics
{
    [DefaultExecutionOrder(1000)]
    public sealed class AabbDebugRenderer : MonoBehaviour
    {
        [Header("수집")]
        [SerializeField] private LayerMask layerMask = ~0;
        [SerializeField] private bool includeTriggers = true;
        [SerializeField, Min(0.1f)] private float rescanInterval = 1f;

        [Header("표시")]
        [SerializeField] private bool drawInGameView = true;
        [SerializeField] private bool drawInSceneView = true;
        [SerializeField] private bool drawObb = true;
        [SerializeField] private bool highlightOverlaps = true;
        [SerializeField] private bool ignoreSameRigidbody = true;
        [SerializeField] private bool alwaysOnTop = true;

        [Header("색상")]
        [SerializeField] private Color aabbColor = new(0.2f, 1f, 0.3f, 0.9f);
        [SerializeField] private Color overlapColor = new(1f, 0.25f, 0.2f, 1f);
        [SerializeField] private Color obbColor = new(0.2f, 0.8f, 1f, 0.6f);

        public event Action<IReadOnlyList<(Collider A, Collider B)>> OverlapsUpdated;

        private readonly List<Collider> scannedColliders = new();
        private readonly List<Collider> targets = new();
        private readonly List<Bounds> bounds = new();
        private readonly HashSet<int> overlappedIndices = new();
        private readonly List<(Collider A, Collider B)> overlapPairs = new();
        private readonly SweepAndPruneOverlapDetector detector = new();
        private readonly Vector3[] corners = new Vector3[8];
        private readonly ILineSink glSink = new GLLineSink();
        private readonly ILineSink gizmosSink = new GizmosLineSink();

        private Material lineMaterial;
        private float nextRescanTime;
        private int snapshotFrame = -1;

        public IReadOnlyList<(Collider A, Collider B)> OverlapPairs => overlapPairs;

        private Material LineMaterial => lineMaterial != null ? lineMaterial : lineMaterial = CreateLineMaterial();

        private void OnEnable()
        {
            nextRescanTime = 0f;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering; // SRP / URP
            Camera.onPostRender += DrawGameView;                              // Built-in
        }

        private void OnDisable()
        {
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            Camera.onPostRender -= DrawGameView;

            if (lineMaterial == null) return;
            if (Application.isPlaying) Destroy(lineMaterial);
            else DestroyImmediate(lineMaterial);
            lineMaterial = null;
        }

        private void LateUpdate() => RefreshSnapshot(false);

        #region Snapshot

        private void RefreshSnapshot(bool force)
        {
            if (force && snapshotFrame == Time.frameCount == false) return;
            snapshotFrame = Time.frameCount;

            if (Time.realtimeSinceStartup >= nextRescanTime)
            {
                Rescan();
                nextRescanTime = Time.realtimeSinceStartup + rescanInterval;
            }

            CollectTargets();
            detector.Detect(bounds);
            BuildOverlapResult();
            OverlapsUpdated?.Invoke(overlapPairs);
        }

        private void Rescan()
        {
            scannedColliders.Clear();
            scannedColliders.AddRange(FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        }

        private void CollectTargets()
        {
            targets.Clear();
            bounds.Clear();

            foreach (var collider in scannedColliders)
            {
                if (IsTarget(collider) == false) continue;
                targets.Add(collider);
                bounds.Add(collider.bounds);
            }
        }

        private void BuildOverlapResult()
        {
            overlappedIndices.Clear();
            overlapPairs.Clear();

            foreach (var (a, b) in detector.Pairs)
            {
                if (ignoreSameRigidbody && ShareRigidbody(targets[a], targets[b])) continue;
                overlappedIndices.Add(a);
                overlappedIndices.Add(b);
                overlapPairs.Add((targets[a], targets[b]));
            }
        }

        private bool IsTarget(Collider collider) =>
            collider != null
            && collider.enabled
            && collider.gameObject.activeInHierarchy
            && (layerMask.value & (1 << collider.gameObject.layer)) != 0
            && (includeTriggers || collider.isTrigger == false);

        private static bool ShareRigidbody(Collider a, Collider b) =>
            a.attachedRigidbody != null && a.attachedRigidbody == b.attachedRigidbody;

        #endregion

        #region Drawing

        private void DrawAll(ILineSink sink)
        {
            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i] == null) continue;

                var isOverlapped = highlightOverlaps && overlappedIndices.Contains(i);
                BoxGeometry.GetCorners(bounds[i], corners);
                BoxGeometry.DrawWireframe(sink, corners, isOverlapped ? overlapColor : aabbColor);

                if (drawObb && BoxGeometry.TryGetColliderWorldCorners(targets[i], corners))
                    BoxGeometry.DrawWireframe(sink, corners, obbColor);
            }
        }

        private void OnEndCameraRendering(ScriptableRenderContext _, Camera camera) => DrawGameView(camera);

        private void DrawGameView(Camera camera)
        {
            if (drawInGameView == false || camera.cameraType != CameraType.Game || targets.Count == 0) return;

            var material = LineMaterial;
            material.SetInt("_ZTest", (int)(alwaysOnTop ? CompareFunction.Always : CompareFunction.LessEqual));
            material.SetPass(0);

            GL.PushMatrix();
            GL.LoadProjectionMatrix(camera.projectionMatrix);
            GL.modelview = camera.worldToCameraMatrix;
            GL.Begin(GL.LINES);
            DrawAll(glSink);
            GL.End();
            GL.PopMatrix();
        }

        private void OnDrawGizmos()
        {
            if (drawInSceneView == false) return;
            if (Application.isPlaying == false) RefreshSnapshot(true); // 에디트 모드에서는 LateUpdate가 돌지 않음
            DrawAll(gizmosSink);
        }

        private static Material CreateLineMaterial()
        {
            var material = new Material(Shader.Find("Hidden/Internal-Colored"))
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_Cull", (int)CullMode.Off);
            material.SetInt("_ZWrite", 0);
            return material;
        }

        #endregion
    }
}