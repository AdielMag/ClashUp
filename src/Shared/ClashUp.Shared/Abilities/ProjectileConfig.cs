namespace ClashUp.Shared.Abilities
{
    public sealed class ProjectileConfig
    {
        public float Speed { get; init; }
        public float Radius { get; init; } = 0.2f;
        public float MaxRange { get; init; }
        public int MaxPierceCount { get; init; }
        public HitboxEffect OnHitEffect { get; init; }
        public float OnHitAmount { get; init; }
    }
}
