namespace ClashUp.Server.Services.Matchmaking;

public sealed class MatchmakingOptions
{
    public const string SectionName = "Matchmaking";

    public int MatchSize { get; init; } = 2;

    public int DefaultTickRateHz { get; init; } = 30;

    /// <summary>How often the matchmaker drains the queue, in milliseconds.</summary>
    public int DrainIntervalMs { get; init; } = 250;
}
