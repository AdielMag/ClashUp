using MessagePack;

namespace ClashUp.Shared.MessagePackObjects;

[MessagePackObject]
public sealed class PongResult
{
    [Key(0)]
    public long ClientStampMs { get; init; }

    [Key(1)]
    public long ServerStampMs { get; init; }

    [Key(2)]
    public string? ServerVersion { get; init; }
}
