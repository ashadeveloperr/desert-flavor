using Newtonsoft.Json.Linq;

namespace Jay.FileIO.JsonDataModule
{
    public interface IJsonDataModule
    {
        string Key { get; }

        void OnLoad(JToken dataSegment);
        JToken OnSave();
    }
}