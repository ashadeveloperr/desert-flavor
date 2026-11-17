using System;
using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;
using DesertSettlement.Core.Water;
using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 수로 표시(결정 CN1, 모양은 <see cref="CanalModel"/>): 둑으로 둘러싼 물길.
    /// 물이 흐르면 수면이 보이고, 부하/용량이 클수록 수면이 높고 진한 물색이며 반짝임 띠가 맵을 가로질러 지나간다('물이 있다'는 표시, M4).
    /// 물이 없으면 마른 바닥, 닫힌 수로는 마른 바닥 + 수문(붉은 판자), 공사·철거 중은 시설과 같은 상태 색. 레벨마다 50%씩 넓어진다.
    /// 지류(결정 CN2): 시설이 실제로 물을 받거나(취수 꼭짓점) 내보낸(우물·물탱크) 꼭짓점에서 시설 쪽으로 짧은 물길이 뻗어 시설 밑으로 들어간다.
    /// 물은 Core 규칙대로 시설 칸의 꼭짓점에서 오가므로 그 자리에 그린다. 꼭짓점에서 칸 가운데 쪽은 맞은편 수로의 연장선이라 ㅗ·ㅡ 모양이 된다.
    /// </summary>
    public sealed partial class SettlementView
    {
        [Header("수로")]
        [Tooltip("물길 폭(타일 크기 대비, L1). 레벨마다 50%씩 넓어진다")]
        [SerializeField] private float canalThickness = 0.14f;
        [Tooltip("둑 색과 물이 없을 때 바닥 색")]
        [SerializeField] private Color canalBank = new Color(0.6f, 0.49f, 0.37f);
        [SerializeField] private Color canalDryColor = new Color(0.38f, 0.31f, 0.23f);
        [Tooltip("물이 적을 때 → 가득 찼을 때 수면 색")]
        [SerializeField] private Color canalShallow = new Color(0.42f, 0.68f, 0.76f);
        [SerializeField] private Color canalDeep = new Color(0.12f, 0.42f, 0.66f);
        [Tooltip("닫힌 수로의 수문 판자 색")]
        [SerializeField] private Color canalClosed = new Color(0.9f, 0.15f, 0.15f);
        [Tooltip("물이 흐르는 수로의 반짝임 세기(0이면 끔)")]
        [Range(0f, 0.5f)]
        [SerializeField] private float canalShimmer = 0.18f;
        [Tooltip("시설로 들어가는 지류 길이(타일 반지름 1 기준, 꼭짓점에서 시설 가운데 쪽으로)")]
        [SerializeField] private float canalSpurLength = 0.42f;

        private readonly Dictionary<(int, VertexId), CanalModel> _spurs = new Dictionary<(int, VertexId), CanalModel>();
        private readonly Dictionary<VertexId, Color?> _vertexWater = new Dictionary<VertexId, Color?>();

        /// <summary>지금 보이는 지류 수(확인용).</summary>
        public int CanalSpurCount => _spurs.Count;

        private GameObject BuildCanal(EdgeId edge)
        {
            HexLayout layout = _session.Layout;
            Vector3 a = _session.ToWorld(HexTopology.PositionOf(edge.VertexA, layout));
            Vector3 b = _session.ToWorld(HexTopology.PositionOf(edge.VertexB, layout));
            return CanalModel.Build("Canal " + edge, _facilitiesRoot, a, b, Size).gameObject;
        }

        private void UpdateCanal(Settlement s, Facility f, CanalModel canal, float progress)
        {
            Color? shown = CanalWater(s, f, canal);
            canal.Apply(CanalWidth(f.Level), Tint(canalBank, f.State, progress), Tint(canalDryColor, f.State, progress), shown,
                0.25f + 0.75f * CanalFill(s, f), f.State == FacilityState.Closed, Color.Lerp(new Color(0.45f, 0.32f, 0.2f), canalClosed, 0.7f));
            if (f.State == FacilityState.Constructing) return;
            foreach (VertexId v in new[] { f.Edge.Value.VertexA, f.Edge.Value.VertexB })
            {
                // 꼭짓점의 물: 그 꼭짓점에 닿은 수로 중 물이 보이는 것(여럿이면 먼저 본 것).
                if (!_vertexWater.TryGetValue(v, out Color? w) || (!w.HasValue && shown.HasValue)) _vertexWater[v] = shown;
            }
        }

        private float CanalWidth(int level) => canalThickness * (1f + 0.5f * (level - 1));

        private static float CanalFill(Settlement s, Facility f)
        {
            double load = f.State == FacilityState.Operating && s.LastWater != null ? ShownLoad(s.LastWater, f.Edge.Value) : 0;
            return load > 1e-6 ? Mathf.Clamp01((float)(load / Math.Max(1e-6, CanalCapacity(s, f)))) : 0f;
        }

        /// <summary>이 수로 수면 색(물이 없으면 null). 반짝임 띠는 위치에 따라 시간차를 두고 지나간다.</summary>
        private Color? CanalWater(Settlement s, Facility f, CanalModel canal)
        {
            double load = f.State == FacilityState.Operating && s.LastWater != null ? ShownLoad(s.LastWater, f.Edge.Value) : 0;
            if (load <= 1e-6) return null;
            float fill = CanalFill(s, f);
            Vector3 p = canal.transform.position / Mathf.Max(0.01f, Size);
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f - (p.x + p.z) * 1.7f);
            return Color.Lerp(Color.Lerp(canalShallow, canalDeep, fill), Color.white, canalShimmer * wave * (0.4f + 0.6f * fill));
        }

        /// <summary>
        /// 매 프레임(수로 갱신 뒤): 시설이 물을 주고받은 꼭짓점마다 지류를 둔다(CN2). 물이 끊기거나 시설이 사라지면 지류도 지운다.
        /// </summary>
        private void UpdateSpurs(Settlement s)
        {
            var wanted = new HashSet<(int, VertexId)>();
            if (s.LastWater != null)
            {
                foreach (Facility f in s.Facilities)
                {
                    if (f.Edge.HasValue) continue;
                    foreach (VertexId v in WaterVertices(s, f))
                        if (_vertexWater.ContainsKey(v)) wanted.Add((f.Id, v));
                }
            }
            bool changed = false;
            foreach ((int, VertexId) key in _spurs.Keys.Where(k => !wanted.Contains(k)).ToList())
            {
                Destroy(_spurs[key].gameObject);
                _spurs.Remove(key);
                changed = true;
            }
            changed |= wanted.Any(k => !_spurs.ContainsKey(k));
            foreach ((int id, VertexId v) in wanted)
            {
                if (!_spurs.TryGetValue((id, v), out CanalModel spur))
                {
                    Facility f = s.Facilities.First(x => x.Id == id);
                    Vector3 from = _session.ToWorld(HexTopology.PositionOf(v, _session.Layout));
                    Vector3 center = _session.ToWorld(_session.Layout.TileCenter(f.Tile));
                    Vector3 to = from + (center - from).normalized * canalSpurLength * Size;
                    _spurs[(id, v)] = spur = CanalModel.Build($"Canal Spur {id} {v}", _natureRoot, from, to, Size, spur: true);
                }
                Color? water = _vertexWater[v];
                spur.Apply(CanalWidth(1), canalBank, canalDryColor, water, water.HasValue ? 0.6f : 0f, false, Color.clear);
            }
            _vertexWater.Clear();
            if (changed) CarveSpurs(s);
        }

        /// <summary>지류가 지나는 칸마다 모래를 파낸다(안 그러면 칸 안쪽 모래 굴곡에 지류 물이 묻힌다). 지류가 없어진 칸은 되돌린다.</summary>
        private void CarveSpurs(Settlement s)
        {
            var byTile = new Dictionary<HexCoord, List<(Vector2, Vector2)>>();
            foreach ((int id, VertexId v) in _spurs.Keys)
            {
                Facility f = s.Facilities.FirstOrDefault(x => x.Id == id);
                if (f == null) continue;
                HexPoint c = _session.Layout.TileCenter(f.Tile), p = HexTopology.PositionOf(v, _session.Layout);
                var corner = new Vector2((float)(p.X - c.X), (float)(p.Z - c.Z)) / Size;
                if (!byTile.TryGetValue(f.Tile, out var list)) byTile[f.Tile] = list = new List<(Vector2, Vector2)>();
                list.Add((corner, corner - corner.normalized * canalSpurLength));
            }
            foreach (HexCoord tile in _carves.Keys.Concat(byTile.Keys).Distinct().ToList())
                SetCarves(tile, byTile.TryGetValue(tile, out var segments) ? segments : new List<(Vector2, Vector2)>());
        }

        /// <summary>정착지가 바뀌면(지류 오브젝트는 자연물 뿌리와 함께 지워진다).</summary>
        private void ResetSpurs()
        {
            _spurs.Clear();
            _vertexWater.Clear();
        }

        /// <summary>
        /// 시설이 실제로 물을 주고받은 꼭짓점(CN2): 물을 받은 시설·물탱크는 이번 배분의 취수 꼭짓점(Core가 정한 것),
        /// 우물·물탱크 방출은 그 칸 꼭짓점 중 물이 보이는 수로가 닿은 곳.
        /// </summary>
        private IEnumerable<VertexId> WaterVertices(Settlement s, Facility f)
        {
            foreach (string id in new[] { WaterIds.Of(f), WaterIds.Tank(f.Id) })
            {
                if (s.LastWater.Consumers.TryGetValue(id, out WaterConsumerResult r))
                    foreach (WaterIntakeFlow intake in r.Intakes)
                        if (intake.Delivered > 1e-6) yield return intake.IntakeVertex;
                if (s.LastWater.SourceReleased.TryGetValue(id, out double released) && released > 1e-6)
                    foreach (VertexId v in HexTopology.VerticesOf(f.Tile))
                        if (_vertexWater.TryGetValue(v, out Color? w) && w.HasValue) yield return v;
            }
        }

        /// <summary>건설 예약된 수로: 변 위의 얇은 막대(<paramref name="lengthRatio"/> = 변 길이 대비).</summary>
        private GameObject CanalBar(EdgeId edge, Transform parent, Color color, float height, float lengthRatio)
        {
            HexLayout layout = _session.Layout;
            Vector3 a = _session.ToWorld(HexTopology.PositionOf(edge.VertexA, layout));
            Vector3 b = _session.ToWorld(HexTopology.PositionOf(edge.VertexB, layout));
            GameObject bar = WorldVisuals.Primitive(PrimitiveType.Cube, "Canal " + edge, parent, color);
            bar.transform.position = (a + b) * 0.5f + Vector3.up * height * 0.5f;
            bar.transform.rotation = Quaternion.LookRotation(b - a, Vector3.up);
            bar.transform.localScale = new Vector3(Size * canalThickness, height, Vector3.Distance(a, b) * lengthRatio);
            return bar;
        }

        /// <summary>수로에 보이는 흐름 = 변을 지난 물 + 이 수로가 이어 준 우물 옆 직접 취수(WD1, 용량은 안 씀).</summary>
        private static double ShownLoad(WaterAllocation water, EdgeId edge) =>
            (water.EdgeLoads.TryGetValue(edge, out double load) ? load : 0) + (water.DirectIntakeLoads.TryGetValue(edge, out double direct) ? direct : 0);

        private static double CanalCapacity(Settlement s, Facility f) =>
            s.Rules.Config.GetLevel(BuildingKinds.CanalEdge, f.Level)?.CapacityPerHour ?? 100.0;
    }
}
