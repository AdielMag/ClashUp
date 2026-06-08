using UnityEngine;

namespace ClashUp.Client.Gameplay
{
    [CreateAssetMenu(fileName = "MapRegistry", menuName = "ClashUp/Map Registry")]
    public sealed class MapRegistry : ScriptableObject
    {
        [SerializeField] private SerializedDictionary<string, MapDefinition> _maps = new();

        public MapDefinition Get(string mapId)
        {
            return _maps.TryGetValue(mapId, out var def) ? def : null;
        }
    }
}
