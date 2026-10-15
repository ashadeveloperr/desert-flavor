using UnityEngine;

namespace Study3D.ThreeDPhysics
{
    public interface ILineSink
    {
        void DrawLine(Vector3 from, Vector3 to, Color color);
    }

    public sealed class GizmosLineSink : ILineSink
    {
        public void DrawLine(Vector3 from, Vector3 to, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawLine(from, to);
        }
    }

    public sealed class GLLineSink : ILineSink
    {
        public void DrawLine(Vector3 from, Vector3 to, Color color)
        {
            GL.Color(color);
            GL.Vertex(from);
            GL.Vertex(to);
        }
    }
}