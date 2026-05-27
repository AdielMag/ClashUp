using System.Collections.Generic;
using MessagePack;

namespace ClashUp.Shared.MessagePackObjects
{
    [MessagePackObject]
    public sealed class MatchResult
    {
        [Key(0)] public MatchId MatchId { get; init; }
        [Key(1)] public int WinningTeamId { get; init; }
        [Key(2)] public IReadOnlyDictionary<string, int> TeamScores { get; init; } =
            new Dictionary<string, int>();
        [Key(3)] public long EndedAtMs { get; init; }
    }
}
