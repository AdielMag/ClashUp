using MessagePack;

namespace ClashUp.Shared.MessagePackObjects;

/// <summary>
/// One frame of player intent. Components are quantized to fixed-point
/// ints at the wire boundary so the server's authoritative simulation
/// stays deterministic across runtimes. See docs/rules/server-authority.md.
/// </summary>
[MessagePackObject]
public sealed class InputCommand
{
    [Key(0)] public int Tick { get; init; }
    [Key(1)] public long ClientSendStampMs { get; init; }
    [Key(2)] public uint ButtonMask { get; init; }
    [Key(3)] public short MoveX { get; init; }
    [Key(4)] public short MoveY { get; init; }
    [Key(5)] public short AimYawQ { get; init; }
    [Key(6)] public short AimPitchQ { get; init; }
    [Key(7)] public int SequenceId { get; init; }
}
