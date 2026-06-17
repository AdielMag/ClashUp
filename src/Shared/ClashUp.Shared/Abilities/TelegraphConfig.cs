using MessagePack;

namespace ClashUp.Shared.Abilities
{
    public enum TelegraphShape
    {
        CircleAroundCaster,
        ForwardLine,
        ForwardCone,
        TargetCircle,
        Capsule,
    }

    [MessagePackObject]
    public sealed class TelegraphConfig
    {
        [Key(0)] public TelegraphShape Shape { get; init; }
        [Key(1)] public float Radius { get; init; }
        [Key(2)] public float Length { get; init; }
        [Key(3)] public float Angle { get; init; }
        [Key(4)] public int ShowDurationTicks { get; init; }

        // Full width (used by Capsule; ForwardLine falls back to a default when 0).
        [Key(5)] public float Width { get; init; }
    }
}
