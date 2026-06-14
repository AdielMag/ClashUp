using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClashUp.Client.Gameplay
{
    [CreateAssetMenu(fileName = "AbilityVisualRegistry", menuName = "ClashUp/Ability Visual Registry")]
    public sealed class AbilityVisualRegistry : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string Guid;
            public string AbilityId;
            public AbilityVisualConfig Config;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

        private Dictionary<string, AbilityVisualConfig> _byGuid;
        private Dictionary<string, AbilityVisualConfig> _byAbilityId;

        public AbilityVisualConfig GetByGuid(string guid)
        {
            if (_byGuid == null) BuildLookup();
            return _byGuid.TryGetValue(guid, out var c) ? c : null;
        }

        public AbilityVisualConfig GetByAbilityId(string abilityId)
        {
            if (_byAbilityId == null) BuildLookup();
            return _byAbilityId.TryGetValue(abilityId, out var c) ? c : null;
        }

        private void BuildLookup()
        {
            _byGuid = new Dictionary<string, AbilityVisualConfig>(StringComparer.Ordinal);
            _byAbilityId = new Dictionary<string, AbilityVisualConfig>(StringComparer.Ordinal);
            foreach (var e in _entries)
            {
                if (e.Config == null) continue;
                if (!string.IsNullOrEmpty(e.Guid)) _byGuid[e.Guid] = e.Config;
                if (!string.IsNullOrEmpty(e.AbilityId)) _byAbilityId[e.AbilityId] = e.Config;
            }
        }

        private void OnValidate()
        {
            _byGuid = null;
            _byAbilityId = null;
        }

        public Entry[] Entries => _entries;
    }
}
