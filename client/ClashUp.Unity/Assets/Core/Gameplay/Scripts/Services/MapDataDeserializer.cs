using ClashUp.Shared.Maps;
using Newtonsoft.Json;

namespace ClashUp.Client.Gameplay
{
    public static class MapDataDeserializer
    {
        public static MapData Deserialize(string json)
        {
            return JsonConvert.DeserializeObject<MapData>(json);
        }
    }
}
