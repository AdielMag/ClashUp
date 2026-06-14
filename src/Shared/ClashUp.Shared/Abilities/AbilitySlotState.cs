namespace ClashUp.Shared.Abilities
{
    public sealed class AbilitySlotState
    {
        public AbilityId AbilityId { get; set; }
        public int CooldownRemaining { get; set; }
        public bool IsActive { get; set; }
    }
}
