using System.Collections.Generic;
using MessagePack;

namespace ClashUp.Shared.MessagePackObjects;

[MessagePackObject]
public sealed class JoinResult
{
    [Key(0)] public PlayerId You { get; init; }
    [Key(1)] public IReadOnlyList<PlayerSummary> Players { get; init; } = System.Array.Empty<PlayerSummary>();
    [Key(2)] public int TickRateHz { get; init; }
    [Key(3)] public int CurrentTick { get; init; }
}
