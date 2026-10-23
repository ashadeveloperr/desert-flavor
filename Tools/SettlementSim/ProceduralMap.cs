using System;
using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Hex;
using DesertSettlement.Core.Progression;

namespace SettlementSim
{
    // 절차 맵 생성기 프로토타입. Core의 헥스 타입만 쓰므로 검증 후 Core(Progression)로 옮길 수 있다.
    // 규칙 출처: Docs/implementation_notes.md "절차 맵" 항목(2026-10-23 사용자 정의 + 제안 확정).

    /// <summary>
    /// 난이도 곡선의 조절값. D = 0(쉬움)과 D = 1(목표: 숙련 플레이어가 겨우 흑자) 두 끝값을 정하고,
    /// D &gt; 1은 같은 기울기로 계속 늘린다(상한 없음, 2026-10-23 확정). 각 값에는 하한·상한이 있다.
    /// 우물·광맥은 '반경 3 맵(37칸)당 개수'로 정해 맵 칸 수 비율만큼 늘린다.
    /// </summary>
    public sealed class DifficultyCurve
    {
        /// <summary>D = max(0, (보유 자금 − CashAtD0) / CashPerD). 상한 없음.</summary>
        public double CashAtD0 = 1000;
        public double CashPerD = 10500; // 조정: 숙련 봇이 6~7번째 정착지에서 D=1 (2026-10-23, -- tune E105)

        /// <summary>반경: D &lt; FirstRadiusStepD이면 3, 이후 RadiusStepD마다 +1, 최대 MaxRadius(HexMap 한도 7).</summary>
        public int BaseRadius = 3;
        public double FirstRadiusStepD = 1.0; // D<1은 반경 3: 반경이 커질 때 광맥 수가 늘어 순서가 뒤집히는 것을 막는다
        public double RadiusStepD = 1.0;
        public int MaxRadius = 7;

        /// <summary>37칸당 우물 수: D0 → D1, 이후 같은 기울기. 37칸당 하한 MinWellsPer37, 맵 전체 최소 1.</summary>
        public double WellsPer37AtD0 = 3, WellsPer37AtD1 = 1, MinWellsPer37 = 0.3;
        /// <summary>37칸당 광맥 수: D0 → D1, 이후 같은 기울기. 37칸당 하한 MinOresPer37, 맵 전체 최소 1.</summary>
        public double OresPer37AtD0 = 3, OresPer37AtD1 = 0.9, MinOresPer37 = 0.3;

        /// <summary>장애물 비율: D0 → D1, 이후 같은 기울기, 상한 MaxObstacleDensity(맵을 막지 않는 한도).</summary>
        public double ObstacleAtD0 = 0.15, ObstacleAtD1 = 0.30, MaxObstacleDensity = 0.40;

        /// <summary>가장 가까운 우물–광맥 거리 목표: D0 → D1, 이후 같은 기울기, 상한 = 반경.</summary>
        public double DistanceAtD0 = 2, DistanceAtD1 = 4;

        public DifficultyCurve Clone() => (DifficultyCurve)MemberwiseClone();
    }

    /// <summary>난이도 D(0 이상, 상한 없음)에서 정해지는 생성 수치.</summary>
    public sealed class DifficultyParams
    {
        public double D;
        public int Radius;
        public int WellCount;
        public int OreCount;
        public double ObstacleDensity;
        /// <summary>가장 가까운 우물–광맥 타일 거리 목표. 실제 값이 [T−1, T+1] 안이어야 한다.</summary>
        public int TargetWellOreDistance;

        /// <summary>시뮬레이터 기본 곡선(조정 결과). 바꿔 보려면 이 값을 고치거나 For(d, curve)에 다른 곡선을 넘긴다.</summary>
        public static DifficultyCurve DefaultCurve = new DifficultyCurve();

        public static double DifficultyFromCash(double cash, DifficultyCurve c = null)
        {
            c = c ?? DefaultCurve;
            return Math.Max(0, (cash - c.CashAtD0) / c.CashPerD);
        }

        public static DifficultyParams FromCash(double cash, DifficultyCurve c = null) => For(DifficultyFromCash(cash, c), c);

        /// <summary>우물·광맥 수 기준이 되는 맵 크기(반경 3 = 37칸).</summary>
        public const int BaseTiles = 37;

        /// <summary>반경 r 헥스 맵의 칸 수 = 3r(r+1)+1.</summary>
        public static int TileCount(int radius) => 3 * radius * (radius + 1) + 1;

        public static DifficultyParams For(double d, DifficultyCurve c = null)
        {
            c = c ?? DefaultCurve;
            d = Math.Max(0, d);
            int radius = d < c.FirstRadiusStepD ? c.BaseRadius
                : c.BaseRadius + 1 + (int)Math.Floor((d - c.FirstRadiusStepD) / c.RadiusStepD);
            radius = Math.Min(c.MaxRadius, radius);
            double scale = TileCount(radius) / (double)BaseTiles;
            return new DifficultyParams
            {
                D = d,
                Radius = radius,
                WellCount = Math.Max(1, Round(Math.Max(c.MinWellsPer37, Lerp(c.WellsPer37AtD0, c.WellsPer37AtD1, d)) * scale)),
                OreCount = Math.Max(1, Round(Math.Max(c.MinOresPer37, Lerp(c.OresPer37AtD0, c.OresPer37AtD1, d)) * scale)),
                ObstacleDensity = Math.Min(c.MaxObstacleDensity, Lerp(c.ObstacleAtD0, c.ObstacleAtD1, d)),
                TargetWellOreDistance = Math.Min(radius, Round(Lerp(c.DistanceAtD0, c.DistanceAtD1, d))),
            };
        }

        /// <summary>a(t=0)와 b(t=1)를 잇는 직선. t &gt; 1이면 같은 기울기로 연장된다.</summary>
        private static double Lerp(double a, double b, double t) => a + (b - a) * t;
        private static int Round(double x) => (int)Math.Round(x, MidpointRounding.AwayFromZero);

        public override string ToString() =>
            $"D {D:0.00}: 반경 {Radius}, 우물 {WellCount}, 광맥 {OreCount}, 장애물 {ObstacleDensity:P0}, 우물–광맥 거리 목표 {TargetWellOreDistance}";
    }

    /// <summary>생성 규칙의 확률·크기(사용자 규칙 + 제안 확정값).</summary>
    public sealed class GeneratorRules
    {
        public int FormationMinSize = 3;
        public int FormationMaxSize = 6;
        /// <summary>장애물 지형마다 산맥(선)일 확률. 나머지는 고원(덩어리).</summary>
        public double MountainChance = 0.5;
        /// <summary>산맥이 한 칸 나아갈 때 60° 꺾일 확률.</summary>
        public double MountainTurnChance = 0.25;
        /// <summary>맵마다 한 번: 독립된 한 칸 장애물이 생길 확률.</summary>
        public double IsolatedObstacleChance = 0.15;
        public double OreNearObstacleChance = 0.9;
        public double BedrockInObstacleChance = 0.9;
        /// <summary>BSP 구역의 최소 타일 수.</summary>
        public int MinLeafTiles = 5;
        /// <summary>우물 반경 이 거리 안에는 다른 우물이 없다.</summary>
        public int WellExclusionRadius = 2;
        /// <summary>우물 주변 6칸 중 최소 사막 칸 수(농장·경매장 자리).</summary>
        public int MinDesertAroundWell = 2;
        public int MinOreSpacing = 2;
        public int MinDesertTiles = 8;
        public int MaxAttempts = 300;
    }

    public enum FormationKind { Mountain, Plateau }

    public sealed class GeneratedMap
    {
        public MapDefinition Map;
        public int Seed;
        public int Attempts;
        public DifficultyParams Params;
        public List<(FormationKind kind, List<HexCoord> tiles)> Formations = new List<(FormationKind, List<HexCoord>)>();
        public HexCoord? IsolatedObstacle;
        public int BspLeaves;
        public int NearestWellOreDistance;
        /// <summary>가장 짧은 우물 → 광맥 수로 경로(변 개수).</summary>
        public int ShortestWellOreCanalPath;
        public int OresNearObstacle, OreTotal, BedrocksInObstacle, BedrockTotal;
    }

    /// <summary>
    /// BSP 기반 헥스 절차 맵 생성기.
    ///
    /// BSP를 쓰는 이유(2026-10-23 확정): 장애물 지형(산맥·고원)이 서로 이어지면 맵을 가로막는 장벽이 생긴다.
    /// 맵을 cube 좌표 축(q, r, s) 직선으로 재귀 분할해 구역(leaf)을 만들고 **구역마다 장애물 지형을 최대 하나**만
    /// 그 구역 안에서 키우면, 지형끼리 섞이지 않고 구역 사이에 트인 땅이 남는다. 여기에 '다른 지형과 붙지 않기'와
    /// '장애물이 아닌 땅이 하나로 이어져 있기' 검사를 더해 가로막힘을 확실히 막는다.
    ///
    /// 순서: BSP → 장애물 지형 → (15%) 독립 장애물 → 가로막힘 검사 → 암반 → 광맥 → 우물 → 풀 수 있는지 검사.
    /// 어느 단계든 실패하면 같은 시드 흐름에서 처음부터 다시 만든다(최대 MaxAttempts).
    /// </summary>
    public static class ProceduralMapGenerator
    {
        public static GeneratedMap Generate(DifficultyParams p, GeneratorRules rules, int seed)
        {
            var rng = new Random(seed);
            for (int attempt = 1; attempt <= rules.MaxAttempts; attempt++)
            {
                GeneratedMap result = TryGenerate(p, rules, rng);
                if (result == null) continue;
                result.Seed = seed;
                result.Attempts = attempt;
                result.Params = p;
                result.Map.Id = $"gen_d{p.D:0.00}_s{seed}";
                result.Map.Name = $"생성 맵 (난이도 {p.D:0.00}, 시드 {seed})";
                result.Map.Description = p.ToString();
                return result;
            }
            return null;
        }

        private static GeneratedMap TryGenerate(DifficultyParams p, GeneratorRules rules, Random rng)
        {
            List<HexCoord> all = HexCoord.Range(p.Radius).ToList();
            var inMap = new HashSet<HexCoord>(all);
            var terrain = all.ToDictionary(c => c, _ => TerrainType.Desert);
            var gen = new GeneratedMap();

            // 1) BSP 구역.
            int obstacleBudget = (int)Math.Round(p.ObstacleDensity * all.Count);
            int formationCount = Math.Max(1, (int)Math.Ceiling(obstacleBudget / ((rules.FormationMinSize + rules.FormationMaxSize) / 2.0)));
            List<List<HexCoord>> leaves = Bsp(all, formationCount, rules.MinLeafTiles, rng);
            gen.BspLeaves = leaves.Count;

            // 2) 구역마다 장애물 지형 하나.
            var owner = new Dictionary<HexCoord, int>(); // 장애물 칸 → 지형 번호
            int remaining = obstacleBudget;
            foreach (List<HexCoord> leaf in leaves.OrderBy(_ => rng.Next()))
            {
                if (remaining < rules.FormationMinSize) break;
                int size = Math.Min(remaining, rng.Next(rules.FormationMinSize, rules.FormationMaxSize + 1));
                FormationKind kind = rng.NextDouble() < rules.MountainChance ? FormationKind.Mountain : FormationKind.Plateau;
                int id = gen.Formations.Count;
                var leafSet = new HashSet<HexCoord>(leaf);
                bool Allowed(HexCoord c) => leafSet.Contains(c) && !owner.ContainsKey(c) &&
                                            Neighbors(c).All(n => !owner.TryGetValue(n, out int o) || o == id);
                List<HexCoord> tiles = kind == FormationKind.Mountain
                    ? GrowMountain(leaf, size, Allowed, rules.MountainTurnChance, rng)
                    : GrowPlateau(leaf, size, Allowed, rng);
                if (tiles.Count < 2) continue;
                foreach (HexCoord c in tiles)
                {
                    owner[c] = id;
                    terrain[c] = TerrainType.Obstacle;
                }
                gen.Formations.Add((kind, tiles));
                remaining -= tiles.Count;
            }

            // 3) 독립된 한 칸 장애물(맵마다 15%).
            if (rng.NextDouble() < rules.IsolatedObstacleChance)
            {
                List<HexCoord> spots = all.Where(c => terrain[c] == TerrainType.Desert && Neighbors(c).All(n => !owner.ContainsKey(n))).ToList();
                if (spots.Count > 0)
                {
                    HexCoord c = spots[rng.Next(spots.Count)];
                    terrain[c] = TerrainType.Obstacle;
                    owner[c] = -1;
                    gen.IsolatedObstacle = c;
                }
            }

            // 4) 가로막힘 검사: 장애물이 아닌 땅이 하나로 이어져 있어야 한다.
            if (!OpenLandConnected(all, inMap, terrain)) return null;

            // 5) 암반: 지형마다 90%는 지형 칸(트인 땅과 맞닿은 칸 우선)을 대신, 10%는 지형 옆 사막.
            foreach ((FormationKind _, List<HexCoord> tiles) in gen.Formations)
            {
                gen.BedrockTotal++;
                if (rng.NextDouble() < rules.BedrockInObstacleChance)
                {
                    List<HexCoord> edge = tiles.Where(c => Neighbors(c).Any(n => inMap.Contains(n) && terrain[n] != TerrainType.Obstacle)).ToList();
                    List<HexCoord> pool = edge.Count > 0 ? edge : tiles;
                    terrain[pool[rng.Next(pool.Count)]] = TerrainType.Bedrock;
                    gen.BedrocksInObstacle++;
                }
                else
                {
                    List<HexCoord> beside = tiles.SelectMany(Neighbors).Distinct()
                        .Where(n => inMap.Contains(n) && terrain[n] == TerrainType.Desert).ToList();
                    if (beside.Count == 0) { gen.BedrockTotal--; continue; }
                    terrain[beside[rng.Next(beside.Count)]] = TerrainType.Bedrock;
                }
            }
            if (gen.BedrockTotal == 0) return null; // 석재 없이는 진행 불가

            // 6) 광맥: 90%는 장애물 지형 바로 옆 사막, 10%는 장애물과 떨어진 사막. 광맥끼리 최소 간격.
            var ores = new List<HexCoord>();
            for (int i = 0; i < p.OreCount; i++)
            {
                bool near = rng.NextDouble() < rules.OreNearObstacleChance;
                List<HexCoord> pool = all.Where(c => terrain[c] == TerrainType.Desert &&
                                                     ores.All(o => o.DistanceTo(c) >= rules.MinOreSpacing) &&
                                                     Neighbors(c).Any(owner.ContainsKey) == near).ToList();
                if (pool.Count == 0) return null;
                HexCoord pick = pool[rng.Next(pool.Count)];
                terrain[pick] = TerrainType.OreVein;
                ores.Add(pick);
                if (near) gen.OresNearObstacle++;
            }
            gen.OreTotal = ores.Count;

            // 7) 우물: 사막 한 칸씩, 반경 2 안에 다른 우물 없음, 주변 사막 2칸 이상. 가장 가까운 우물–광맥 거리가 목표 범위 안.
            List<HexCoord> wells = null;
            int lo = Math.Max(1, p.TargetWellOreDistance - 1), hi = p.TargetWellOreDistance + 1;
            for (int tries = 0; tries < 40 && wells == null; tries++)
            {
                var placed = new List<HexCoord>();
                for (int i = 0; i < p.WellCount; i++)
                {
                    List<HexCoord> pool = all.Where(c => terrain[c] == TerrainType.Desert &&
                                                         placed.All(w => w.DistanceTo(c) > rules.WellExclusionRadius) &&
                                                         Neighbors(c).Count(n => inMap.Contains(n) && terrain[n] == TerrainType.Desert) >= rules.MinDesertAroundWell &&
                                                         ores.All(o => o.DistanceTo(c) >= lo)).ToList();
                    if (pool.Count == 0) break;
                    placed.Add(pool[rng.Next(pool.Count)]);
                }
                if (placed.Count < p.WellCount) continue;
                int nearest = placed.SelectMany(w => ores.Select(o => w.DistanceTo(o))).Min();
                if (nearest >= lo && nearest <= hi) wells = placed;
            }
            if (wells == null) return null;
            foreach (HexCoord w in wells) terrain[w] = TerrainType.WaterSource;
            gen.NearestWellOreDistance = wells.SelectMany(w => ores.Select(o => w.DistanceTo(o))).Min();

            if (all.Count(c => terrain[c] == TerrainType.Desert) < rules.MinDesertTiles) return null;

            var map = new MapDefinition
            {
                Radius = p.Radius,
                Terrain = all.Where(c => terrain[c] != TerrainType.Desert).OrderBy(c => c.R).ThenBy(c => c.Q)
                    .Select(c => new MapTerrainEntry { Q = c.Q, R = c.R, Type = terrain[c].ToString() }).ToList(),
            };

            // 8) 풀 수 있는지: 수로 규칙(양쪽 장애물 변 불가) 아래에서 우물 → 광맥, 우물 → 암반 경로가 있어야 한다.
            HexMap hex = map.CreateMap();
            var topology = new HexTopology(hex);
            int oreDist = CanalPaths.ShortestPathLength(topology, hex, wells, ores);
            int rockDist = CanalPaths.ShortestPathLength(topology, hex, wells,
                all.Where(c => terrain[c] == TerrainType.Bedrock).ToList());
            if (oreDist < 0 || rockDist < 0) return null;
            gen.ShortestWellOreCanalPath = oreDist;
            gen.Map = map;
            return gen;
        }

        // ───────────── BSP ─────────────

        private static int Axis(HexCoord c, int axis) => axis == 0 ? c.Q : axis == 1 ? c.R : c.S;

        /// <summary>가장 큰 구역부터 cube 축 직선으로 나눠 target개 구역을 만든다(못 나누면 그대로).</summary>
        private static List<List<HexCoord>> Bsp(List<HexCoord> tiles, int target, int minLeaf, Random rng)
        {
            var leaves = new List<List<HexCoord>> { tiles };
            while (leaves.Count < target)
            {
                List<HexCoord> largest = leaves.OrderByDescending(l => l.Count).First();
                List<HexCoord>[] split = Split(largest, minLeaf, rng);
                if (split == null) break;
                leaves.Remove(largest);
                leaves.AddRange(split);
            }
            return leaves;
        }

        private static List<HexCoord>[] Split(List<HexCoord> region, int minLeaf, Random rng)
        {
            foreach (int axis in new[] { 0, 1, 2 }.OrderBy(_ => rng.Next()))
            {
                List<int> values = region.Select(c => Axis(c, axis)).Distinct().OrderBy(v => v).ToList();
                var cuts = values.Skip(1)
                    .Where(t => region.Count(c => Axis(c, axis) < t) >= minLeaf && region.Count(c => Axis(c, axis) >= t) >= minLeaf)
                    .ToList();
                if (cuts.Count == 0) continue;
                int cut = cuts[rng.Next(cuts.Count)];
                return new[]
                {
                    region.Where(c => Axis(c, axis) < cut).ToList(),
                    region.Where(c => Axis(c, axis) >= cut).ToList(),
                };
            }
            return null;
        }

        // ───────────── 장애물 지형 ─────────────

        private static List<HexCoord> GrowMountain(List<HexCoord> leaf, int size, Func<HexCoord, bool> allowed, double turnChance, Random rng)
        {
            List<HexCoord> starts = leaf.Where(allowed).ToList();
            if (starts.Count == 0) return new List<HexCoord>();
            HexCoord cur = starts[rng.Next(starts.Count)];
            var tiles = new List<HexCoord> { cur };
            int dir = rng.Next(6);
            while (tiles.Count < size)
            {
                if (rng.NextDouble() < turnChance) dir = (dir + (rng.Next(2) == 0 ? 1 : 5)) % 6;
                // 곧게 → 양옆 60° 순으로 시도. 선 모양을 유지하려고 이미 놓은 칸(직전 칸 제외)과 붙는 칸은 피한다.
                int[] order = rng.Next(2) == 0 ? new[] { 0, 1, 5 } : new[] { 0, 5, 1 };
                bool moved = false;
                foreach (int turn in order)
                {
                    int d = (dir + turn) % 6;
                    HexCoord next = cur.Neighbor((HexDirection)d);
                    if (!allowed(next) || tiles.Contains(next)) continue;
                    if (Neighbors(next).Count(tiles.Contains) > 1) continue;
                    tiles.Add(next);
                    cur = next;
                    dir = d;
                    moved = true;
                    break;
                }
                if (!moved) break;
            }
            return tiles;
        }

        private static List<HexCoord> GrowPlateau(List<HexCoord> leaf, int size, Func<HexCoord, bool> allowed, Random rng)
        {
            List<HexCoord> starts = leaf.Where(allowed).ToList();
            if (starts.Count == 0) return new List<HexCoord>();
            var tiles = new List<HexCoord> { starts[rng.Next(starts.Count)] };
            while (tiles.Count < size)
            {
                // 이웃이 많은 칸을 더 잘 고르게 해 둥근 덩어리가 되게 한다.
                List<HexCoord> frontier = tiles.SelectMany(Neighbors).Distinct().Where(c => allowed(c) && !tiles.Contains(c)).ToList();
                if (frontier.Count == 0) break;
                HexCoord pick = frontier.OrderByDescending(c => Neighbors(c).Count(tiles.Contains) + rng.NextDouble() * 1.5).First();
                tiles.Add(pick);
            }
            return tiles;
        }

        // ───────────── 도우미 ─────────────

        public static IEnumerable<HexCoord> Neighbors(HexCoord c)
        {
            for (int i = 0; i < 6; i++) yield return c.Neighbor((HexDirection)i);
        }

        private static bool OpenLandConnected(List<HexCoord> all, HashSet<HexCoord> inMap, Dictionary<HexCoord, TerrainType> terrain)
        {
            List<HexCoord> open = all.Where(c => terrain[c] != TerrainType.Obstacle).ToList();
            if (open.Count == 0) return false;
            var seen = new HashSet<HexCoord> { open[0] };
            var queue = new Queue<HexCoord>(seen);
            while (queue.Count > 0)
            {
                foreach (HexCoord n in Neighbors(queue.Dequeue()))
                {
                    if (!inMap.Contains(n) || terrain[n] == TerrainType.Obstacle || !seen.Add(n)) continue;
                    queue.Enqueue(n);
                }
            }
            return seen.Count == open.Count;
        }
    }

    /// <summary>수로 경로 탐색(양쪽이 모두 장애물인 변은 쓸 수 없음).</summary>
    public static class CanalPaths
    {
        public static bool Allowed(HexTopology topology, HexMap map, EdgeId e)
        {
            IReadOnlyList<HexCoord> sides = topology.TilesOf(e);
            return !(sides.Count == 2 && sides.All(t => map.GetTile(t).Terrain == TerrainType.Obstacle));
        }

        /// <summary>from 타일들의 꼭짓점에서 to 타일들의 꼭짓점까지 최단 수로 경로(변 목록). 없으면 null.</summary>
        public static List<EdgeId> ShortestPath(HexTopology topology, HexMap map, IEnumerable<VertexId> sources, IEnumerable<HexCoord> targets)
        {
            var goal = new HashSet<VertexId>(targets.SelectMany(HexTopology.VerticesOf).Where(topology.Contains));
            var parent = new Dictionary<VertexId, (VertexId from, EdgeId edge)?>();
            var queue = new Queue<VertexId>();
            foreach (VertexId s in sources.Where(topology.Contains).Distinct().OrderBy(v => v.ToString(), StringComparer.Ordinal))
            {
                parent[s] = null;
                queue.Enqueue(s);
            }
            while (queue.Count > 0)
            {
                VertexId v = queue.Dequeue();
                if (goal.Contains(v))
                {
                    var path = new List<EdgeId>();
                    for (var step = parent[v]; step.HasValue; step = parent[step.Value.from]) path.Add(step.Value.edge);
                    path.Reverse();
                    return path;
                }
                foreach (EdgeId e in topology.EdgesOf(v).OrderBy(x => x.ToString(), StringComparer.Ordinal))
                {
                    if (!Allowed(topology, map, e)) continue;
                    VertexId other = e.VertexA == v ? e.VertexB : e.VertexA;
                    if (parent.ContainsKey(other)) continue;
                    parent[other] = (v, e);
                    queue.Enqueue(other);
                }
            }
            return null;
        }

        public static int ShortestPathLength(HexTopology topology, HexMap map, IEnumerable<HexCoord> from, IEnumerable<HexCoord> to)
        {
            List<EdgeId> path = ShortestPath(topology, map, from.SelectMany(HexTopology.VerticesOf), to);
            return path?.Count ?? -1;
        }
    }
}
