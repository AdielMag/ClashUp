using MessagePack;

namespace ClashUp.Shared.MessagePackObjects
{
    [MessagePackObject]
    public sealed class PingRequest
    {
        [Key(0)]
        public long ClientStampMs { get; init; }

        [Key(1)]
        public string? Note { get; init; }
    }
}
