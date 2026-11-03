using System;
using System.Collections.Generic;
using DesertSettlement.Core.Config;
using Newtonsoft.Json;

namespace DesertSettlement.Core.Progression
{
    /// <summary>
    /// 절차 맵 설정 JSON을 읽고 검증한다. 파일 읽기는 호출하는 쪽(Unity에서는 TextAsset, 시뮬레이터는 파일)이 한다.
    /// 클래스에 없는 키(오타)는 오류다(<see cref="JsonKeyCheck"/>, '_'로 시작하는 설명용 키는 허용).
    /// </summary>
    public static class ProceduralMapConfigLoader
    {
        /// <summary>지원하는 스키마. JSON의 "schema" 값과 같아야 한다.</summary>
        public const string SupportedSchema = "desert-settlement-procedural-map-v1";

        public static ProceduralMapConfig Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("절차 맵 설정 내용이 비어 있습니다.");
            ProceduralMapConfig config;
            try
            {
                config = JsonConvert.DeserializeObject<ProceduralMapConfig>(json);
            }
            catch (JsonException e)
            {
                throw new ArgumentException("절차 맵 설정 JSON 형식 오류: " + e.Message);
            }
            if (config == null) throw new ArgumentException("절차 맵 설정을 읽을 수 없습니다.");
            List<string> problems = EconomyConfigLoader.UnknownKeyProblems<ProceduralMapConfig>(json);
            if (config.Schema != SupportedSchema) problems.Add($"schema가 '{SupportedSchema}'가 아닙니다(현재 '{config.Schema}').");
            problems.AddRange(config.Validate());
            if (problems.Count > 0) throw new ArgumentException("절차 맵 설정 오류:\n- " + string.Join("\n- ", problems));
            return config;
        }
    }
}
