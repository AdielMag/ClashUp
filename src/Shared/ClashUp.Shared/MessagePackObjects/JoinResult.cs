using System.Collections.Generic;
using MessagePack;

namespace ClashUp.Shared.MessagePackObjects
{
    [MessagePackObject]
    public sealed class JoinResult
    {
        [Key(0)] public PlayerId You { get; init; }
        [Key(1)] public IReadOnlyList<PlayerSummary> Players { get; init; } = System.Array.Empty<PlayerSummary>();
        [Key(2)] public int TickRateHz { get; init; }
        [Key(3)] public int CurrentTick { get; init; }
        [Key(4)] public int DurationSeconds { get; init; }
        [Key(5)] public double ElapsedSeconds { get; init; }
        [Key(6)] public uint RandomSeed { get; init; }
        [Key(7)] public string MapId { get; init; } = "arena_tdm";
        [Key(8)] public CharactersConfig Characters { get; init; } = CharactersConfig.Default;
        [Key(9)] public AbilitiesConfig Abilities { get; init; } = AbilitiesConfig.Default;

        /// <summary>Game-mode discriminator (e.g. "survival", "elimination"). Drives the client HUD.</summary>
        [Key(10)] public string ObjectiveType { get; init; } = "survival";
    }
}
