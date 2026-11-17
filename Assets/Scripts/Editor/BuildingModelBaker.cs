using System.Collections.Generic;
using System.IO;
using System.Linq;
using DesertSettlement.Core.Config;
using DesertSettlement.Runtime;
using UnityEditor;
using UnityEngine;

namespace DesertSettlement.EditorTools
{
    /// <summary>
    /// 메뉴 Tools > Desert Settlement > 건물 모델 굽기.
    /// 원본 FBX(16개 건물이 4×4 선반처럼 놓인 메시 하나)를 건물별 메시로 잘라 <c>Resources/Buildings/&lt;시설 id&gt;.asset</c>에 저장한다.
    /// 런타임(<see cref="FacilityModels"/>)은 이 메시와 <c>Resources/Buildings/BuildingAtlas.mat</c>(원본에서 꺼낸 텍스처),
    /// 폐쇄 시설용 <c>BuildingAtlasMuted.mat</c>(채도 낮춘 사본)을 쓴다.
    /// <para>자르는 법: 메시를 이어진 덩어리로 나누고, 큰 덩어리 16개를 높이(행)·가로(열) 순서로 <see cref="Grid"/>에 맞춘다.
    /// 작은 조각은 가장 가까운 큰 덩어리에 붙인다. 각 건물은 평면 상자의 가운데가 원점, 바닥이 y = 0,
    /// 건물 전체가 반지름 <see cref="FootprintRadius"/>(타일 반지름 1 기준) 안에 들어오게 줄이고, 입구가 −z(화면 아래)를 보게 돌린다.</para>
    /// </summary>
    public static class BuildingModelBaker
    {
        public const string SourcePath = "Assets/Art/Buildings/IsometricVillage.fbx";
        public const string TextureFolder = "Assets/Art/Buildings/Textures";
        public const string OutputFolder = "Assets/Resources/Buildings";

        /// <summary>원본 시트의 칸 → 시설 id. 위 행부터, 각 행은 정면에서 본 왼쪽부터(이미지 목록 01~16 순서).</summary>
        private static readonly string[,] Grid =
        {
            { BuildingKinds.Well, BuildingKinds.WaterTank, BuildingKinds.Farm, BuildingKinds.Orchard },
            { BuildingKinds.Pasture, BuildingKinds.HerbFarm, BuildingKinds.Quarry, BuildingKinds.Mine },
            { BuildingKinds.Mill, BuildingKinds.Brewery, BuildingKinds.Winery, BuildingKinds.House },
            { BuildingKinds.Warehouse, BuildingKinds.Market, BuildingKinds.Hospital, BuildingKinds.AuctionHall },
        };

        /// <summary>건물이 들어갈 반지름(타일 반지름 1 기준). 레벨이 오르면 런타임이 더 키우므로 L3에서도 이웃 타일을 넘지 않게 작게 둔다.</summary>
        private const float FootprintRadius = 0.75f;

        /// <summary>원본은 입구가 +z를 본다. 게임은 입구가 −z이므로 180° 돌린다. 건물별로 더 돌릴 것이 있으면 여기에.</summary>
        private const float BaseYaw = 180f;
        private static readonly Dictionary<string, float> ExtraYaw = new Dictionary<string, float>();

        [MenuItem("Tools/Desert Settlement/건물 모델 굽기")]
        public static void Bake()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            if (source == null) { Debug.LogError("[BuildingModelBaker] 원본이 없습니다: " + SourcePath); return; }
            EnsureTextures();
            MeshFilter filter = source.GetComponentInChildren<MeshFilter>();
            List<Piece> buildings = Split(filter.sharedMesh, filter.transform.localToWorldMatrix);
            if (buildings.Count != Grid.Length)
            {
                Debug.LogError($"[BuildingModelBaker] 건물 덩어리가 {buildings.Count}개입니다(기대 {Grid.Length}).");
                return;
            }

            Directory.CreateDirectory(OutputFolder);
            Material atlas = BuildMaterial("BuildingAtlas", $"{TextureFolder}/base_color.png");
            BuildMaterial("BuildingAtlasMuted", WriteMutedTexture());
            for (int i = 0; i < buildings.Count; i++)
            {
                string kind = Grid[i / Grid.GetLength(1), i % Grid.GetLength(1)];
                Mesh mesh = Normalize(buildings[i], filter.sharedMesh, kind);
                SaveAsset(mesh, $"{OutputFolder}/{kind}.asset");
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[BuildingModelBaker] 건물 {buildings.Count}개를 {OutputFolder}에 구웠습니다. 머티리얼 {atlas.name}");
        }

        /// <summary>건물 하나 = 원본 메시의 삼각형 묶음(원본 좌표계, 미터).</summary>
        private sealed class Piece
        {
            public readonly List<int> Triangles = new List<int>(); // 원본 triangles 배열의 시작 인덱스
            public Vector3[] World, WorldNormals;                 // 원본 꼭짓점·노멀(루트 회전 적용)
            public Bounds Bounds;
        }

        // ── 자르기 ───────────────────────────────────────────

        private static List<Piece> Split(Mesh mesh, Matrix4x4 toWorld)
        {
            Vector3[] world = mesh.vertices.Select(p => toWorld.MultiplyPoint3x4(p)).ToArray();
            Vector3[] worldNormals = mesh.normals.Select(n => toWorld.MultiplyVector(n).normalized).ToArray();
            int[] tris = mesh.triangles;

            // 같은 위치의 꼭짓점(UV 경계에서 갈라진 것)을 한 점으로 보고 이어진 덩어리를 찾는다.
            var weldIds = new Dictionary<Vector3Int, int>();
            int[] weld = new int[world.Length];
            for (int i = 0; i < world.Length; i++)
            {
                Vector3Int key = Vector3Int.RoundToInt(world[i] * 1e5f);
                if (!weldIds.TryGetValue(key, out int id)) weldIds[key] = id = weldIds.Count;
                weld[i] = id;
            }
            int[] parent = Enumerable.Range(0, weldIds.Count).ToArray();
            int Root(int a) { while (parent[a] != a) a = parent[a] = parent[parent[a]]; return a; }
            for (int t = 0; t < tris.Length; t += 3)
            {
                int a = Root(weld[tris[t]]);
                parent[Root(weld[tris[t + 1]])] = a;
                parent[Root(weld[tris[t + 2]])] = a;
            }
            var groups = new Dictionary<int, Piece>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                int r = Root(weld[tris[t]]);
                if (!groups.TryGetValue(r, out Piece p)) groups[r] = p = new Piece { World = world, WorldNormals = worldNormals, Bounds = new Bounds(world[tris[t]], Vector3.zero) };
                p.Triangles.Add(t);
                for (int k = 0; k < 3; k++) p.Bounds.Encapsulate(world[tris[t + k]]);
            }

            // 큰 덩어리 16개 = 건물. 나머지 조각은 가운데가 가장 가까운 건물 상자에 붙인다.
            List<Piece> sorted = groups.Values.OrderByDescending(p => p.Triangles.Count).ToList();
            List<Piece> buildings = sorted.Take(Grid.Length).ToList();
            foreach (Piece crumb in sorted.Skip(Grid.Length))
            {
                Piece owner = buildings.OrderBy(b => b.Bounds.SqrDistance(crumb.Bounds.center)).First();
                owner.Triangles.AddRange(crumb.Triangles);
                owner.Bounds.Encapsulate(crumb.Bounds);
            }

            // 행: 높은 것부터. 열: 원본 정면(+z 쪽)에서 볼 때 왼쪽 = +x 쪽부터.
            int cols = Grid.GetLength(1);
            return buildings.OrderByDescending(b => b.Bounds.center.y).Select((b, i) => (b, row: i / cols))
                .GroupBy(x => x.row).OrderBy(g => g.Key)
                .SelectMany(g => g.Select(x => x.b).OrderByDescending(b => b.Bounds.center.x)).ToList();
        }

        // ── 정규화(상자 가운데 = 원점, 바닥 = 0, 반지름 맞춤, 입구 −z) ─────────

        private static Mesh Normalize(Piece piece, Mesh source, string kind)
        {
            Vector2[] srcUv = source.uv;
            int[] srcTris = source.triangles;
            float bottom = piece.Bounds.min.y;

            var used = new Dictionary<int, int>();
            foreach (int t in piece.Triangles)
                for (int k = 0; k < 3; k++)
                    if (!used.ContainsKey(srcTris[t + k])) used[srcTris[t + k]] = used.Count;
            Vector3 center = new Vector3(piece.Bounds.center.x, bottom, piece.Bounds.center.z);
            // 받침 밖으로 튀어나온 부분(풍차 날개 등)까지 포함해 건물 전체가 이 반지름 안에 들어오게 한다.
            float radius = used.Keys.Max(i => new Vector2(piece.World[i].x - center.x, piece.World[i].z - center.z).magnitude);
            float scale = FootprintRadius / Mathf.Max(1e-4f, radius);
            Debug.Log($"[BuildingModelBaker] {kind}: 원본 상자 {piece.Bounds.size}, 반지름 {radius:0.000}, 배율 {scale:0.00}, 삼각형 {piece.Triangles.Count}");
            Quaternion yaw = Quaternion.Euler(0f, BaseYaw + (ExtraYaw.TryGetValue(kind, out float extra) ? extra : 0f), 0f);

            var vertices = new Vector3[used.Count];
            var normals = new Vector3[used.Count];
            var uv = new Vector2[used.Count];
            foreach (KeyValuePair<int, int> kv in used)
            {
                vertices[kv.Value] = yaw * ((piece.World[kv.Key] - center) * scale);
                normals[kv.Value] = yaw * piece.WorldNormals[kv.Key];
                uv[kv.Value] = srcUv[kv.Key];
            }
            var triangles = new int[piece.Triangles.Count * 3];
            int n = 0;
            foreach (int t in piece.Triangles)
                for (int k = 0; k < 3; k++) triangles[n++] = used[srcTris[t + k]];

            var mesh = new Mesh { name = kind };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ── 텍스처·머티리얼 ───────────────────────────────────

        /// <summary>FBX에 들어 있는 텍스처를 꺼내고(처음 한 번), 노멀 맵을 노멀 맵으로 임포트한다.</summary>
        private static void EnsureTextures()
        {
            string normalPath = $"{TextureFolder}/normal.png";
            if (!File.Exists(normalPath))
            {
                Directory.CreateDirectory(TextureFolder);
                ((ModelImporter)AssetImporter.GetAtPath(SourcePath)).ExtractTextures(TextureFolder);
                AssetDatabase.Refresh();
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(normalPath);
            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
        }

        /// <summary>
        /// 폐쇄 시설용 채도 낮춘 텍스처(<see cref="FacilityModels.MutedSaturation"/>만큼 색을 남김)를 base_color에서 만든다.
        /// URP Lit에는 채도 조절이 없어서 텍스처 사본을 두고 머티리얼을 바꿔 끼운다. 굽을 때마다 다시 만든다.
        /// </summary>
        private static string WriteMutedTexture()
        {
            string path = $"{TextureFolder}/base_color_muted.jpg";
            var source = new Texture2D(2, 2);
            source.LoadImage(File.ReadAllBytes($"{TextureFolder}/base_color.png"));
            Color[] pixels = source.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                float gray = pixels[i].grayscale;
                pixels[i] = Color.Lerp(new Color(gray, gray, gray), pixels[i], FacilityModels.MutedSaturation);
            }
            source.SetPixels(pixels);
            File.WriteAllBytes(path, source.EncodeToJPG(92));
            Object.DestroyImmediate(source);
            AssetDatabase.ImportAsset(path);
            return path;
        }

        private static Material BuildMaterial(string name, string baseMapPath)
        {
            string path = $"{OutputFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                // 단색과 같은 URP Lit(빌드에 셰이더가 들어가도록 Resources 머티리얼에서 복사).
                material = new Material(Resources.Load<Material>("WorldSolid")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(baseMapPath));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/normal.png"));
            material.SetColor("_BaseColor", Color.white);
            material.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SaveAsset(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return; }
            // 같은 에셋을 다시 채운다(GUID를 지켜 참조가 끊기지 않게).
            existing.Clear();
            existing.vertices = mesh.vertices;
            existing.normals = mesh.normals;
            existing.tangents = mesh.tangents;
            existing.uv = mesh.uv;
            existing.triangles = mesh.triangles;
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
        }
    }
}
