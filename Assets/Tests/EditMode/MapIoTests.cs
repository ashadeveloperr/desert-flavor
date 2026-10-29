using System.Collections.Generic;
using System.IO;
using System.Linq;
using DesertSettlement.Core.Hex;
using DesertSettlement.Core.Progression;
using NUnit.Framework;
using UnityEngine;

namespace DesertSettlement.Core.Tests
{
    /// <summary>맵 JSON 저장·불러오기(맵 스케치 도구와 캠페인 맵이 같은 형식을 쓴다).</summary>
    public class MapIoTests
    {
        [Test]
        public void ToJson_ThenParse_RoundTrips()
        {
            var map = new MapDefinition
            {
                Id = "sketch_test",
                Name = "테스트 스케치",
                Description = "규칙 메모",
                Radius = 4,
                Terrain = new List<MapTerrainEntry>
                {
                    new MapTerrainEntry { Q = 0, R = 0, Type = "WaterSource" },
                    new MapTerrainEntry { Q = 4, R = -2, Type = "Obstacle" },
                },
            };

            string json = MapLoader.ToJson(map);
            StringAssert.Contains("\"waterSource\"".Replace("waterSource", "WaterSource"), json, "지형 이름은 enum 그대로");
            StringAssert.Contains("\"radius\": 4", json, "키는 camelCase");

            MapDefinition back = MapLoader.Parse(json);
            Assert.AreEqual("테스트 스케치", back.Name);
            Assert.AreEqual(4, back.Radius);
            Assert.AreEqual(2, back.Terrain.Count);
            HexMap hex = back.CreateMap();
            Assert.AreEqual(TerrainType.Obstacle, hex.GetTile(new HexCoord(4, -2)).Terrain);
        }

        [Test]
        public void ExistingCampaignMaps_SurviveARoundTrip()
        {
            foreach (string path in TestData.MapPaths)
            {
                MapDefinition original = MapLoader.Parse(File.ReadAllText(path));
                MapDefinition back = MapLoader.Parse(MapLoader.ToJson(original));
                Assert.AreEqual(original.Terrain.Count, back.Terrain.Count, path);
                Assert.AreEqual(original.SampleLayout.Count, back.SampleLayout.Count, path);
                CollectionAssert.AreEqual(original.Terrain.Select(t => (t.Q, t.R, t.Type)), back.Terrain.Select(t => (t.Q, t.R, t.Type)), path);
            }
        }

        [Test]
        public void ToJson_RejectsTilesOutsideRadius()
        {
            var map = new MapDefinition { Id = "bad", Radius = 3, Terrain = { new MapTerrainEntry { Q = 5, R = 0, Type = "Obstacle" } } };
            Assert.Throws<System.ArgumentException>(() => MapLoader.ToJson(map));
        }
    }
}
