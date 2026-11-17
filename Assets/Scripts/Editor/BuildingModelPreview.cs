using System;
using System.Collections.Generic;
using System.IO;
using DesertSettlement.Core.Config;
using DesertSettlement.Runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DesertSettlement.EditorTools
{
    /// <summary>
    /// 메뉴 Tools > Desert Settlement > 건물 모델 미리보기 캡처.
    /// 구운 건물 메시(<see cref="BuildingModelBaker"/>)를 프로젝트 루트의 <c>Captures/</c>에 세 장으로 찍는다.
    /// <list type="bullet">
    /// <item><c>building_models.png</c> — 열 = 시설(이미지 목록 01~16 순서, 8개씩 두 묶음), 행 = 게임 기본 시점 · 뒤(Q·E로 돌렸을 때) · 위.</item>
    /// <item><c>building_models_game.png</c> — 16개를 육각 타일 위에 4줄로(목록 순서), 게임 카메라 각도.</item>
    /// <item><c>building_levels.png</c> — 시설 넷의 L1·L2·L3 크기(행 = 시설, 열 = 레벨).</item>
    /// </list>
    /// 잘린 모양·방향·텍스처·타일 안 크기를 한눈에 확인하는 용도. 상태 색·등불·비계는 없다(게임 화면 캡처는 ScreenCaptureTests).
    /// </summary>
    public static class BuildingModelPreview
    {
        private static readonly string[] Kinds =
        {
            BuildingKinds.Well, BuildingKinds.WaterTank, BuildingKinds.Farm, BuildingKinds.Orchard,
            BuildingKinds.Pasture, BuildingKinds.HerbFarm, BuildingKinds.Quarry, BuildingKinds.Mine,
            BuildingKinds.Mill, BuildingKinds.Brewery, BuildingKinds.Winery, BuildingKinds.House,
            BuildingKinds.Warehouse, BuildingKinds.Market, BuildingKinds.Hospital, BuildingKinds.AuctionHall,
        };

        private static readonly string[] LevelKinds = { BuildingKinds.Well, BuildingKinds.House, BuildingKinds.Mill, BuildingKinds.AuctionHall };

        private const float GamePitch = 50f;   // StrategyCameraRig 기본 피치와 맞춘 값
        private const float TileGap = 0.96f;   // SettlementView 기본 타일 간격과 맞춘 값
        private static readonly Color Desert = new Color(0.86f, 0.74f, 0.52f);

        [MenuItem("Tools/Desert Settlement/건물 모델 미리보기 캡처")]
        public static void Capture()
        {
            var stage = new Stage();
            try
            {
                CaptureViews(stage);
                CaptureGameGrid(stage);
                CaptureLevels(stage);
            }
            finally { stage.Dispose(); }
        }

        /// <summary>배치 모드용: 굽고 바로 미리보기.</summary>
        public static void BakeAndCapture()
        {
            BuildingModelBaker.Bake();
            AssetDatabase.Refresh();
            Capture();
        }

        private static void CaptureViews(Stage stage)
        {
            const int cell = 360;
            (float yaw, float pitch)[] views = { (0f, GamePitch), (180f, GamePitch), (0f, 90f) };
            var sheet = new Texture2D(cell * 8, cell * views.Length * 2, TextureFormat.RGB24, false);
            for (int k = 0; k < Kinds.Length; k++)
            {
                GameObject go = stage.Building(Kinds[k], Vector3.zero, 1f);
                GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // 타일 반지름 1 비교용 원판
                disc.transform.SetParent(go.transform, false);
                disc.transform.localScale = new Vector3(2f, 0.005f, 2f);
                disc.transform.localPosition = new Vector3(0f, -0.006f, 0f);
                for (int v = 0; v < views.Length; v++)
                {
                    stage.Aim(new Vector3(0f, 0.35f, 0f), views[v].yaw, views[v].pitch, 1.15f);
                    Texture2D shot = stage.Render(cell, cell);
                    int x = (k % 8) * cell, y = sheet.height - ((k / 8) * views.Length + v + 1) * cell;
                    sheet.SetPixels(x, y, cell, cell, shot.GetPixels());
                    Object.DestroyImmediate(shot);
                }
                Object.DestroyImmediate(go);
            }
            Save(sheet, "building_models.png");
        }

        private static void CaptureGameGrid(Stage stage)
        {
            var placed = new List<GameObject>();
            for (int k = 0; k < Kinds.Length; k++)
            {
                Vector3 at = HexCenter(k % 4, k / 4);
                placed.Add(stage.Tile(at));
                placed.Add(stage.Building(Kinds[k], at, 1f));
            }
            stage.Aim(Middle(placed) + Vector3.up * 0.3f, 0f, GamePitch, 4.2f);
            Save(stage.Render(1920, 1080), "building_models_game.png");
            placed.ForEach(Object.DestroyImmediate);
        }

        private static void CaptureLevels(Stage stage)
        {
            var placed = new List<GameObject>();
            for (int row = 0; row < LevelKinds.Length; row++)
            {
                for (int level = 1; level <= 3; level++)
                {
                    Vector3 at = HexCenter(level - 1, row);
                    placed.Add(stage.Tile(at));
                    placed.Add(stage.Building(LevelKinds[row], at, 1f + SettlementView.DefaultUpgradeScalePerLevel * (level - 1)));
                }
            }
            stage.Aim(Middle(placed) + Vector3.up * 0.3f, 0f, GamePitch, 3.6f);
            Save(stage.Render(1440, 1080), "building_levels.png");
            placed.ForEach(Object.DestroyImmediate);
        }

        /// <summary>pointy-top 육각 격자(반지름 1)의 칸 가운데. 홀수 줄은 반 칸 오른쪽, 다음 줄은 화면 아래(−z)로.</summary>
        private static Vector3 HexCenter(float col, float row) =>
            new Vector3(Mathf.Sqrt(3f) * (col + 0.5f * (Mathf.FloorToInt(row) % 2)), 0f, -1.5f * row);

        private static Vector3 Middle(List<GameObject> placed)
        {
            Vector3 sum = Vector3.zero;
            foreach (GameObject go in placed) sum += go.transform.position;
            return sum / placed.Count;
        }

        private static void Save(Texture2D image, string name)
        {
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Captures", name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            Debug.Log("[BuildingModelPreview] " + path);
        }

        /// <summary>임시 카메라·조명과 찍을 오브젝트들(끝나면 모두 지운다).</summary>
        private sealed class Stage : IDisposable
        {
            private readonly GameObject _root = new GameObject("Preview Root");
            private readonly Camera _camera;
            private readonly Material _atlas = Resources.Load<Material>(FacilityModels.AtlasPath);

            public Stage()
            {
                _camera = new GameObject("Preview Camera").AddComponent<Camera>();
                _camera.transform.SetParent(_root.transform);
                _camera.orthographic = true;
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(0.78f, 0.66f, 0.46f);
                var sun = new GameObject("Preview Sun").AddComponent<Light>();
                sun.transform.SetParent(_root.transform);
                sun.type = LightType.Directional;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);
            }

            public GameObject Building(string kind, Vector3 at, float scale)
            {
                var go = new GameObject(kind);
                go.transform.SetParent(_root.transform);
                go.transform.position = at;
                go.transform.localScale = Vector3.one * scale;
                go.AddComponent<MeshFilter>().sharedMesh = FacilityModels.MeshOf(kind);
                go.AddComponent<MeshRenderer>().sharedMaterial = _atlas;
                return go;
            }

            public GameObject Tile(Vector3 at)
            {
                GameObject tile = WorldVisuals.HexTile("Tile", _root.transform, Desert);
                tile.transform.position = at + Vector3.down * 0.002f;
                tile.transform.localScale = new Vector3(TileGap, 1f, TileGap);
                return tile;
            }

            public void Aim(Vector3 target, float yaw, float pitch, float size)
            {
                Quaternion look = Quaternion.Euler(pitch, yaw, 0f);
                _camera.transform.SetPositionAndRotation(target - look * Vector3.forward * 20f, look);
                _camera.orthographicSize = size;
            }

            public Texture2D Render(int width, int height)
            {
                var rt = new RenderTexture(width, height, 24) { antiAliasing = 4 };
                _camera.targetTexture = rt;
                _camera.Render();
                RenderTexture.active = rt;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                RenderTexture.active = null;
                _camera.targetTexture = null;
                Object.DestroyImmediate(rt);
                return image;
            }

            public void Dispose() => Object.DestroyImmediate(_root);
        }
    }
}
