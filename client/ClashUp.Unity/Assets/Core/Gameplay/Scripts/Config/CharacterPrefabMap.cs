using System;
using ClashUp.Shared.Characters;
using UnityEngine;

namespace ClashUp.Client.Gameplay
{
    [CreateAssetMenu(fileName = "CharacterPrefabMap", menuName = "ClashUp/Character Prefab Map")]
    public sealed class CharacterPrefabMap : ScriptableObject
    {
        [Serializable]
        private struct Entry
        {
            public string CharacterId;
            public GameObject Prefab;
        }

        [SerializeField] private Entry[] _entries;
        [SerializeField] private GameObject _fallbackPrefab;

        public GameObject Get(CharacterId id)
        {
            foreach (var entry in _entries)
            {
                if (string.Equals(entry.CharacterId, id.Value, StringComparison.Ordinal))
                    return entry.Prefab;
            }
            return _fallbackPrefab;
        }
    }
}
