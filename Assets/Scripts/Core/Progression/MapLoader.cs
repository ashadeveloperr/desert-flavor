using System;
using Newtonsoft.Json;

namespace DesertSettlement.Core.Progression
{
    /// <summary>맵 JSON을 읽는다. 검증 실패 시 이유와 함께 예외.</summary>
    public static class MapLoader
    {
        public static MapDefinition Parse(string json)
        {
            MapDefinition map;
            try
            {
                map = JsonConvert.DeserializeObject<MapDefinition>(json);
            }
            catch (JsonException e)
            {
                throw new ArgumentException("맵 JSON 형식 오류: " + e.Message);
            }
            if (map == null || string.IsNullOrEmpty(map.Id)) throw new ArgumentException("맵 JSON에 id가 없습니다.");
            map.CreateMap(); // 지형·좌표 검증
            return map;
        }
    }
}
