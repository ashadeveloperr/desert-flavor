using System;
using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Core.Hex;
using DesertSettlement.Core.Progression;
using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>
    /// 맵 스케치: 에디터 씬 뷰에서 헥스 타일 지형을 칠해 예시 맵을 만드는 데이터(플레이 없이 편집).
    /// 칠하기·저장·불러오기는 에디터 확장(MapSketchEditor)이 담당하고, 이 컴포넌트는 데이터와 변환만 가진다.
    /// 저장 형식은 캠페인 맵(Assets/Data/Maps/*.json)과 같은 <see cref="MapDefinition"/>이라 그대로 맵으로 쓸 수도 있다.
    /// 메뉴 Tools > Desert Settlement > 맵 스케치 씬 열기.
    /// </summary>
    public sealed class MapSketch : MonoBehaviour
    {
        [Header("맵 정보")]
        public string mapId = "sketch_01";
        public string mapName = "스케치 1";
        [TextArea(3, 8)]
        [Tooltip("이 예시로 설명하고 싶은 생성 규칙 메모")]
        public string description = "";
        [Range(HexMap.MinRadius, HexMap.MaxRadius)]
        public int radius = 3;

        [Header("지형 (사막이 아닌 타일만 저장)")]
        public List<TerrainEntry> tiles = new List<TerrainEntry>();

        [Header("표시")]
        [Min(0.1f)] public float tileSize = 1f;
        public bool showCoordinates = true;
        public Color desert = new Color(0.86f, 0.74f, 0.52f);
        public Color waterSource = new Color(0.35f, 0.65f, 0.85f);
        public Color oreVein = new Color(0.55f, 0.4f, 0.45f);
        public Color bedrock = new Color(0.58f, 0.56f, 0.52f);
        public Color obstacle = new Color(0.3f, 0.26f, 0.22f);

        public HexLayout Layout => new HexLayout(tileSize);

        public Color ColorOf(TerrainType t)
        {
            switch (t)
            {
                case TerrainType.WaterSource: return waterSource;
                case TerrainType.OreVein: return oreVein;
                case TerrainType.Bedrock: return bedrock;
                case TerrainType.Obstacle: return obstacle;
                default: return desert;
            }
        }

        public bool Contains(HexCoord c) => HexCoord.Zero.DistanceTo(c) <= radius;

        public TerrainType TerrainAt(HexCoord c)
        {
            foreach (TerrainEntry e in tiles)
            {
                if (e.q == c.Q && e.r == c.R) return e.terrain;
            }
            return TerrainType.Desert;
        }

        /// <summary>타일 지형을 바꾼다(사막이면 목록에서 지운다). 바뀌었으면 true.</summary>
        public bool SetTerrain(HexCoord c, TerrainType terrain)
        {
            if (!Contains(c)) return false;
            int index = tiles.FindIndex(e => e.q == c.Q && e.r == c.R);
            if (terrain == TerrainType.Desert)
            {
                if (index < 0) return false;
                tiles.RemoveAt(index);
                return true;
            }
            if (index >= 0)
            {
                if (tiles[index].terrain == terrain) return false;
                tiles[index] = new TerrainEntry(c.Q, c.R, terrain);
                return true;
            }
            tiles.Add(new TerrainEntry(c.Q, c.R, terrain));
            return true;
        }

        /// <summary>반경을 바꾼 뒤 반경 밖 타일을 지운다.</summary>
        public void TrimToRadius() => tiles.RemoveAll(e => !Contains(new HexCoord(e.q, e.r)));

        /// <summary>지형별 개수(사막 포함).</summary>
        public Dictionary<TerrainType, int> Counts()
        {
            var counts = Enum.GetValues(typeof(TerrainType)).Cast<TerrainType>().ToDictionary(t => t, _ => 0);
            int total = 3 * radius * radius + 3 * radius + 1;
            foreach (TerrainEntry e in tiles) counts[e.terrain]++;
            counts[TerrainType.Desert] = total - tiles.Count(e => e.terrain != TerrainType.Desert);
            return counts;
        }

        public MapDefinition ToDefinition() => new MapDefinition
        {
            Id = mapId,
            Name = mapName,
            Description = description,
            Radius = radius,
            Terrain = tiles
                .Where(e => e.terrain != TerrainType.Desert)
                .OrderBy(e => e.r).ThenBy(e => e.q)
                .Select(e => new MapTerrainEntry { Q = e.q, R = e.r, Type = e.terrain.ToString() })
                .ToList(),
            SampleLayout = new List<MapBuildEntry>(),
        };

        public void LoadDefinition(MapDefinition map)
        {
            mapId = map.Id;
            mapName = map.Name;
            description = map.Description ?? "";
            radius = map.Radius;
            tiles = map.Terrain
                .Select(t => new TerrainEntry(t.Q, t.R, (TerrainType)Enum.Parse(typeof(TerrainType), t.Type, true)))
                .Where(e => e.terrain != TerrainType.Desert)
                .ToList();
        }

        // 선택하지 않아도 항상 보이도록 지형 채우기·경계선은 Gizmo로 그린다(씬 뷰 전용, 게임 화면엔 안 나옴).
        private void OnDrawGizmos()
        {
            HexLayout layout = Layout;
            foreach (HexCoord c in HexCoord.Range(radius))
            {
                Vector3 center = ToWorld(layout.TileCenter(c));
                Gizmos.color = ColorOf(TerrainAt(c));
                Gizmos.DrawMesh(WorldVisuals.HexMesh, center, transform.rotation, Vector3.one * tileSize * 0.97f);
                Gizmos.color = new Color(0f, 0f, 0f, 0.35f);
                for (int i = 0; i < 6; i++)
                {
                    Vector3 a = ToWorld(layout.CornerPosition(c, (HexCorner)i), 0.01f);
                    Vector3 b = ToWorld(layout.CornerPosition(c, (HexCorner)((i + 1) % 6)), 0.01f);
                    Gizmos.DrawLine(a, b);
                }
            }
        }

        public Vector3 ToWorld(HexPoint p, float height = 0f) =>
            transform.TransformPoint(new Vector3((float)p.X, height, (float)p.Z));

        public HexPoint ToHexPoint(Vector3 world)
        {
            Vector3 local = transform.InverseTransformPoint(world);
            return new HexPoint(local.x, local.z);
        }
    }
}
