using System.Collections.Generic;
using ClashUp.Shared.Abilities;
using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Client.Gameplay
{
    public sealed class MatchAbilitiesHolder
    {
        private readonly Dictionary<string, AbilityClientInfo> _byId = new();

        public void Initialize(AbilitiesConfig config)
        {
            _byId.Clear();
            var source = config ?? AbilitiesConfig.Default;
            if (source.Abilities == null) return;
            foreach (var info in source.Abilities)
                if (info?.Id.Value != null)
                    _byId[info.Id.Value] = info;
        }

        public int Count => _byId.Count;

        public bool TryGet(AbilityId id, out AbilityClientInfo info)
        {
            if (id.Value == null) { info = default; return false; }
            return _byId.TryGetValue(id.Value, out info);
        }
    }
}
