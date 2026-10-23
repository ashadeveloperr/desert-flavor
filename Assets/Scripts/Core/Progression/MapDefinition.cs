using System;
using System.Collections.Generic;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;

namespace DesertSettlement.Core.Progression
{
    /// <summary>
    /// 고정 맵 한 장(Assets/Data/Maps/*.json). 사막이 아닌 타일만 적는다.
    /// SampleLayout은 '샘플 배치 예약' 버튼과 테스트가 쓰는 예시 건설 목록(선택).
    /// </summary>
    public sealed class MapDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Radius { get; set; } = 3;
        public List<MapTerrainEntry> Terrain { get; set; } = new List<MapTerrainEntry>();
        public List<MapBuildEntry> SampleLayout { get; set; } = new List<MapBuildEntry>();

        public HexMap CreateMap()
        {
            var map = new HexMap(Radius);
            foreach (MapTerrainEntry t in Terrain)
            {
                if (!Enum.TryParse(t.Type, true, out TerrainType type))
                {
                    throw new ArgumentException($"맵 {Id}: 알 수 없는 지형 '{t.Type}'");
                }
                if (map.TryGetTile(new HexCoord(t.Q, t.R), out HexTile tile)) tile.Terrain = type;
                else throw new ArgumentException($"맵 {Id}: 반경 밖 좌표 ({t.Q},{t.R})");
            }
            return map;
        }

        /// <summary>샘플 배치를 예약한다. 실패한 항목의 설명 목록을 돌려준다(전부 성공하면 빈 목록).</summary>
        public List<string> RequestSampleLayout(Settlement settlement)
        {
            var failures = new List<string>();
            foreach (MapBuildEntry b in SampleLayout)
            {
                CommandResult r;
                if (b.Kind == BuildingKinds.CanalEdge)
                {
                    if (!Enum.TryParse(b.Side, true, out HexDirection side))
                    {
                        failures.Add($"{b.Kind} ({b.Q},{b.R}) 방향 '{b.Side}'을 알 수 없음");
                        continue;
                    }
                    EdgeId edge = EdgeId.Of(new HexCoord(b.Q, b.R), side);
                    r = settlement.RequestCanal(edge);
                }
                else
                {
                    r = settlement.RequestBuild(b.Kind, new HexCoord(b.Q, b.R));
                }
                if (!r.Success) failures.Add($"{b.Kind} ({b.Q},{b.R}{(b.Side != null ? " " + b.Side : "")}): {r.Message}");
            }
            return failures;
        }
    }

    public sealed class MapTerrainEntry
    {
        public int Q { get; set; }
        public int R { get; set; }
        /// <summary>TerrainType 이름: WaterSource, OreVein, Bedrock, Obstacle.</summary>
        public string Type { get; set; }
    }

    public sealed class MapBuildEntry
    {
        /// <summary>buildingLevels 키(well, farm, canalEdge ...).</summary>
        public string Kind { get; set; }
        public int Q { get; set; }
        public int R { get; set; }
        /// <summary>수로·수문만: 타일 (Q,R)의 변 방향(East, SouthEast, ...).</summary>
        public string Side { get; set; }
    }
}
