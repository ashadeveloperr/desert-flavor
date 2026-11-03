using System.Collections.Generic;
using System.Linq;
using DesertSettlement.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DesertSettlement.EditorTools
{
    /// <summary>
    /// 메뉴 Tools > Desert Settlement > 디버그 세션 만들기.
    /// 현재 씬에 플레이 가능한 세션 한 벌(<see cref="DebugSessionBuilder"/>)을 만들고 설정·맵 파일을 연결한다.
    /// 이미 세션이 있으면 빠진 컴포넌트와 비어 있는 맵 목록만 채운다.
    /// </summary>
    public static class DebugSessionMenu
    {
        private const string ConfigPath = "Assets/Data/Config/economy_balance_v0.8_config.json";
        private const string MapsFolder = "Assets/Data/Maps";
        private const string ProceduralConfigPath = "Assets/Data/Config/procedural_map_config.json";

        [MenuItem("Tools/Desert Settlement/디버그 세션 만들기")]
        public static void CreateDebugSession()
        {
            List<TextAsset> maps = LoadMaps();
            GameSession existing = Object.FindAnyObjectByType<GameSession>();
            if (existing != null)
            {
                Undo.RegisterFullObjectHierarchyUndo(existing.gameObject, "Update Debug Session");
                int added = DebugSessionBuilder.EnsureComponents(existing);
                bool mapsAssigned = AssignMapsIfEmpty(existing, maps);
                bool proceduralAssigned = AssignProceduralIfEmpty(existing);
                Selection.activeObject = existing.gameObject;
                if (added > 0 || mapsAssigned || proceduralAssigned) EditorSceneManager.MarkSceneDirty(existing.gameObject.scene);
                Debug.Log($"[DebugSessionMenu] 씬에 이미 GameSession이 있습니다. 빠진 컴포넌트 {added}개{(mapsAssigned ? ", 맵 목록" : "")}{(proceduralAssigned ? ", 절차 맵 설정" : "")}를 채웠습니다.");
                return;
            }

            var config = AssetDatabase.LoadAssetAtPath<TextAsset>(ConfigPath);
            if (config == null)
            {
                Debug.LogError($"[DebugSessionMenu] 설정 파일을 찾을 수 없습니다: {ConfigPath}");
                return;
            }

            DebugSessionBuilder.Result result = DebugSessionBuilder.Build(config, maps, AssetDatabase.LoadAssetAtPath<TextAsset>(ProceduralConfigPath));
            Undo.RegisterCreatedObjectUndo(result.Session.gameObject, "Create Debug Session");
            Undo.RegisterCreatedObjectUndo(result.CameraRig.gameObject, "Create Debug Session");
            Undo.RegisterCreatedObjectUndo(result.VirtualCamera, "Create Debug Session");

            Selection.activeObject = result.Session.gameObject;
            EditorSceneManager.MarkSceneDirty(result.Session.gameObject.scene);
            Debug.Log($"[DebugSessionMenu] 디버그 세션을 만들었습니다(맵 {maps.Count}장). Play를 누르고 '샘플 배치 예약' 또는 도구로 건설해 보세요.");
        }

        /// <summary>
        /// batch mode용: SampleScene을 열어 디버그 세션을 넣거나 갱신하고 저장한다.
        /// Unity.exe -batchmode -quit -projectPath ... -executeMethod DesertSettlement.EditorTools.DebugSessionMenu.SetupSampleScene
        /// </summary>
        public static void SetupSampleScene()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            CreateDebugSession();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[DebugSessionMenu] SampleScene 저장 완료");
        }

        private static List<TextAsset> LoadMaps() =>
            AssetDatabase.FindAssets("t:TextAsset", new[] { MapsFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".json"))
                .OrderBy(p => p, System.StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<TextAsset>)
                .ToList();

        /// <summary>절차 맵 설정이 비어 있으면 연결한다(첫 정착지 뒤로 생성 맵).</summary>
        private static bool AssignProceduralIfEmpty(GameSession session)
        {
            var so = new SerializedObject(session);
            SerializedProperty prop = so.FindProperty("proceduralMapConfig");
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(ProceduralConfigPath);
            if (prop == null || prop.objectReferenceValue != null || asset == null) return false;
            prop.objectReferenceValue = asset;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool AssignMapsIfEmpty(GameSession session, List<TextAsset> maps)
        {
            var so = new SerializedObject(session);
            SerializedProperty list = so.FindProperty("maps");
            if (list == null || list.arraySize > 0 || maps.Count == 0) return false;
            list.arraySize = maps.Count;
            for (int i = 0; i < maps.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = maps[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
    }
}
