using MessagePack;

namespace ClashUp.Shared.MessagePackObjects
{
    [MessagePackObject]
    public sealed class WorldStatePacket
    {
        [Key(0)] public PlayerStateDto[] Players { get; init; } = System.Array.Empty<PlayerStateDto>();
    }

    [MessagePackObject]
    public sealed class PlayerStateDto
    {
        [Key(0)] public PlayerId Id { get; init; }
        [Key(1)] public float X { get; init; }
        [Key(2)] public float Z { get; init; }
        [Key(3)] public float Yaw { get; init; }
        [Key(4)] public float Health { get; init; }

        /// <summary>
        /// Sequence id of the last <see cref="InputCommand"/> the server applied for this
        /// player. The owning client uses it to discard acked pending inputs and replay the
        /// rest (server reconciliation). Zero for players that have not sent input yet.
        /// </summary>
        [Key(5)] public int LastProcessedInputSeq { get; init; }
    }
}
