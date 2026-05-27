using System.Collections.Generic;
using MessagePack;

namespace ClashUp.Shared.MessagePackObjects
{
    /// <summary>
    /// Services → GameServer payload that asks a GS to host a new match.
    /// </summary>
    [MessagePackObject]
    public sealed class MatchProvision
    {
        [Key(0)] public MatchId MatchId { get; init; }
        [Key(1)] public IReadOnlyList<PlayerId> Players { get; init; } = System.Array.Empty<PlayerId>();
        [Key(2)] public string ModeId { get; init; } = "default";
        [Key(3)] public int TickRateHz { get; init; } = 30;
    }

    [MessagePackObject]
    public sealed class MatchReady
    {
        [Key(0)] public MatchId MatchId { get; init; }
        [Key(1)] public int ExpectedPlayers { get; init; }
        [Key(2)] public long ReadyAtMs { get; init; }
    }
}
