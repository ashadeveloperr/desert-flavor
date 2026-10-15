using UnityEngine;

namespace Study3D.ThreeDPhysics
{
    public static class BoxGeometry
    {
        private static readonly Vector3[] cornerSigns =
        {
            new(-1f, -1f, -1f), new(1f, -1f, -1f), new(1f, -1f, 1f), new(-1f, -1f, 1f),
            new(-1f,  1f, -1f), new(1f,  1f, -1f), new(1f,  1f, 1f), new(-1f,  1f, 1f)
        };

        private static readonly (int From, int To)[] edges =
        {
            (0, 1), (1, 2), (2, 3), (3, 0),
            (4, 5), (5, 6), (6, 7), (7, 4),
            (0, 4), (1, 5), (2, 6), (3, 7)
        };

        /// <summary>월드 축 정렬 박스(AABB)의 꼭짓점 8개.</summary>
        public static void GetCorners(Bounds box, Vector3[] result)
        {
            for (var i = 0; i < cornerSigns.Length; i++)
                result[i] = box.center + Vector3.Scale(box.extents, cornerSigns[i]);
        }

        public static bool TryGetColliderWorldCorners(Collider collider, Vector3[] result)
        {
            if (TryGetLocalBox(collider, out var center, out var extents) == false) return false;

            var transform = collider.transform;
            for (var i = 0; i < cornerSigns.Length; i++)
                result[i] = transform.TransformPoint(center + Vector3.Scale(extents, cornerSigns[i]));
            return true;
        }

        public static void DrawWireframe(ILineSink sink, Vector3[] corners, Color color)
        {
            foreach (var (from, to) in edges) sink.DrawLine(corners[from], corners[to], color);
        }

        private static bool TryGetLocalBox(Collider collider, out Vector3 center, out Vector3 extents)
        {
            switch (collider)
            {
                case BoxCollider box:
                    center = box.center;
                    extents = box.size * 0.5f;
                    return true;

                case SphereCollider sphere:
                    center = sphere.center;
                    extents = Vector3.one * sphere.radius;
                    return true;

                case CapsuleCollider capsule:
                    center = capsule.center;
                    extents = Vector3.one * capsule.radius;
                    extents[capsule.direction] = Mathf.Max(capsule.height * 0.5f, capsule.radius);
                    return true;

                case MeshCollider { sharedMesh: not null } mesh:
                    center = mesh.sharedMesh.bounds.center;
                    extents = mesh.sharedMesh.bounds.extents;
                    return true;

                default:
                    center = default;
                    extents = default;
                    return false;
            }
        }
    }
}