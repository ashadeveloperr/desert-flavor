using DesertSettlement.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DesertSettlement.EditorTools
{
    /// <summary>
    /// 메뉴 Tools > Desert Settlement > 디버그 세션 만들기.
    /// 현재 씬에 GameSession + 디버그 뷰 + HUD + 낮/밤 조명 오브젝트를 만들고 설정 파일을 연결한다.
    /// </summary>
    public static class DebugSessionMenu
    {
        private const string ConfigPath = "Assets/Data/Config/economy_balance_v0.8_config.json";

        [MenuItem("Tools/Desert Settlement/디버그 세션 만들기")]
        public static void CreateDebugSession()
        {
            GameSession existing = Object.FindAnyObjectByType<GameSession>();
            if (existing != null)
            {
                Selection.activeObject = existing.gameObject;
                Debug.LogWarning("[DebugSessionMenu] 씬에 이미 GameSession이 있습니다. 선택만 했습니다.");
                return;
            }

            var go = new GameObject("GameSession");
            Undo.RegisterCreatedObjectUndo(go, "Create Debug Session");

            var session = go.AddComponent<GameSession>();
            go.AddComponent<HexMapDebugView>();
            go.AddComponent<GameClockHud>();
            var lighting = go.AddComponent<DayNightLighting>();

            var config = AssetDatabase.LoadAssetAtPath<TextAsset>(ConfigPath);
            if (config == null) Debug.LogError($"[DebugSessionMenu] 설정 파일을 찾을 수 없습니다: {ConfigPath}");

            var sessionSo = new SerializedObject(session);
            sessionSo.FindProperty("economyConfig").objectReferenceValue = config;
            sessionSo.ApplyModifiedPropertiesWithoutUndo();

            var lightingSo = new SerializedObject(lighting);
            lightingSo.FindProperty("session").objectReferenceValue = session;
            lightingSo.FindProperty("sun").objectReferenceValue = FindSun();
            lightingSo.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeObject = go;
            EditorSceneManager.MarkSceneDirty(go.scene);
        }

        private static Light FindSun()
        {
            if (RenderSettings.sun != null) return RenderSettings.sun;
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional) return light;
            }
            Debug.LogWarning("[DebugSessionMenu] Directional Light가 없어 낮/밤 조명이 동작하지 않습니다. DayNightLighting의 Sun에 지정하세요.");
            return null;
        }
    }
}
