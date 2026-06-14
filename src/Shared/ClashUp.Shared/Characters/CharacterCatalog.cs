using System;
using System.Collections.Generic;
using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Shared.Characters
{
    public sealed class CharacterCatalog
    {
        private readonly Dictionary<string, CharacterDefinition> _byId;

        public CharacterId DefaultId { get; }

        public CharacterCatalog(CharactersConfig config)
        {
            var source = config ?? CharactersConfig.Default;
            DefaultId = new CharacterId(source.DefaultCharacterId);
            _byId = new Dictionary<string, CharacterDefinition>(StringComparer.Ordinal);
            foreach (var character in source.Characters)
                if (character?.Id.Value != null)
                    _byId[character.Id.Value] = character;
        }

        public static CharacterCatalog FromConfig(CharactersConfig config) => new(config);

        public CharacterDefinition Default => Get(DefaultId);

        public CharacterDefinition Get(CharacterId id)
        {
            if (id.Value != null && _byId.TryGetValue(id.Value, out var def))
                return def;
            return Default;
        }
    }
}
