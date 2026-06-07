using System;
using System.Collections.Generic;

namespace ClashUp.Shared.Characters
{
    public static class CharacterRegistry
    {
        public static CharacterDefinition Default { get; } = new()
        {
            Id = new CharacterId("brawler"),
            DisplayName = "Brawler",
            BaseStats = new StatBlock
            {
                MaxHealth = 100f,
                Damage = 10f,
            },
        };

        private static readonly Dictionary<string, CharacterDefinition> _byId = new(StringComparer.Ordinal)
        {
            [Default.Id.Value] = Default,
        };

        public static IReadOnlyList<CharacterDefinition> All { get; } = new[] { Default };

        public static CharacterDefinition Get(CharacterId id)
        {
            if (_byId.TryGetValue(id.Value, out var def)) return def;
            throw new KeyNotFoundException($"Unknown character: {id.Value}");
        }
    }
}
