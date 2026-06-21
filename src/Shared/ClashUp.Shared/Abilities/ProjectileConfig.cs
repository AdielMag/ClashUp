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

        // Area-of-effect explosion on impact. 0 = single-target (direct hit only); >0 = detonate in a
        // circle at the impact point applying AoeAmount to everyone in range.
        public float AoeRadius { get; init; }
        public float AoeAmount { get; init; }
        public HitboxEffect AoeEffect { get; init; }

        // Ticks the projectile lives before auto-expiring. 0 => derive from MaxRange / (Speed * dt).
        public int LifetimeTicks { get; init; }
    }
}
