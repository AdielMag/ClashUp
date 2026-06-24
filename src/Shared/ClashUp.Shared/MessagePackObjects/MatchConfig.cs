using MessagePack;

namespace ClashUp.Shared.MessagePackObjects
{
    [MessagePackObject]
    public sealed class MatchConfig
    {
        [Key(0)] public int NumberOfTeams { get; init; } = 2;
        [Key(1)] public int TeamSize { get; init; } = 1;
        [Key(2)] public int DurationSeconds { get; init; } = 20;
        [Key(3)] public string ObjectiveType { get; init; } = "survival";
        [Key(4)] public string MapId { get; init; } = "arena_tdm";

        /// <summary>
        /// When true, the matchmaker fills the remaining empty slots with AI bots once
        /// <see cref="BotFillWaitSeconds"/> has elapsed since the oldest waiting player queued.
        /// Off by default — only meaningful for modes with NumberOfTeams &gt;= 2 (bots are
        /// enemies only across teams).
        /// </summary>
        [Key(5)] public bool FillWithBots { get; init; } = false;

        /// <summary>How long (seconds) to wait for real players before forming a bot-filled match.</summary>
        [Key(6)] public int BotFillWaitSeconds { get; init; } = 15;

        /// <summary>Minimum real players that must be queued before a bot-filled match is created.</summary>
        [Key(7)] public int MinRealPlayers { get; init; } = 1;
    }
}
