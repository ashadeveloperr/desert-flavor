using System;
using UnityEngine;

namespace Study3D.Transforms
{
    public class DirectionGizmo : MonoBehaviour
    {
        public float length;

        private void OnDrawGizmos()
        {
            DrawArrow(transform.forward, Color.blue);
            DrawArrow(transform.right, Color.red);
            DrawArrow(transform.up, Color.green);
        }

        private void DrawArrow(Vector3 direction, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawRay(transform.position, direction * length);
            Vector3 end = transform.position + direction * length;
            Gizmos.DrawSphere(end, length * 0.05f);
        }
    }
}