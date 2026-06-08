using UnityEngine;

namespace ClashUp.Client.Gameplay
{
    [CreateAssetMenu(fileName = "MapDefinition", menuName = "ClashUp/Map Definition")]
    public sealed class MapDefinition : ScriptableObject
    {
        [SerializeField] private string _mapId;
        [SerializeField] private string _displayName;
        [SerializeField] private TextAsset _bakedMapJson;
        [SerializeField] private GameObject _visualPrefab;

        public string MapId => _mapId;
        public string DisplayName => _displayName;
        public TextAsset BakedMapJson => _bakedMapJson;
        public GameObject VisualPrefab => _visualPrefab;
    }
}
