namespace ClashUp.Shared.Abilities
{
    public enum CastMode
    {
        Instant = 0,
        Aimed = 1,

        // Joystick direction + distance-from-center resolve a world target point
        // (caster + dir × magnitude × maxDist). The cast originates AT that point, not the caster.
        TargetPoint = 2,
    }
}
