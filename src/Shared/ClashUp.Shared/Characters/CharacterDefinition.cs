namespace ClashUp.Shared.Characters
{
    public sealed class CharacterDefinition
    {
        public CharacterId Id { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public StatBlock BaseStats { get; init; } = new();
    }
}
