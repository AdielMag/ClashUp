using ClashUp.Shared.Characters;
using ClashUp.Shared.Maps;
using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Server.GameServer.Simulation;

public sealed class NullServerSimulation : IServerSimulation
{
    public int CurrentTick { get; private set; }
    public uint RandomSeed => 0;

    public void LoadMap(MapData mapData) { }

    public void Configure(string objectiveType) { }

    public bool IsEliminated(string playerId) => false;

    public IReadOnlyDictionary<string, int> GetTeamScores() => new Dictionary<string, int>();

    public void EnsurePlayer(PlayerId player, int colorSlot, int teamId, CharacterId characterId) { }

    public void ApplyInput(PlayerId player, InputCommand command) { }

    public void Step(double deltaSeconds) => CurrentTick++;

    public ReadOnlyMemory<byte> EncodeDelta(int baselineTick) => ReadOnlyMemory<byte>.Empty;

    public IReadOnlyList<MatchEvent> DrainAbilityEvents() => Array.Empty<MatchEvent>();

    public bool TryGetBotView(string botId, out BotView view) { view = default; return false; }

    public void Dispose() { }
}
