using System;
using System.Collections.Generic;

namespace DesertSettlement.Core.Hex
{
    /// <summary>
    /// 타일 지형. 어떤 시설을 지을 수 있는지는 이후 건설 규칙에서 정한다.
    /// 새 지형을 추가할 때는 끝에 추가한다(저장 데이터 호환).
    /// </summary>
    public enum TerrainType
    {
        /// <summary>일반 사막. 농장·창고·경매장 등.</summary>
        Desert = 0,
        /// <summary>수원. 우물 설치 가능.</summary>
        WaterSource = 1,
        /// <summary>광맥. 광산 설치 가능.</summary>
        OreVein = 2,
        /// <summary>암반. 채석장 설치 가능.</summary>
        Bedrock = 3,
        /// <summary>장애물. 건설 불가/제한.</summary>
        Obstacle = 4,
    }

    public sealed class HexTile
    {
        public HexCoord Coord { get; }
        public TerrainType Terrain { get; set; }

        public HexTile(HexCoord coord, TerrainType terrain)
        {
            Coord = coord;
            Terrain = terrain;
        }
    }

    /// <summary>
    /// 반경 R의 육각형 맵(원점 중심). 반경 3 = 37타일, 5 = 91타일, 7 = 169타일.
    /// 고정 맵 데이터(수원·광맥 배치)는 아직 미정이라 모든 타일을 사막으로 만든다.
    /// </summary>
    public sealed class HexMap
    {
        public const int MinRadius = 3;
        public const int MaxRadius = 7;

        private readonly Dictionary<HexCoord, HexTile> _tiles = new Dictionary<HexCoord, HexTile>();

        public int Radius { get; }
        public int TileCount => _tiles.Count;
        public IEnumerable<HexTile> Tiles => _tiles.Values;

        public HexMap(int radius)
        {
            if (radius < MinRadius || radius > MaxRadius)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), $"맵 반경은 {MinRadius}~{MaxRadius} 사이여야 합니다: {radius}");
            }

            Radius = radius;
            foreach (HexCoord coord in HexCoord.Range(radius))
            {
                _tiles.Add(coord, new HexTile(coord, TerrainType.Desert));
            }
        }

        public bool Contains(HexCoord coord) => _tiles.ContainsKey(coord);

        public bool TryGetTile(HexCoord coord, out HexTile tile) => _tiles.TryGetValue(coord, out tile);

        public HexTile GetTile(HexCoord coord)
        {
            if (!_tiles.TryGetValue(coord, out HexTile tile))
            {
                throw new KeyNotFoundException($"맵 밖의 좌표입니다: {coord}");
            }
            return tile;
        }
    }
}
