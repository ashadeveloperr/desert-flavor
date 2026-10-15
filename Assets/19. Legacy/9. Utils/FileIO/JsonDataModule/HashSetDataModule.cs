using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Jay.FileIO.JsonDataModule
{
    public class HashSetDataModule<T> : IJsonDataModule
    {
        public string Key { get; private set; }
        public HashSet<T> Value { get; private set; }

        public HashSetDataModule(string key, HashSet<T> defaultValue = null)
        {
            Key = key;
            Value = defaultValue ?? new HashSet<T>();
        }

        public void OnLoad(JToken dataSegment)
        {
            if (dataSegment != null && dataSegment.Type != JTokenType.Null)
            {
                Value = dataSegment.ToObject<HashSet<T>>();
            }
        }

        public JToken OnSave()
        {
            return JToken.FromObject(Value);
        }
    }
}