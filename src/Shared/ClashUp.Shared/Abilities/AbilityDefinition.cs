namespace ClashUp.Shared.Abilities
{
    public sealed class AbilityDefinition
    {
        public AbilityId Id { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public int CooldownTicks { get; init; }
        public int ButtonIndex { get; init; }
        public TriggerMode TriggerMode { get; init; }
        public CastMode CastMode { get; init; }
        public float AutoRange { get; init; }
        public TelegraphConfig? Telegraph { get; init; }
        public AbilityNode? RootNode { get; init; }
        public string? VisualConfigGuid { get; init; }
    }
}
