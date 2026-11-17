using System.Collections.Generic;
using DesertSettlement.Core.Hex;
using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 지형 표시(결정 GR3·GR5): 칸 색 대신 바닥 모양과 장식으로 지형을 알린다. 배치는 타일 좌표로 정해져 늘 같다.
    /// - 수원: 가운데가 오목하고 둘레가 솟은 샘 자리 + 물웅덩이·돌·풀·작은 야자수
    /// - 광맥: 어두운 바위 사이로 보라색 수정 무리가 솟는다
    /// - 암반: 채석장 돌 색의 층 바위가 계단처럼 쌓인 두꺼운 덩어리와 둘레의 바위 조각
    /// - 장애물: 위가 평평하고 옆이 층진 절벽인 암반 기둥(광산 바위 색)
    /// 그 칸에 시설(우물·광산·채석장)이 들어서면 장식을 숨기고 바닥 모양만 남는다(시설이 그 위에 묻힌다).
    /// </summary>
    public sealed partial class SettlementView
    {
        [Header("지형 장식 (타일 반지름 1 기준)")]
        [Tooltip("샘 물웅덩이 반지름과 물 높이, 둘레 둔덕 높이")]
        [SerializeField] private float springRadius = 0.36f;
        [SerializeField] private float springWaterHeight = 0.016f;
        [SerializeField] private float springRim = 0.05f;
        [Tooltip("장애물 칸 가운데 둔덕 높이, 암반 칸 모래 굴곡 배율(작을수록 평평, 층 바위가 바로 얹힘)")]
        [SerializeField] private float obstacleMound = 0.1f;
        [SerializeField, Range(0f, 1f)] private float bedrockFlatness = 0.3f;

        private readonly Dictionary<HexCoord, GameObject> _features = new Dictionary<HexCoord, GameObject>();

        /// <summary>지형별 바닥 모양. <paramref name="sand"/> = 모래 굴곡 높이, <paramref name="edge"/> = 가장자리로 갈수록 0이 되는 배율.</summary>
        private float TerrainShape(TerrainType terrain, Vector2 local, float sand, float edge)
        {
            float r = local.magnitude;
            switch (terrain)
            {
                case TerrainType.WaterSource:
                {
                    // 샘 자리: 가운데는 낮고 평평하게(물웅덩이 아래), 둘레는 둔덕.
                    float bowl = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(springRadius * 1.05f, springRadius * 0.6f, r));
                    float rimOffset = (r - springRadius * 1.2f) / 0.13f;
                    return Mathf.Lerp(sand, 0.004f, bowl) + springRim * Mathf.Exp(-rimOffset * rimOffset) * edge;
                }
                case TerrainType.Bedrock:
                    return sand * bedrockFlatness;
                case TerrainType.Obstacle:
                    return sand + obstacleMound * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.75f, 0.1f, r)) * edge;
                default:
                    return sand;
            }
        }

        private GameObject BuildFeature(HexTile tile)
        {
            switch (tile.Terrain)
            {
                case TerrainType.WaterSource: return Feature("Spring", tile.Coord, Spring);
                case TerrainType.OreVein: return Feature("Ore Vein", tile.Coord, OreVein);
                case TerrainType.Bedrock: return Feature("Bedrock", tile.Coord, Bedrock);
                case TerrainType.Obstacle: return Feature("Obstacle", tile.Coord, Obstacle);
                default: return null;
            }
        }

        /// <summary>시설이 들어선 칸의 장식을 숨긴다(우물이 샘을, 광산이 수정을, 채석장이 판석을 덮는다).</summary>
        private void UpdateFeatures(HashSet<HexCoord> occupied)
        {
            foreach (KeyValuePair<HexCoord, GameObject> feature in _features)
            {
                bool show = !occupied.Contains(feature.Key);
                if (feature.Value.activeSelf != show) feature.Value.SetActive(show);
            }
        }

        /// <summary>장식 묶음 하나: 타일 가운데에 뿌리를 두고(크기 = 타일 크기) <paramref name="fill"/>이 채운다.</summary>
        private GameObject Feature(string name, HexCoord tile, System.Action<Transform, HexCoord, System.Random> fill)
        {
            var root = new GameObject($"{name} {tile}").transform;
            root.SetParent(_natureRoot, false);
            root.position = _session.ToWorld(_session.Layout.TileCenter(tile));
            root.localScale = Vector3.one * Size;
            fill(root, tile, new System.Random(TileSeed(tile, 7)));
            return root.gameObject;
        }

        // 샘: 물웅덩이 + 둘레 돌 셋 + 풀 덤불 셋 + 작은 야자수 하나.
        private void Spring(Transform root, HexCoord tile, System.Random random)
        {
            int variant = random.Next(NatureModels.Variants);
            GameObject water = Decoration(root, "Water", NatureModels.SpringWater(variant), NatureModels.Water, Vector2.zero, 0f, springRadius);
            water.transform.localPosition = new Vector3(0f, springWaterHeight, 0f);
            water.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            float start = Angle(random);
            for (int i = 0; i < 3; i++)
            {
                float a = start + i * 2.1f;
                Stone(root, tile, random, Polar(a, springRadius * 1.05f), NatureModels.StoneColors, new Vector3(0.065f, 0.035f, 0.05f));
                Vector2 tuft = Polar(a + 1.05f, springRadius * (1.15f + 0.2f * (float)random.NextDouble()));
                Decoration(root, "Grass", NatureModels.GrassTuft(i % NatureModels.Variants), WorldVisuals.Solid(NatureModels.GrassColors[i % 3]),
                    tuft, GroundHeight(tile, tuft), 0.9f + 0.4f * (float)random.NextDouble());
            }
            Vector2 palmAt = Polar(start + 1.6f, 0.62f);
            Palm(root, variant, palmAt, GroundHeight(tile, palmAt), 0.7f, Angle(random) * Mathf.Rad2Deg);
        }

        // 광맥: 어두운 바위 다섯 + 수정 무리 셋(가운데 무리가 가장 크다). 수정은 바깥으로 기울어 솟는다.
        private void OreVein(Transform root, HexCoord tile, System.Random random)
        {
            float start = Angle(random);
            for (int i = 0; i < 5; i++)
                Stone(root, tile, random, Polar(start + i * 1.26f, 0.32f + 0.3f * (float)random.NextDouble()), new[] { NatureModels.OreRockColor },
                    new Vector3(0.14f, 0.1f, 0.12f) * (0.8f + 0.5f * (float)random.NextDouble()));
            Vector2[] clusters = { Vector2.zero, Polar(start + 0.6f, 0.45f), Polar(start + 3.1f, 0.4f) };
            for (int c = 0; c < clusters.Length; c++)
            {
                float size = c == 0 ? 1f : 0.7f;
                int shards = c == 0 ? 5 : 3;
                for (int k = 0; k < shards; k++)
                {
                    float a = Angle(random);
                    Vector2 at = clusters[c] + Polar(a, k == 0 ? 0f : 0.05f + 0.04f * (float)random.NextDouble());
                    GameObject shard = Decoration(root, "Crystal", NatureModels.CrystalShard(), NatureModels.Crystal, at, GroundHeight(tile, at) - 0.01f, 1f);
                    float h = size * (k == 0 ? 0.38f : 0.2f + 0.1f * (float)random.NextDouble());
                    shard.transform.localScale = new Vector3(h * 0.75f, h, h * 0.75f);
                    shard.transform.localRotation = Quaternion.Euler(k == 0 ? 0f : 20f + 15f * (float)random.NextDouble(), a * Mathf.Rad2Deg, 0f);
                }
            }
        }

        // 암반: 채석장 돌 색의 층 바위가 계단처럼 어긋나게 쌓인 두꺼운 덩어리(큰 것 세 켜 + 옆의 작은 것 두 켜)와 둘레에 반쯤 묻힌 바위 조각.
        private void Bedrock(Transform root, HexCoord tile, System.Random random)
        {
            float start = Angle(random);
            Strata(root, tile, random, Polar(start, 0.06f), new[] { (0.68f, 0.16f), (0.52f, 0.15f), (0.34f, 0.12f) }, start + 2.4f, NatureModels.BedrockColors);
            Strata(root, tile, random, Polar(start + 2.6f, 0.6f), new[] { (0.26f, 0.13f), (0.17f, 0.1f) }, start + 0.9f, NatureModels.BedrockColors);
            for (int i = 0; i < 6; i++)
            {
                float s = 0.07f + 0.07f * (float)random.NextDouble();
                Stone(root, tile, random, Polar(start + 1f + i * 0.85f, 0.66f + 0.18f * (float)random.NextDouble()), NatureModels.BedrockColors,
                    new Vector3(s * 1.3f, s * 0.8f, s));
            }
        }

        /// <summary>
        /// 층 바위 더미 하나: <paramref name="layers"/>(반지름, 두께)를 아래부터 쌓고, 위 켜일수록 <paramref name="drift"/> 방향으로 <paramref name="step"/>씩 밀어
        /// 계단처럼 드러나게 한다(밀지 않고 <paramref name="stack"/>을 작게 해 많이 겹치면 곧은 절벽 기둥). 켜마다 <paramref name="colors"/> 중 한 색이라 층이 보인다. 맨 아래 켜는 모래에 살짝 묻힌다.
        /// </summary>
        private void Strata(Transform root, HexCoord tile, System.Random random, Vector2 at, (float radius, float thickness)[] layers, float drift,
            Color[] colors, float step = 0.07f, float stack = 0.93f)
        {
            float y = GroundHeight(tile, at) - 0.03f;
            for (int i = 0; i < layers.Length; i++)
            {
                Vector2 p = at + Polar(drift, step * i);
                GameObject slab = Decoration(root, "Rock Layer", NatureModels.Slab(random.Next(NatureModels.Variants)),
                    WorldVisuals.Solid(colors[random.Next(colors.Length)]), p, y, 1f);
                float r = layers[i].radius;
                slab.transform.localScale = new Vector3(r, layers[i].thickness, r * (0.8f + 0.15f * (float)random.NextDouble()));
                slab.transform.localRotation = Quaternion.Euler(0f, Angle(random) * Mathf.Rad2Deg, 0f);
                y += layers[i].thickness * stack; // 1보다 작을수록 겹쳐 얹어 켜 사이 홈이 얕아진다
            }
        }

        // 장애물: 위가 평평한 암반 기둥 — 층 바위 여섯 켜를 거의 어긋나지 않게 높이 쌓아 옆면이 층진 절벽이 되고,
        // 옆에 낮은 기둥 하나가 붙는다. 발치에는 무너져 내린 바위 조각(광산 바위 색).
        private void Obstacle(Transform root, HexCoord tile, System.Random random)
        {
            float start = Angle(random);
            var main = new (float, float)[6];
            for (int i = 0; i < main.Length; i++) main[i] = (0.55f - 0.012f * i + 0.03f * (float)random.NextDouble(), 0.19f + 0.04f * (float)random.NextDouble());
            Strata(root, tile, random, Polar(start, 0.08f), main, start + 1.2f, NatureModels.BoulderColors, 0.01f, 0.7f);
            Strata(root, tile, random, Polar(start + 3.3f, 0.52f), new[] { (0.3f, 0.18f), (0.28f, 0.17f), (0.26f, 0.16f) }, start, NatureModels.BoulderColors, 0.01f, 0.7f);
            for (int i = 0; i < 7; i++)
            {
                float s = 0.07f + 0.07f * (float)random.NextDouble();
                Stone(root, tile, random, Polar(start + 0.5f + i * 0.9f, 0.68f + 0.12f * (float)random.NextDouble()), NatureModels.BoulderColors, new Vector3(s * 1.2f, s * 0.8f, s));
            }
        }

        /// <summary>각진 바위 하나(바닥 높이에 살짝 묻고 <paramref name="raise"/>만큼 올림, 색은 <paramref name="colors"/> 중 하나, 방향은 제멋대로).</summary>
        private void Stone(Transform root, HexCoord tile, System.Random random, Vector2 at, Color[] colors, Vector3 size, float raise = 0f)
        {
            GameObject stone = Decoration(root, "Stone", NatureModels.Rock(random.Next(NatureModels.Variants)),
                WorldVisuals.Solid(colors[random.Next(colors.Length)]), at, GroundHeight(tile, at) + size.y * 0.2f + raise, 1f);
            stone.transform.localScale = size;
            stone.transform.localRotation = Quaternion.Euler(0f, Angle(random) * Mathf.Rad2Deg, 0f);
        }

        private static float Angle(System.Random random) => (float)random.NextDouble() * Mathf.PI * 2f;

        private static Vector2 Polar(float angle, float radius) => new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * radius;

        /// <summary>타일마다 늘 같은 난수 시드(HexCoord.GetHashCode는 실행마다 달라 쓰지 않는다).</summary>
        private static int TileSeed(HexCoord tile, int salt) => unchecked(tile.Q * 73856093 ^ tile.R * 19349663 ^ salt * 83492791);

        /// <summary>메시 하나짜리 장식(타일 단위 위치 <paramref name="at"/>, 높이 <paramref name="y"/>, 크기 <paramref name="scale"/>).</summary>
        private static GameObject Decoration(Transform parent, string name, Mesh mesh, Material material, Vector2 at, float y, float scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(at.x, y, at.y);
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>야자수 하나(줄기 + 잎). 바닥에 살짝 묻어 세운다.</summary>
        private static Transform Palm(Transform parent, int variant, Vector2 at, float y, float scale, float yaw)
        {
            GameObject palm = Decoration(parent, "Palm", NatureModels.PalmTrunk(variant), WorldVisuals.Solid(NatureModels.TrunkColor), at, y - 0.01f, scale);
            palm.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            GameObject crown = Decoration(palm.transform, "Fronds", NatureModels.PalmCrown(variant), WorldVisuals.Solid(NatureModels.FrondColor), Vector2.zero, 0f, 1f);
            crown.transform.localPosition = NatureModels.PalmTop(variant);
            return palm.transform;
        }
    }
}
