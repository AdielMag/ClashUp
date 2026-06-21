using MessagePack;

namespace ClashUp.Shared.MessagePackObjects
{
    /// <summary>
    /// One frame of player intent. Components are quantized to fixed-point
    /// ints at the wire boundary so the server's authoritative simulation
    /// stays deterministic across runtimes. See docs/rules/server-authority.md.
    /// </summary>
    [MessagePackObject]
    public sealed class InputCommand
    {
        /// <summary>
        /// High bit of <see cref="ButtonMask"/> requesting server-side auto-aim (nearest enemy)
        /// for this cast — set when the player fires a manual ability without aiming past the
        /// input dead zone. Does not collide with ability slot bits (slots are low bits).
        /// </summary>
        public const uint AutoAimFlag = 1u << 31;

        [Key(0)] public int Tick { get; init; }
        [Key(1)] public long ClientSendStampMs { get; init; }
        [Key(2)] public uint ButtonMask { get; init; }
        [Key(3)] public short MoveX { get; init; }
        [Key(4)] public short MoveY { get; init; }
        [Key(5)] public short AimYawQ { get; init; }

        /// <summary>
        /// Quantized 0..1 joystick distance-from-center for this cast (via
        /// <see cref="Simulation.MovementModel.EncodeAxis"/>). Used by <c>CastMode.TargetPoint</c>
        /// abilities to place the cast point nearer/farther from the player. 0 when not aiming.
        /// </summary>
        [Key(6)] public short AimDistanceQ { get; init; }
        [Key(7)] public int SequenceId { get; init; }
    }
}
