using System;
using System.Collections.Generic;
using DesertSettlement.Core.Hex;
using DesertSettlement.Core.Progression;

namespace DesertSettlement.Runtime
{
    /// <summary>인스펙터에서 편집하는 지형 한 칸(맵 파일이 없을 때의 대체 지형).</summary>
    [Serializable]
    public struct TerrainEntry
    {
        public int q;
        public int r;
        public TerrainType terrain;

        public TerrainEntry(int q, int r, TerrainType terrain)
        {
            this.q = q;
            this.r = r;
            this.terrain = terrain;
        }
    }

    /// <summary>맵 파일이 하나도 없을 때 인스펙터 지형(아래 TerrainEntry 목록)으로 만드는 대체 맵.</summary>
    public sealed partial class GameSession
    {
        private MapDefinition FallbackMap()
        {
            var def = new MapDefinition { Id = "inspector", Name = "인스펙터 지형", Radius = mapRadius };
            foreach (TerrainEntry t in terrain) def.Terrain.Add(new MapTerrainEntry { Q = t.q, R = t.r, Type = t.terrain.ToString() });
            return def;
        }

        /// <summary>맵 파일이 없을 때의 대체 지형(맵 1과 같음).</summary>
        public static List<TerrainEntry> DefaultTestTerrain() => new List<TerrainEntry>
        {
            new TerrainEntry(0, 0, TerrainType.WaterSource),
            new TerrainEntry(-2, 2, TerrainType.WaterSource),
            new TerrainEntry(2, 0, TerrainType.OreVein),
            new TerrainEntry(-1, 3, TerrainType.OreVein),
            new TerrainEntry(2, -1, TerrainType.Bedrock),
            new TerrainEntry(-2, 0, TerrainType.Bedrock),
            new TerrainEntry(0, -3, TerrainType.Obstacle),
            new TerrainEntry(3, -2, TerrainType.Obstacle),
            new TerrainEntry(-3, 1, TerrainType.Obstacle),
        };
    }
}
