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

        /// <summary>맵을 JSON 문자열로 저장한다(Assets/Data/Maps와 같은 camelCase 형식, 들여쓰기).</summary>
        public static string ToJson(MapDefinition map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            map.CreateMap(); // 저장 전에도 같은 검증
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver(),
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
            };
            return JsonConvert.SerializeObject(map, settings);
        }
    }
}
