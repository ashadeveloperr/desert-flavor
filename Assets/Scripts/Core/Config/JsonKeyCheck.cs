using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace DesertSettlement.Core.Config
{
    /// <summary>
    /// 설정 JSON에서 클래스에 없는 키를 찾는다. Newtonsoft는 모르는 키를 조용히 무시해서, 키 이름에 오타가 나면
    /// 오류 없이 그 값이 기본값(0·null)이 된다. 설정 로더는 이 목록을 불러오기 오류로 낸다(결정 CF1).
    /// '_'로 시작하는 키는 설명용으로 허용한다. 딕셔너리의 키(시설 종류 등)는 데이터라서 검사하지 않는다.
    /// </summary>
    public static class JsonKeyCheck
    {
        /// <summary>json을 T로 읽을 때 T와 그 안의 타입에 없는 키의 경로(예: "disasters.graceHour"). 없으면 빈 목록.</summary>
        public static List<string> UnknownKeys<T>(string json)
        {
            var unknown = new List<string>();
            Walk(JToken.Parse(json), typeof(T), JsonSerializer.CreateDefault().ContractResolver, unknown);
            return unknown;
        }

        private static void Walk(JToken token, Type type, IContractResolver resolver, List<string> unknown)
        {
            switch (resolver.ResolveContract(type))
            {
                case JsonObjectContract obj when token is JObject o:
                    foreach (JProperty p in o.Properties())
                    {
                        if (p.Name.StartsWith("_", StringComparison.Ordinal)) continue;
                        JsonProperty member = obj.Properties.GetClosestMatchProperty(p.Name); // 역직렬화와 같은 대소문자 무시 매칭
                        if (member == null || member.Ignored) unknown.Add(p.Path);
                        else Walk(p.Value, member.PropertyType, resolver, unknown);
                    }
                    break;
                case JsonDictionaryContract dict when token is JObject d && dict.DictionaryValueType != null:
                    foreach (JProperty p in d.Properties()) Walk(p.Value, dict.DictionaryValueType, resolver, unknown);
                    break;
                case JsonArrayContract array when token is JArray a && array.CollectionItemType != null:
                    foreach (JToken item in a) Walk(item, array.CollectionItemType, resolver, unknown);
                    break;
            }
        }
    }
}
