using ClashUp.Shared.Characters;
using ClashUp.Shared.Maps;
using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Server.GameServer.Simulation;

public interface IServerSimulation : IDisposable
{
    int CurrentTick { get; }

    uint RandomSeed { get; }

    void LoadMap(MapData mapData);

    /// <summary>
    /// Apply game-mode rules (e.g. "survival" vs "elimination"). Call after <see cref="LoadMap"/> so
    /// mode setup that depends on map data (breakable boxes) can run. No-op for survival.
    /// </summary>
    void Configure(string objectiveType);

    /// <summary>True once a player is permanently out in a no-respawn mode. Always false in survival.</summary>
    bool IsEliminated(string playerId);

    void EnsurePlayer(PlayerId player, int colorSlot, int teamId, CharacterId characterId);

    void ApplyInput(PlayerId player, InputCommand command);

    void Step(double deltaSeconds);

    ReadOnlyMemory<byte> EncodeDelta(int baselineTick);

    IReadOnlyList<MatchEvent> DrainAbilityEvents();

    /// <summary>Per-team total points (objective modes). Empty in survival.</summary>
    IReadOnlyDictionary<string, int> GetTeamScores();

    /// <summary>
    /// Perception snapshot for AI bots: the bot's own pose plus the nearest living enemy
    /// (different team). Returns false if the bot isn't in the simulation yet.
    /// </summary>
    bool TryGetBotView(string botId, out BotView view);
}

/// <summary>Minimal world view the <c>BotDirector</c> needs to drive a bot each tick.</summary>
public readonly struct BotView
{
    public float SelfX { get; init; }
    public float SelfZ { get; init; }
    public float SelfYaw { get; init; }
    public bool SelfAlive { get; init; }

    public bool HasEnemy { get; init; }
    public float EnemyX { get; init; }
    public float EnemyZ { get; init; }
    public float EnemyDistance { get; init; }

    // Nearest breakable box (objective modes). Bots seek these for points when no player is the priority.
    public bool HasBox { get; init; }
    public float BoxX { get; init; }
    public float BoxZ { get; init; }
    public float BoxDistance { get; init; }
}
