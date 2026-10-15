using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Jay.FileIO.JsonDataModule
{
    /// <summary>
    /// 여러 <see cref="IJsonDataModule"/>을 Key별로 모아 하나의 JSON 파일로 round-trip 하는 컨테이너.
    /// 각 모듈은 자신의 Key 구간(JToken)만 책임지고, 이 컨테이너가 전체 JObject 조립/분배를 맡는다.
    /// (기존엔 모듈 부품만 있고 이를 묶어 파일로 저장/로드하는 조립기가 없었음)
    ///
    /// 사용 예:
    ///   var file  = new JsonDataFile();
    ///   var coins = new JsonDataModule&lt;int&gt;("coins", 0);
    ///   var inv   = new ListDataModule&lt;string&gt;("inventory");
    ///   file.Register(coins).Register(inv);
    ///
    ///   file.Load(path);   // 파일 → 각 모듈 (없으면 기본값 유지)
    ///   coins.Value;       // 사용
    ///   file.Save(path);   // 각 모듈 → 파일
    /// </summary>
    public class JsonDataFile
    {
        private readonly Dictionary<string, IJsonDataModule> modules = new Dictionary<string, IJsonDataModule>();

        public IReadOnlyDictionary<string, IJsonDataModule> Modules => modules;

        /// <summary>모듈을 Key로 등록한다. 같은 Key면 덮어쓴다. (체이닝 가능)</summary>
        public JsonDataFile Register(IJsonDataModule module)
        {
            if (module == null)
            {
                Debug.LogError("[JsonDataFile] module이 null");
                return this;
            }
            if (string.IsNullOrEmpty(module.Key))
            {
                Debug.LogError("[JsonDataFile] module.Key가 비어있음");
                return this;
            }

            modules[module.Key] = module;
            return this;
        }

        public bool TryGet(string key, out IJsonDataModule module) => modules.TryGetValue(key, out module);

        /// <summary>
        /// 메모리상의 JObject를 각 모듈에 분배(OnLoad)한다. 파일 IO 없이 조립/테스트에 사용.
        /// 해당 Key가 없으면 그 모듈에는 손대지 않아 기본값이 유지된다.
        /// </summary>
        public void LoadFrom(JObject root)
        {
            if (root == null) return;

            foreach (var module in modules.Values)
            {
                if (root.TryGetValue(module.Key, out var segment))
                {
                    module.OnLoad(segment);
                }
            }
        }

        /// <summary>등록된 모든 모듈을 모아 하나의 JObject로 조립(OnSave)한다.</summary>
        public JObject SaveTo()
        {
            var root = new JObject();
            foreach (var module in modules.Values)
            {
                root[module.Key] = module.OnSave();
            }
            return root;
        }

        /// <summary>파일을 읽어 각 모듈에 분배한다. 파일이 없으면 기본값을 유지하고 false 반환.</summary>
        public bool Load(string filePath)
        {
            string fullPath = Resolve(filePath);
            if (File.Exists(fullPath) == false)
            {
                Debug.LogWarning($"[JsonDataFile] 파일이 존재하지 않음(기본값 유지): {fullPath}");
                return false;
            }

            try
            {
                var root = JObject.Parse(File.ReadAllText(fullPath));
                LoadFrom(root);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[JsonDataFile] 로드 실패: {fullPath}\n{ex}");
                return false;
            }
        }

        /// <summary>등록된 모든 모듈을 모아 파일로 저장한다. 파일명에 .json이 없으면 자동으로 붙는다.</summary>
        public bool Save(string filePath, bool prettyPrint = true)
        {
            string fullPath = Resolve(filePath);
            try
            {
                var directory = Path.GetDirectoryName(fullPath);
                if (string.IsNullOrEmpty(directory) == false && Directory.Exists(directory) == false)
                {
                    Directory.CreateDirectory(directory);
                }

                var formatting = prettyPrint ? Formatting.Indented : Formatting.None;
                File.WriteAllText(fullPath, SaveTo().ToString(formatting));
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[JsonDataFile] 저장 실패: {fullPath}\n{ex}");
                return false;
            }
        }

        private static string Resolve(string filePath)
            => filePath.EndsWith(".json") ? filePath : $"{filePath}.json";
    }
}
