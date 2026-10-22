using DesertSettlement.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DesertSettlement.EditorTools
{
    /// <summary>
    /// 메뉴 Tools > Desert Settlement > 디버그 세션 만들기.
    /// 현재 씬에 플레이 가능한 세션 한 벌(<see cref="DebugSessionBuilder"/>)을 만들고 설정 파일을 연결한다.
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
                Undo.RegisterFullObjectHierarchyUndo(existing.gameObject, "Update Debug Session");
                int added = DebugSessionBuilder.EnsureComponents(existing);
                Selection.activeObject = existing.gameObject;
                if (added > 0) EditorSceneManager.MarkSceneDirty(existing.gameObject.scene);
                Debug.Log($"[DebugSessionMenu] 씬에 이미 GameSession이 있습니다. 빠진 컴포넌트 {added}개를 붙였습니다.");
                return;
            }

            var config = AssetDatabase.LoadAssetAtPath<TextAsset>(ConfigPath);
            if (config == null)
            {
                Debug.LogError($"[DebugSessionMenu] 설정 파일을 찾을 수 없습니다: {ConfigPath}");
                return;
            }

            DebugSessionBuilder.Result result = DebugSessionBuilder.Build(config);
            Undo.RegisterCreatedObjectUndo(result.Session.gameObject, "Create Debug Session");
            Undo.RegisterCreatedObjectUndo(result.CameraRig.gameObject, "Create Debug Session");
            Undo.RegisterCreatedObjectUndo(result.VirtualCamera, "Create Debug Session");

            Selection.activeObject = result.Session.gameObject;
            EditorSceneManager.MarkSceneDirty(result.Session.gameObject.scene);
            Debug.Log("[DebugSessionMenu] 디버그 세션을 만들었습니다. Play를 누르고 '샘플 배치 예약' 또는 도구로 건설해 보세요.");
        }

        /// <summary>
        /// batch mode용: SampleScene을 열어 디버그 세션을 넣고 저장한다(이미 있으면 그대로).
        /// Unity.exe -batchmode -quit -projectPath ... -executeMethod DesertSettlement.EditorTools.DebugSessionMenu.SetupSampleScene
        /// </summary>
        public static void SetupSampleScene()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            CreateDebugSession();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[DebugSessionMenu] SampleScene 저장 완료");
        }
    }
}
