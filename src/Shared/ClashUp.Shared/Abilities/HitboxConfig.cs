namespace ClashUp.Shared.Abilities
{
    public enum HitboxEffect
    {
        Damage,
        Heal,
    }

    public sealed class HitboxConfig
    {
        public HitboxEffect Effect { get; init; }
        public float Amount { get; init; }
        public float Radius { get; init; }
        public float OffsetForward { get; init; }
        public int DurationTicks { get; init; } = 1;
        public int HitIntervalTicks { get; init; }
        public bool HitSelf { get; init; }
        public bool HitAllies { get; init; }
    }
}
