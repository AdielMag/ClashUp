namespace ClashUp.Shared.Abilities
{
    public enum TelegraphShape
    {
        CircleAroundCaster,
        ForwardLine,
        ForwardCone,
        TargetCircle,
    }

    public sealed class TelegraphConfig
    {
        public TelegraphShape Shape { get; init; }
        public float Radius { get; init; }
        public float Length { get; init; }
        public float Angle { get; init; }
        public int ShowDurationTicks { get; init; }
    }
}
