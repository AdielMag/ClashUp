using System.Collections.Generic;
using ClashUp.Shared.Abilities;
using ClashUp.Shared.Characters;
using MessagePack;

namespace ClashUp.Shared.MessagePackObjects
{
    [MessagePackObject]
    public sealed class CharactersConfig
    {
        [Key(0)] public string DefaultCharacterId { get; init; } = "brawler";
        [Key(1)] public IReadOnlyList<CharacterDefinition> Characters { get; init; } = System.Array.Empty<CharacterDefinition>();

        public static CharactersConfig Default { get; } = new()
        {
            DefaultCharacterId = "brawler",
            Characters = new[]
            {
                new CharacterDefinition
                {
                    Id = new CharacterId("brawler"),
                    DisplayName = "Brawler",
                    BaseStats = new StatBlock
                    {
                        MaxHealth = 100f,
                        Damage = 10f,
                        MoveSpeed = 5f,
                    },
                    AutoAttackId = new AbilityId("brawler_punch"),
                    ActiveAbilityId = new AbilityId("brawler_charge"),
                },
                new CharacterDefinition
                {
                    Id = new CharacterId("mage"),
                    DisplayName = "Mage",
                    BaseStats = new StatBlock
                    {
                        MaxHealth = 70f,
                        Damage = 8f,
                        MoveSpeed = 4.5f,
                    },
                    AutoAttackId = new AbilityId("mage_bolt"),
                    ActiveAbilityId = new AbilityId("mage_blast"),
                },
            },
        };
    }
}
