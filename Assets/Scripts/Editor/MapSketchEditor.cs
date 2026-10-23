using System;
using System.IO;
using System.Linq;
using DesertSettlement.Core.Hex;
using DesertSettlement.Core.Progression;
using DesertSettlement.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DesertSettlement.EditorTools
{
    /// <summary>
    /// 맵 스케치 편집기. MapSketch 오브젝트를 선택하면 씬 뷰에서 지형을 칠할 수 있다(플레이 불필요).
    /// - 왼쪽 클릭/드래그: 고른 지형으로 칠하기   - Shift + 클릭/드래그: 사막으로 지우기
    /// - 숫자 1~5: 사막·수원·광맥·암반·장애물 고르기   - Alt + 드래그: 씬 뷰 회전(Unity 기본)
    /// 인스펙터에서 저장(JSON)·불러오기·전체 지우기. 저장 형식은 캠페인 맵과 같다.
    /// '생성 맵 미리보기': 난이도 D·시드로 절차 맵을 만들어 스케치에 그린다(수치는 procedural_map_config.json).
    /// </summary>
    [CustomEditor(typeof(MapSketch))]
    public sealed class MapSketchEditor : UnityEditor.Editor
    {
        private const string BrushPrefKey = "DesertSettlement.MapSketch.Brush";
        private const string SketchFolder = "Assets/Data/MapSketches";
        private const string ProceduralConfigPath = "Assets/Data/Config/procedural_map_config.json";
        private const string PreviewDPrefKey = "DesertSettlement.MapSketch.PreviewD";
        private const string PreviewSeedPrefKey = "DesertSettlement.MapSketch.PreviewSeed";

        private static readonly TerrainType[] Palette =
        {
            TerrainType.Desert, TerrainType.WaterSource, TerrainType.OreVein, TerrainType.Bedrock, TerrainType.Obstacle,
        };

        private static TerrainType Brush
        {
            get => (TerrainType)EditorPrefs.GetInt(BrushPrefKey, (int)TerrainType.WaterSource);
            set => EditorPrefs.SetInt(BrushPrefKey, (int)value);
        }

        private HexCoord? _hover;
        private string _previewInfo;

        public static string TerrainLabel(TerrainType t)
        {
            switch (t)
            {
                case TerrainType.WaterSource: return "수원";
                case TerrainType.OreVein: return "광맥";
                case TerrainType.Bedrock: return "암반";
                case TerrainType.Obstacle: return "장애물";
                default: return "사막";
            }
        }

        // ───────────── 인스펙터 ─────────────

        public override void OnInspectorGUI()
        {
            var sketch = (MapSketch)target;
            int radiusBefore = sketch.radius;

            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck() && sketch.radius != radiusBefore)
            {
                Undo.RecordObject(sketch, "Change Map Radius");
                sketch.TrimToRadius();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("붓 (숫자키 1~5)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < Palette.Length; i++)
            {
                TerrainType t = Palette[i];
                Color old = GUI.backgroundColor;
                GUI.backgroundColor = sketch.ColorOf(t);
                bool selected = Brush == t;
                if (GUILayout.Toggle(selected, $"{i + 1} {TerrainLabel(t)}", EditorStyles.miniButton) && !selected) Brush = t;
                GUI.backgroundColor = old;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("씬 뷰에서 왼쪽 클릭/드래그 = 칠하기, Shift + 클릭/드래그 = 사막으로 지우기.", MessageType.Info);

            var counts = sketch.Counts();
            EditorGUILayout.LabelField("개수", string.Join("  ", Palette.Select(t => $"{TerrainLabel(t)} {counts[t]}")));

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("JSON으로 저장")) Save(sketch);
            if (GUILayout.Button("JSON 불러오기")) Load(sketch);
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("전체 사막으로 지우기") &&
                EditorUtility.DisplayDialog("맵 스케치", "모든 타일을 사막으로 되돌릴까요?", "지우기", "취소"))
            {
                Undo.RecordObject(sketch, "Clear Map Sketch");
                sketch.tiles.Clear();
                MarkDirty(sketch);
            }

            DrawProceduralPreview(sketch);
        }

        // ───────────── 생성 맵 미리보기 ─────────────

        private void DrawProceduralPreview(MapSketch sketch)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("생성 맵 미리보기", EditorStyles.boldLabel);
            float d = EditorGUILayout.Slider("난이도 D", EditorPrefs.GetFloat(PreviewDPrefKey, 0.5f), 0f, 5f);
            EditorPrefs.SetFloat(PreviewDPrefKey, d);
            EditorGUILayout.BeginHorizontal();
            int seed = EditorGUILayout.IntField("시드", EditorPrefs.GetInt(PreviewSeedPrefKey, 1));
            if (GUILayout.Button("다음 시드", GUILayout.Width(80))) seed++;
            EditorGUILayout.EndHorizontal();
            EditorPrefs.SetInt(PreviewSeedPrefKey, seed);
            EditorGUILayout.HelpBox("보유 자금 → D: D = (자금 − cashAtD0) / cashPerD. 덮어쓰기 전에 지금 스케치를 저장해 두세요.", MessageType.None);

            if (GUILayout.Button("생성해서 스케치에 그리기")) GeneratePreview(sketch, d, seed);
            if (!string.IsNullOrEmpty(_previewInfo)) EditorGUILayout.HelpBox(_previewInfo, MessageType.Info);
        }

        private void GeneratePreview(MapSketch sketch, float d, int seed)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(ProceduralConfigPath);
            if (asset == null)
            {
                _previewInfo = "설정 파일이 없습니다: " + ProceduralConfigPath;
                return;
            }
            try
            {
                ProceduralMapConfig config = ProceduralMapConfigLoader.Parse(asset.text);
                DifficultyParams p = DifficultyParams.For(d, config.Difficulty);
                GeneratedMap g = ProceduralMapGenerator.Generate(p, config.Generator, seed);
                if (g == null)
                {
                    _previewInfo = $"{p}\n규칙을 맞추는 맵을 만들지 못했습니다(시도 {config.Generator.MaxAttempts}회). 시드를 바꿔 보세요.";
                    return;
                }
                Undo.RecordObject(sketch, "Generate Map Preview");
                sketch.LoadDefinition(g.Map);
                MarkDirty(sketch);
                _previewInfo = $"{p}\n시도 {g.Attempts}회, 산맥 {g.Formations.Count(f => f.kind == FormationKind.Mountain)} · 고원 {g.Formations.Count(f => f.kind == FormationKind.Plateau)}" +
                               $"{(g.IsolatedObstacle.HasValue ? " · 독립 장애물 1" : "")}, 가장 가까운 우물–광맥 {g.NearestWellOreDistance}칸 · 수로 {g.ShortestWellOreCanalPath}변";
            }
            catch (ArgumentException e)
            {
                _previewInfo = e.Message;
            }
        }

        // ───────────── 씬 뷰 칠하기 ─────────────

        private void OnSceneGUI()
        {
            var sketch = (MapSketch)target;
            Event e = Event.current;
            int control = GUIUtility.GetControlID(FocusType.Passive);
            if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(control);

            // 숫자키로 붓 고르기.
            if (e.type == EventType.KeyDown && e.keyCode >= KeyCode.Alpha1 && e.keyCode <= KeyCode.Alpha5)
            {
                Brush = Palette[e.keyCode - KeyCode.Alpha1];
                e.Use();
                Repaint();
            }

            _hover = PickTile(sketch, e.mousePosition);
            bool painting = !e.alt && e.button == 0 && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag);
            if (painting && _hover.HasValue)
            {
                TerrainType paint = e.shift ? TerrainType.Desert : Brush;
                if (sketch.TerrainAt(_hover.Value) != paint)
                {
                    Undo.RecordObject(sketch, "Paint Map Sketch");
                    sketch.SetTerrain(_hover.Value, paint);
                    MarkDirty(sketch);
                }
                GUIUtility.hotControl = control;
                e.Use();
            }
            if (e.type == EventType.MouseUp && GUIUtility.hotControl == control) GUIUtility.hotControl = 0;
            if (e.type == EventType.MouseMove) SceneView.RepaintAll();

            DrawOverlay(sketch);
        }

        private static HexCoord? PickTile(MapSketch sketch, Vector2 guiPosition)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);
            var plane = new Plane(sketch.transform.up, sketch.transform.position);
            if (!plane.Raycast(ray, out float distance)) return null;
            HexCoord c = sketch.Layout.PointToHex(sketch.ToHexPoint(ray.GetPoint(distance)));
            return sketch.Contains(c) ? c : (HexCoord?)null;
        }

        private void DrawOverlay(MapSketch sketch)
        {
            HexLayout layout = sketch.Layout;

            if (_hover.HasValue)
            {
                Handles.color = Color.yellow;
                var corners = Enumerable.Range(0, 7)
                    .Select(i => sketch.ToWorld(layout.CornerPosition(_hover.Value, (HexCorner)(i % 6)), 0.02f)).ToArray();
                Handles.DrawAAPolyLine(4f, corners);
            }

            if (sketch.showCoordinates)
            {
                var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.black } };
                foreach (HexCoord c in HexCoord.Range(sketch.radius))
                {
                    Handles.Label(sketch.ToWorld(layout.TileCenter(c), 0.02f), $"{c.Q},{c.R}", style);
                }
            }

            Handles.BeginGUI();
            var box = new Rect(10, 10, 260, 84);
            GUI.Box(box, GUIContent.none);
            GUILayout.BeginArea(new Rect(box.x + 8, box.y + 6, box.width - 16, box.height - 12));
            GUILayout.Label($"맵 스케치: {sketch.mapName} (반경 {sketch.radius})", EditorStyles.boldLabel);
            GUILayout.Label($"붓: {TerrainLabel(Brush)}   (1~5로 변경, Shift = 지우기)");
            string hover = _hover.HasValue ? $"({_hover.Value.Q},{_hover.Value.R}) {TerrainLabel(sketch.TerrainAt(_hover.Value))}" : "-";
            GUILayout.Label("가리킨 타일: " + hover);
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private static void MarkDirty(MapSketch sketch)
        {
            EditorUtility.SetDirty(sketch);
            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(sketch.gameObject.scene);
            SceneView.RepaintAll();
        }

        // ───────────── 저장·불러오기 ─────────────

        private static void Save(MapSketch sketch)
        {
            Directory.CreateDirectory(SketchFolder);
            string path = EditorUtility.SaveFilePanelInProject("맵 스케치 저장", sketch.mapId, "json",
                "저장할 위치를 고르세요. Assets/Data/Maps에 저장하면 캠페인 맵으로도 쓰입니다.", SketchFolder);
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                File.WriteAllText(path, MapLoader.ToJson(sketch.ToDefinition()));
                AssetDatabase.ImportAsset(path);
                Debug.Log($"[MapSketch] 저장: {path}");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("맵 스케치", "저장 실패: " + ex.Message, "확인");
            }
        }

        private static void Load(MapSketch sketch)
        {
            string path = EditorUtility.OpenFilePanel("맵 JSON 불러오기", SketchFolder, "json");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                MapDefinition map = MapLoader.Parse(File.ReadAllText(path));
                Undo.RecordObject(sketch, "Load Map Sketch");
                sketch.LoadDefinition(map);
                MarkDirty(sketch);
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("맵 스케치", "불러오기 실패: " + ex.Message, "확인");
            }
        }

        // ───────────── 씬 메뉴 ─────────────

        public const string ScenePath = "Assets/Scenes/MapSketch.unity";

        [MenuItem("Tools/Desert Settlement/맵 스케치 씬 열기")]
        public static void OpenSketchScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) CreateSketchScene();
            EditorSceneManager.OpenScene(ScenePath);
            var sketch = UnityEngine.Object.FindAnyObjectByType<MapSketch>();
            if (sketch == null) return;
            Selection.activeObject = sketch.gameObject;
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                view.orthographic = true;
                view.LookAt(sketch.transform.position, Quaternion.Euler(90f, 0f, 0f), sketch.tileSize * (sketch.radius + 1) * 2.2f);
            }
        }

        /// <summary>맵 스케치 씬을 새로 만든다(batch mode에서도 사용). 이미 있으면 덮어쓰지 않는다.</summary>
        public static void CreateSketchScene()
        {
            if (File.Exists(ScenePath)) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var go = new GameObject("MapSketch");
            go.AddComponent<MapSketch>();
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.transform.SetPositionAndRotation(new Vector3(0f, 12f, 0f), Quaternion.Euler(90f, 0f, 0f));
                cam.orthographic = true;
                cam.orthographicSize = 6f;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            Directory.CreateDirectory(SketchFolder);
            AssetDatabase.Refresh();
            Debug.Log($"[MapSketch] 씬 생성: {ScenePath}");
        }
    }
}
