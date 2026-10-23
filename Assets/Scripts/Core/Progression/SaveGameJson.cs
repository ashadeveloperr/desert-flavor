using Newtonsoft.Json;

namespace DesertSettlement.Core.Progression
{
    /// <summary>저장 파일(JSON) 읽기·쓰기. 내용과 복원 방법은 <see cref="SaveGame"/>.</summary>
    public static class SaveGameJson
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            DefaultValueHandling = DefaultValueHandling.Ignore, // 쓰지 않는 칸(0/null)은 파일에 적지 않는다
        };

        public static string Serialize(SaveData data) => JsonConvert.SerializeObject(data, Settings);

        public static SaveData Deserialize(string json) => JsonConvert.DeserializeObject<SaveData>(json, Settings);
    }
}
