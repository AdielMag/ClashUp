namespace ClashUp.Shared.Abilities
{
    public enum HitboxEffect
    {
        Damage,
        Heal,
    }

    public enum HitboxShape
    {
        Circle,
        Capsule,
        Cone,
    }

    public sealed class HitboxConfig
    {
        public HitboxEffect Effect { get; init; }
        public float Amount { get; init; }

        // Shape of the damage area. Default Circle preserves legacy behavior.
        public HitboxShape Shape { get; init; }

        // Circle: query radius. Capsule: half-width (radius around the forward segment).
        public float Radius { get; init; }

        // Capsule: forward segment length. Cone: reach.
        public float Length { get; init; }

        // Cone: full opening angle in degrees (centered on aim).
        public float Angle { get; init; }

        // Forward distance from the caster to the shape's start point.
        public float OffsetForward { get; init; }
        public int DurationTicks { get; init; } = 1;
        public int HitIntervalTicks { get; init; }
        public bool HitSelf { get; init; }
        public bool HitAllies { get; init; }
    }
}
