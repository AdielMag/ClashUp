using MessagePack;

namespace ClashUp.Shared.MessagePackObjects;

[MessagePackObject]
public sealed class MatchJoinRequest
{
    [Key(0)] public MatchId MatchId { get; init; }
    [Key(1)] public string MatchToken { get; init; } = string.Empty;
}
