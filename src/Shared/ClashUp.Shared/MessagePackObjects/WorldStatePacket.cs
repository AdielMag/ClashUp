using MessagePack;

namespace ClashUp.Shared.MessagePackObjects
{
    [MessagePackObject]
    public sealed class WorldStatePacket
    {
        [Key(0)] public PlayerStateDto[] Players { get; init; } = System.Array.Empty<PlayerStateDto>();

        /// <summary>Breakable boxes (objective modes only; empty otherwise).</summary>
        [Key(1)] public BoxStateDto[] Boxes { get; init; } = System.Array.Empty<BoxStateDto>();

        /// <summary>Loose point orbs on the ground (objective modes only; empty otherwise).</summary>
        [Key(2)] public OrbStateDto[] Orbs { get; init; } = System.Array.Empty<OrbStateDto>();
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
        [Key(6)] public bool IsInvulnerable { get; init; }
        [Key(7)] public int RespawnInTicks { get; init; }

        /// <summary>Points this player currently holds (objective modes). Drives HP/damage scaling.</summary>
        [Key(8)] public int Points { get; init; }

        /// <summary>True once a player is permanently out (no-respawn modes). Survival: always false.</summary>
        [Key(9)] public bool IsEliminated { get; init; }

        /// <summary>Current max health — grows with points, so the client health bar can scale.</summary>
        [Key(10)] public float MaxHealth { get; init; }
    }

    [MessagePackObject]
    public sealed class BoxStateDto
    {
        [Key(0)] public int Id { get; init; }
        [Key(1)] public float X { get; init; }
        [Key(2)] public float Z { get; init; }
        [Key(3)] public float Health { get; init; }
        [Key(4)] public float MaxHealth { get; init; }
    }

    [MessagePackObject]
    public sealed class OrbStateDto
    {
        [Key(0)] public int Id { get; init; }
        [Key(1)] public float X { get; init; }
        [Key(2)] public float Z { get; init; }
        [Key(3)] public int Value { get; init; }
    }
}
