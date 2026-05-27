using MessagePack;

namespace ClashUp.Shared.MessagePackObjects
{
    /// <summary>
    /// Out-of-band match event (player joined/left handled by their own
    /// receiver methods; this covers everything else: round started, goal
    /// scored, etc.). Payload schema is intentionally loose for phase 1.
    /// </summary>
    [MessagePackObject]
    public sealed class MatchEvent
    {
        [Key(0)] public int Tick { get; init; }
        [Key(1)] public string Kind { get; init; } = string.Empty;
        [Key(2)] public string Payload { get; init; } = string.Empty;
    }
}
