using System;
using System.Collections.Generic;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;
using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 녹지(결정 SG4·GR4): 물이 흐르는 수로에 닿은 사막 칸의 '푸름'(0~1)이 정산 1시간마다 목표(그 수로의 부하율)로 다가가고,
    /// 푸름만큼 풀 덤불이 하나씩 돋아 자라며, 더 푸르러지면 야자수가 선다. 물이 끊기면 천천히 시들어 사라진다.
    /// 시설이 있는 칸에서는 야자수와 건물 자리(가운데)의 풀은 숨기고 둘레의 풀만 남긴다. 타일 색은 바꾸지 않는다.
    /// </summary>
    public sealed partial class SettlementView
    {
        [Header("녹지")]
        [Tooltip("정산 1시간마다 목표 푸름에 다가가는 비율(작을수록 천천히 자람)")]
        [Range(0.01f, 1f)]
        [SerializeField] private float greeningRate = 0.08f;
        [Tooltip("한 칸의 풀 덤불 수와 야자수 수(빈 칸 기준)")]
        [SerializeField, Range(0, 16)] private int grassPerTile = 8;
        [SerializeField, Range(0, 4)] private int palmsPerTile = 2;
        [Tooltip("첫 야자수가 돋는 푸름. 다음 야자수는 이보다 조금 더 푸를 때")]
        [Range(0f, 1f)]
        [SerializeField] private float palmGreenness = 0.3f;
        [Tooltip("시설이 있는 칸에서 풀을 남기는 둘레(타일 반지름 1 기준, 이보다 안쪽은 건물 자리)")]
        [SerializeField] private float occupiedClearRadius = 0.8f;

        private readonly Dictionary<HexCoord, float> _green = new Dictionary<HexCoord, float>();
        private readonly Dictionary<HexCoord, List<Plant>> _plants = new Dictionary<HexCoord, List<Plant>>();
        private long _greenHour = -1;

        private sealed class Plant
        {
            public Transform Transform;
            public float Threshold, Radius, Scale;
            public bool Palm;
        }

        /// <summary>풀이나 야자수가 하나라도 보이는 칸 수(확인용).</summary>
        public int GreenTileCount { get; private set; }

        private void ResetGreenery()
        {
            _green.Clear();
            _plants.Clear(); // 오브젝트는 타일 뿌리와 함께 지워진다
            _greenHour = -1;
            GreenTileCount = 0;
        }

        /// <summary>정산 1시간마다 칸별 푸름을 갱신한다(SG4). 물이 끊기면 다시 0으로 돌아간다.</summary>
        private void UpdateGreening(Settlement s)
        {
            if (s.CurrentHour == _greenHour || s.LastWater == null) return;
            long steps = _greenHour < 0 ? 1 : Math.Min(24, s.CurrentHour - _greenHour);
            _greenHour = s.CurrentHour;

            var wet = new Dictionary<EdgeId, float>();
            foreach (Facility f in s.Facilities)
            {
                if (f.Kind != BuildingKinds.CanalEdge || f.State != FacilityState.Operating) continue;
                double load = ShownLoad(s.LastWater, f.Edge.Value);
                if (load <= 1e-6) continue;
                wet[f.Edge.Value] = Mathf.Clamp01((float)(load / Math.Max(1e-6, CanalCapacity(s, f))));
            }

            foreach (HexTile tile in _session.Map.Tiles)
            {
                if (tile.Terrain != TerrainType.Desert) continue;
                float target = 0f;
                foreach (EdgeId e in HexTopology.EdgesOf(tile.Coord))
                {
                    if (wet.TryGetValue(e, out float fill)) target = Mathf.Max(target, 0.45f + 0.55f * fill);
                }
                _green.TryGetValue(tile.Coord, out float g);
                for (long i = 0; i < steps; i++) g += (target - g) * greeningRate;
                if (Mathf.Abs(g - target) < 0.01f) g = target;
                _green[tile.Coord] = g;
            }
        }

        /// <summary>매 프레임: 칸마다 푸름과 시설 유무에 맞춰 풀·야자수를 보이고 키운다.</summary>
        private void ApplyGreenery(HashSet<HexCoord> occupied)
        {
            int green = 0;
            foreach (KeyValuePair<HexCoord, float> kv in _green)
            {
                if (kv.Value < 0.02f && !_plants.ContainsKey(kv.Key)) continue;
                if (!_plants.TryGetValue(kv.Key, out List<Plant> plants)) _plants[kv.Key] = plants = Sow(kv.Key);
                bool busy = occupied.Contains(kv.Key), any = false;
                foreach (Plant p in plants)
                {
                    float grown = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(p.Threshold, p.Threshold + 0.2f, kv.Value));
                    bool show = grown > 0.02f && !(busy && (p.Palm || p.Radius < occupiedClearRadius));
                    if (p.Transform.gameObject.activeSelf != show) p.Transform.gameObject.SetActive(show);
                    if (!show) continue;
                    p.Transform.localScale = Vector3.one * (p.Scale * grown);
                    any = true;
                }
                if (any) green++;
            }
            GreenTileCount = green;
        }

        /// <summary>
        /// 칸 하나의 풀·야자수 자리를 정한다(타일 좌표로 늘 같은 배치). 풀은 둘레까지 흩어지고 낮은 푸름부터 하나씩,
        /// 야자수는 안쪽에 <see cref="palmGreenness"/>부터.
        /// </summary>
        private List<Plant> Sow(HexCoord tile)
        {
            var random = new System.Random(TileSeed(tile, 11));
            var root = new GameObject($"Greenery {tile}").transform;
            root.SetParent(_natureRoot, false);
            root.position = _session.ToWorld(_session.Layout.TileCenter(tile));
            root.localScale = Vector3.one * Size;
            var plants = new List<Plant>();

            for (int i = 0; i < grassPerTile; i++)
            {
                float a = (float)random.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Lerp(0.2f, 0.86f, Mathf.Sqrt((float)random.NextDouble()));
                Vector2 at = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * r;
                if (i == 0) at = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 0.84f; // 첫 풀은 둘레(시설이 있어도 보이게)
                GameObject tuft = Decoration(root, "Grass", NatureModels.GrassTuft(random.Next(NatureModels.Variants)),
                    WorldVisuals.Solid(NatureModels.GrassColors[random.Next(NatureModels.GrassColors.Length)]), at, GroundHeight(tile, at), 1f);
                tuft.transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                plants.Add(new Plant
                {
                    Transform = tuft.transform, Threshold = 0.02f + 0.3f * i / Mathf.Max(1, grassPerTile),
                    Radius = at.magnitude, Scale = 0.9f + 0.5f * (float)random.NextDouble(),
                });
            }
            for (int i = 0; i < palmsPerTile; i++)
            {
                float a = (float)random.NextDouble() * Mathf.PI * 2f + i * Mathf.PI;
                Vector2 at = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * Mathf.Lerp(0.3f, 0.6f, (float)random.NextDouble());
                Transform palm = Palm(root, random.Next(NatureModels.Variants), at, GroundHeight(tile, at), 1f, (float)random.NextDouble() * 360f);
                plants.Add(new Plant
                {
                    Transform = palm, Threshold = palmGreenness + 0.12f * i, Radius = at.magnitude,
                    Scale = 0.85f + 0.3f * (float)random.NextDouble(), Palm = true,
                });
            }
            foreach (Plant p in plants) p.Transform.gameObject.SetActive(false);
            return plants;
        }
    }
}
