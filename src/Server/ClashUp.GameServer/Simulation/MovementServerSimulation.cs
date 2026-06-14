using ClashUp.Server.GameServer.Match;
using ClashUp.Shared.Characters;
using ClashUp.Shared.Maps;
using ClashUp.Shared.MessagePackObjects;
using ClashUp.Shared.Simulation;
using MessagePack;

namespace ClashUp.Server.GameServer.Simulation;

public sealed class MovementServerSimulation : IServerSimulation
{
    private sealed class PlayerState
    {
        public float X;
        public float Z;
        public float Yaw;
        public float MoveX;
        public float MoveZ;
        public PlayerId Id;
    }

    private readonly MatchCharactersHolder _characters;
    private readonly Dictionary<string, PlayerState> _players = new();
    private readonly HealthTable _health = new();

    public MovementServerSimulation(MatchCharactersHolder characters)
    {
        _characters = characters;
    }

    public int CurrentTick { get; private set; }
    public uint RandomSeed { get; } = (uint)System.Random.Shared.Next(1, int.MaxValue);

    public void LoadMap(MapData mapData) { }

    public void EnsurePlayer(PlayerId player, int colorSlot, int teamId, CharacterId characterId)
    {
        if (_players.ContainsKey(player.Value)) return;

        const float spacing = 3f;
        _players[player.Value] = new PlayerState
        {
            Id = player,
            X = colorSlot * spacing,
            Z = 0f,
            Yaw = 0f,
        };
        var stats = _characters.Catalog.Get(characterId).BaseStats;
        _health.Initialize(player.Value, stats.MaxHealth);
    }

    public void ApplyInput(PlayerId player, InputCommand command)
    {
        if (!_players.TryGetValue(player.Value, out var state)) return;
        state.MoveX = MovementModel.DecodeAxis(command.MoveX);
        state.MoveZ = MovementModel.DecodeAxis(command.MoveY);
    }

    public void Step(double deltaSeconds)
    {
        foreach (var state in _players.Values)
        {
            MovementModel.Step(ref state.X, ref state.Z, ref state.Yaw, state.MoveX, state.MoveZ, deltaSeconds);
        }
        CurrentTick++;
    }

    public ReadOnlyMemory<byte> EncodeDelta(int baselineTick)
    {
        var dtos = new PlayerStateDto[_players.Count];
        int i = 0;
        foreach (var state in _players.Values)
        {
            dtos[i++] = new PlayerStateDto
            {
                Id = state.Id,
                X = state.X,
                Z = state.Z,
                Yaw = state.Yaw,
                Health = _health.GetHealth(state.Id.Value),
            };
        }
        return MessagePackSerializer.Serialize(new WorldStatePacket { Players = dtos });
    }

    public IReadOnlyList<MatchEvent> DrainAbilityEvents() => Array.Empty<MatchEvent>();

    public void Dispose() { }
}
