using System;
using System.Collections.Generic;
using DesertSettlement.Core.Progression;
using UnityEngine;

namespace DesertSettlement.Runtime
{
    /// <summary>저장·불러오기(F5/F9, 결정 S1~S5): 파일 한 칸에 시드 + 명령 기록을 쓰고, 불러올 때 같은 시드로 다시 실행한다.</summary>
    public sealed partial class GameSession
    {
        /// <summary>저장 파일 위치(한 칸). 에디터·빌드 모두 Application.persistentDataPath 아래.</summary>
        public string SavePath => System.IO.Path.Combine(Application.persistentDataPath, "desert_settlement_save.json");

        public bool HasSave => System.IO.File.Exists(SavePath);

        /// <summary>지금 캠페인을 저장한다(명령 기록 + 시드 + 지금 시간). 결과 문구를 돌려준다.</summary>
        public string SaveGame()
        {
            if (!IsReady || Campaign == null) return "저장할 게임이 없습니다.";
            try
            {
                SaveData data = DesertSettlement.Core.Progression.SaveGame.Capture(Campaign, DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                System.IO.File.WriteAllText(SavePath, SaveGameJson.Serialize(data));
                Debug.Log($"[Save] {SavePath} — {data.Summary}, 명령 {data.Commands.Count}개");
                return $"저장했습니다: {data.Summary}";
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] 저장 실패: {e.Message}", this);
                return "저장 실패: " + e.Message;
            }
        }

        /// <summary>저장 파일을 읽어 같은 시드로 명령을 다시 실행해 캠페인을 되살린다. 결과 문구를 돌려준다.</summary>
        public string LoadGame()
        {
            if (!HasSave) return "저장 파일이 없습니다.";
            try
            {
                SaveData data = SaveGameJson.Deserialize(System.IO.File.ReadAllText(SavePath));
                LoadData(out List<MapDefinition> definitions, out ProceduralMapConfig procedural);
                var result = DesertSettlement.Core.Progression.SaveGame.Replay(Config, definitions, procedural, data);
                foreach (string p in result.Problems) Debug.LogWarning("[Save] 복원 중 " + p);
                IsReady = false;
                Campaign = result.Campaign;
                EnsureClock();
                HookSettlement();
                Clock.RestoreElapsedHours(Settlement?.Now ?? 0); // 저장한 분까지(결정 JQ4)
                Clock.SetPaused(true); // 불러온 뒤에는 멈춘 상태로 시작
                string warn = result.Problems.Count > 0 ? $" (경고 {result.Problems.Count}개, 콘솔 참고)" : "";
                return $"불러왔습니다: {data.Summary}{warn}";
            }
            catch (Exception e)
            {
                Debug.LogError($"[Save] 불러오기 실패: {e.Message}", this);
                return "불러오기 실패: " + e.Message;
            }
        }
    }
}
