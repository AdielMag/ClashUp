using MessagePack;

namespace ClashUp.Shared.MessagePackObjects;

[MessagePackObject]
public sealed class QueueRequest
{
    [Key(0)] public string ModeId { get; init; } = "default";
}

[MessagePackObject]
public sealed class QueueTicket
{
    [Key(0)] public string TicketId { get; init; } = string.Empty;
}

public enum TicketStatus
{
    Queued = 0,
    Matched = 1,
    Cancelled = 2,
    Failed = 3,
}

[MessagePackObject]
public sealed class TicketPoll
{
    [Key(0)] public TicketStatus Status { get; init; }
    [Key(1)] public MatchHandoff? Handoff { get; init; }
    [Key(2)] public string? FailureReason { get; init; }
}

[MessagePackObject]
public sealed class MatchHandoff
{
    [Key(0)] public MatchId MatchId { get; init; }
    [Key(1)] public string GsEndpoint { get; init; } = string.Empty;
    [Key(2)] public string MatchToken { get; init; } = string.Empty;
    [Key(3)] public long MatchTokenExpiresAtMs { get; init; }
}
