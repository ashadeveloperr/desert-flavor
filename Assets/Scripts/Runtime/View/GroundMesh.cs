using System;
using System.Collections.Generic;
using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 높이가 있는 육각형 메시(pointy-top, 꼭짓점 방향은 <see cref="WorldVisuals.HexMesh"/>와 같음).
    /// 맵 타일의 굴곡 있는 모래 바닥과, 그 위를 따라 덮는 표시(건설 가능·마우스 강조·건설 예약)가 함께 쓴다.
    /// 좌표는 타일 반지름 1 단위이고 원점이 타일 가운데다. 쓰는 쪽이 오브젝트 크기를 타일 크기로 맞춘다.
    /// </summary>
    public static class GroundMesh
    {
        /// <summary>
        /// 반지름 <paramref name="extent"/>의 육각형을 꼭짓점-가운데 삼각형 6개로 나누고 각각을 <paramref name="rings"/>² 개의 작은 삼각형으로 쪼갠다.
        /// 꼭짓점 높이 = <paramref name="height"/>(가운데 기준 위치), UV = <paramref name="uv"/>(같은 위치, null이면 0). 정점 색은 흰색(Sprites/Default용).
        /// </summary>
        public static Mesh Build(string name, float extent, int rings, Func<Vector2, float> height, Func<Vector2, Vector2> uv = null)
        {
            var index = new Dictionary<Vector2Int, int>();
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            int Vertex(Vector2 p)
            {
                var key = Vector2Int.RoundToInt(p * 10000f);
                if (index.TryGetValue(key, out int i)) return i;
                i = vertices.Count;
                index[key] = i;
                vertices.Add(new Vector3(p.x, height(p), p.y));
                uvs.Add(uv != null ? uv(p) : Vector2.zero);
                return i;
            }

            for (int side = 0; side < 6; side++)
            {
                Vector2 a = Corner(side) * extent, b = Corner(side + 1) * extent;
                Vector2 At(int i, int j) => (a * i + b * j) / rings; // i + j ≤ rings
                for (int i = 0; i < rings; i++)
                {
                    for (int j = 0; j < rings - i; j++)
                    {
                        // 감기는 위에서 볼 때 시계 방향(Unity 앞면).
                        triangles.Add(Vertex(At(i, j)));
                        triangles.Add(Vertex(At(i + 1, j)));
                        triangles.Add(Vertex(At(i, j + 1)));
                        if (j < rings - i - 1)
                        {
                            triangles.Add(Vertex(At(i + 1, j)));
                            triangles.Add(Vertex(At(i + 1, j + 1)));
                            triangles.Add(Vertex(At(i, j + 1)));
                        }
                    }
                }
            }

            var colors = new Color[vertices.Count];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.colors = colors;
            mesh.SetTriangles(triangles, 0);
            FaceUp(mesh);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>육각형 안에서 가장자리까지의 상대 거리(가운데 0, 변 위 1). 가장자리 쪽으로 높이를 0에 붙일 때 쓴다.</summary>
        public static float HexDistance(Vector2 local)
        {
            float d = 0f;
            for (int i = 0; i < 3; i++)
            {
                float angle = Mathf.PI / 6f + Mathf.PI / 3f * i; // 변의 바깥 방향
                d = Mathf.Max(d, Mathf.Abs(local.x * Mathf.Sin(angle) + local.y * Mathf.Cos(angle)));
            }
            return d / (Mathf.Sqrt(3f) * 0.5f);
        }

        private static Vector2 Corner(int i)
        {
            float angle = Mathf.PI / 3f * (i % 6); // 북쪽에서 시계 방향 (HexLayout과 같음)
            return new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
        }

        /// <summary>모든 삼각형이 위를 보게 감기 방향을 맞춘다.</summary>
        private static void FaceUp(Mesh mesh)
        {
            Vector3[] v = mesh.vertices;
            int[] t = mesh.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 n = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (n.y < 0) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
            }
            mesh.triangles = t;
        }
    }
}
