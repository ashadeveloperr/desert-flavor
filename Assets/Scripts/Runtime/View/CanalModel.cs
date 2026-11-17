using System.Collections.Generic;
using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 수로 하나의 표시(결정 CN1): 변을 따라 판 물길. 바닥(끝이 둥근 띠) + 양옆 둑(가운데가 높고 양 끝으로 낮아져 꼭짓점에서 이웃 수로와 이어짐)
    /// + 물 수면(물이 많을수록 높고 진함) + 수문(닫힌 수로에만, 물길을 가로지르는 판자와 기둥 둘).
    /// 좌표는 타일 반지름 1 단위, 물길 방향이 +z, 가운데가 원점. 색·상태는 <see cref="SettlementView"/>가 <see cref="Apply"/>로 넣는다.
    /// </summary>
    public sealed class CanalModel : MonoBehaviour
    {
        private const int CapSegments = 8;
        private static readonly Dictionary<(int, int, string), Mesh> Meshes = new Dictionary<(int, int, string), Mesh>();

        private MeshFilter _bed, _banks, _water;
        private GameObject _gate;
        private MeshRenderer[] _gateParts;
        private float _length, _width = -1f;
        private bool _spur;

        /// <summary>
        /// 점 <paramref name="a"/>→<paramref name="b"/>(월드) 사이의 수로를 만든다. 크기 = <paramref name="tileSize"/>.
        /// <paramref name="spur"/>면 시설로 들어가는 지류: 둑이 <paramref name="a"/>(꼭짓점) 쪽에서만 좁아지고 <paramref name="b"/> 쪽(시설 밑)은 그대로
        /// (짧은 물길의 둑이 양 끝에서 좁아지면 쐐기처럼 보인다).
        /// </summary>
        public static CanalModel Build(string name, Transform parent, Vector3 a, Vector3 b, float tileSize, bool spur = false)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = (a + b) * 0.5f;
            root.transform.rotation = Quaternion.LookRotation(b - a, Vector3.up);
            root.transform.localScale = Vector3.one * tileSize;
            CanalModel m = root.AddComponent<CanalModel>();
            m._length = Vector3.Distance(a, b) / tileSize;
            m._bed = m.Part("Bed");
            m._banks = m.Part("Banks");
            m._spur = spur;
            m._water = m.Part("Water");
            m._water.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m.BuildGate();
            return m;
        }

        /// <summary>물이 보이는지(확인용).</summary>
        public bool WaterVisible => _water.gameObject.activeSelf;
        public bool GateVisible => _gate.activeSelf;

        /// <summary>
        /// 상태를 입힌다. <paramref name="width"/> = 물길 폭(타일 단위), <paramref name="water"/>이 null이면 물 없음(마른 바닥),
        /// 있으면 그 색으로 높이 <paramref name="waterLevel"/>(0 = 바닥 바로 위, 1 = 둑 바로 아래)에 수면. <paramref name="gate"/>면 수문을 보인다.
        /// </summary>
        public void Apply(float width, Color bank, Color bed, Color? water, float waterLevel, bool gate, Color gateColor)
        {
            if (!Mathf.Approximately(width, _width))
            {
                _width = width;
                _bed.sharedMesh = Cached("bed", width, () => Stadium(width * 0.5f * 0.82f, 0.006f));
                _water.sharedMesh = Cached("water", width, () => Stadium(width * 0.5f * 0.74f, 0f));
                _banks.sharedMesh = Cached(_spur ? "spur banks" : "banks", width, () => Banks(width * 0.5f));
                _gate.transform.localScale = new Vector3(width / 0.14f, 1f, 1f);
            }
            _banks.GetComponent<MeshRenderer>().sharedMaterial = WorldVisuals.Solid(bank);
            _bed.GetComponent<MeshRenderer>().sharedMaterial = WorldVisuals.Solid(bed);
            bool wet = water.HasValue;
            if (_water.gameObject.activeSelf != wet) _water.gameObject.SetActive(wet);
            if (wet)
            {
                _water.GetComponent<MeshRenderer>().sharedMaterial = WorldVisuals.Tinted(NatureModels.Water, water.Value);
                _water.transform.localPosition = new Vector3(0f, Mathf.Lerp(0.011f, 0.026f, Mathf.Clamp01(waterLevel)), 0f);
            }
            if (_gate.activeSelf != gate) _gate.SetActive(gate);
            if (gate) foreach (MeshRenderer r in _gateParts) r.sharedMaterial = WorldVisuals.Solid(r.name == "Plank" ? gateColor : Color.Lerp(gateColor, Color.black, 0.45f));
        }

        private MeshFilter Part(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshRenderer>();
            return go.AddComponent<MeshFilter>();
        }

        // 수문: 물길 가운데를 가로지르는 판자 + 양쪽 둑 위 기둥 둘(폭 0.14 기준으로 만들고 루트 x 크기로 폭을 맞춤).
        private void BuildGate()
        {
            _gate = new GameObject("Gate");
            _gate.transform.SetParent(transform, false);
            var parts = new List<MeshRenderer>();
            void Add(string name, Vector3 at, Vector3 size)
            {
                GameObject p = WorldVisuals.Primitive(PrimitiveType.Cube, name, _gate.transform, Color.white);
                p.transform.localPosition = at;
                p.transform.localScale = size;
                parts.Add(p.GetComponent<MeshRenderer>());
            }
            Add("Plank", new Vector3(0f, 0.035f, 0f), new Vector3(0.15f, 0.06f, 0.025f));
            Add("Post", new Vector3(-0.085f, 0.05f, 0f), new Vector3(0.022f, 0.1f, 0.03f));
            Add("Post", new Vector3(0.085f, 0.05f, 0f), new Vector3(0.022f, 0.1f, 0.03f));
            _gateParts = parts.ToArray();
            _gate.SetActive(false);
        }

        private Mesh Cached(string part, float width, System.Func<Mesh> build)
        {
            var key = (Mathf.RoundToInt(_length * 1000f), Mathf.RoundToInt(width * 1000f), part);
            if (Meshes.TryGetValue(key, out Mesh m) && m != null) return m;
            return Meshes[key] = build();
        }

        /// <summary>끝이 반원인 납작한 띠(반지름 <paramref name="r"/>, 높이 <paramref name="y"/>). 볼록해서 가운데에서 부채꼴로 채운다.</summary>
        private Mesh Stadium(float r, float y)
        {
            List<Vector2> outline = Outline(r);
            var vertices = new List<Vector3> { new Vector3(0f, y, 0f) };
            var triangles = new List<int>();
            foreach (Vector2 p in outline) vertices.Add(new Vector3(p.x, y, p.y));
            for (int i = 0; i < outline.Count; i++) triangles.AddRange(new[] { 0, 1 + (i + 1) % outline.Count, 1 + i });
            return Finish("Canal Strip", vertices, triangles);
        }

        /// <summary>
        /// 둑: 물길 양옆을 따라 안쪽 가장자리 → 마루 → 바깥 가장자리 단면을 잇는다. 양 끝 반지름 1.5배 구간에서 높이와 폭이 함께 줄어
        /// 안쪽 가장자리로 사라진다(꼭짓점에서 이웃 수로의 물길을 막거나 납작한 띠로 삐져나오지 않게).
        /// </summary>
        private Mesh Banks(float r)
        {
            const int steps = 12;
            float half = _length * 0.5f;
            (float x, float y)[] profile = { (r * 0.72f, 0.004f), (r * 0.98f, 0.05f), (r * 1.4f, 0f) };
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (float side in new[] { -1f, 1f })
            {
                int start = vertices.Count;
                for (int s = 0; s <= steps; s++)
                {
                    float z = Mathf.Lerp(-half, half, s / (float)steps);
                    float taper = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(half, half - r * 1.5f, _spur ? -z : Mathf.Abs(z)));
                    foreach ((float x, float y) in profile)
                        vertices.Add(new Vector3(side * (profile[0].x + (x - profile[0].x) * taper), y * taper, z));
                }
                for (int s = 0; s < steps; s++)
                {
                    for (int k = 0; k < profile.Length - 1; k++)
                    {
                        int i = start + s * profile.Length + k, j = i + profile.Length;
                        triangles.AddRange(new[] { i, j, i + 1, i + 1, j, j + 1 }); // 감기는 Finish가 위를 보게 맞춘다
                    }
                }
            }
            return Finish("Canal Banks", vertices, triangles);
        }

        /// <summary>물길 둘레(시계 방향, 위에서 볼 때): +z 끝 반원 → −z 끝 반원.</summary>
        private List<Vector2> Outline(float r)
        {
            float half = _length * 0.5f;
            var points = new List<Vector2>();
            for (int i = 0; i <= CapSegments; i++)
            {
                float a = Mathf.PI * i / CapSegments;
                points.Add(new Vector2(r * Mathf.Cos(a), half + r * Mathf.Sin(a)));
            }
            for (int i = 0; i <= CapSegments; i++)
            {
                float a = Mathf.PI + Mathf.PI * i / CapSegments;
                points.Add(new Vector2(r * Mathf.Cos(a), -half + r * Mathf.Sin(a)));
            }
            return points;
        }

        private static Mesh Finish(string name, List<Vector3> vertices, List<int> triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            // 감기 방향과 상관없이 위를 보게.
            int[] t = mesh.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 n = Vector3.Cross(vertices[t[i + 1]] - vertices[t[i]], vertices[t[i + 2]] - vertices[t[i]]);
                if (n.y < 0) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
            }
            mesh.triangles = t;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
