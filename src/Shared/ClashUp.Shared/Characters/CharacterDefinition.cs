using ClashUp.Shared.Abilities;
using MessagePack;

namespace ClashUp.Shared.Characters
{
    [MessagePackObject]
    public sealed class CharacterDefinition
    {
        [Key(0)] public CharacterId Id { get; init; }
        [Key(1)] public string DisplayName { get; init; } = string.Empty;
        [Key(2)] public StatBlock BaseStats { get; init; } = new();
        [Key(3)] public AbilityId AutoAttackId { get; init; }
        [Key(4)] public AbilityId ActiveAbilityId { get; init; }
    }
}
