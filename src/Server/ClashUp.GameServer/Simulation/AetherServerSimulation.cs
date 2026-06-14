using System.Collections.Generic;
using ClashUp.Server.GameServer.Abilities;
using ClashUp.Server.GameServer.Match;
using ClashUp.Shared.Abilities;
using ClashUp.Shared.Characters;
using ClashUp.Shared.Maps;
using ClashUp.Shared.MessagePackObjects;
using ClashUp.Shared.Simulation;
using MessagePack;

namespace ClashUp.Server.GameServer.Simulation;

public sealed class AetherServerSimulation : IServerSimulation
{
    private readonly MatchCharactersHolder _characters;
    private readonly MatchPhysicsWorld _world = new();
    private readonly HealthTable _health = new();
    private readonly AbilityExecutor _abilities = new();
    private readonly Dictionary<string, int> _lastSeq = new();
    private readonly Dictionary<int, int> _teamSlotCounters = new();
    private readonly HashSet<string> _knownPlayers = new();
    private MapData? _mapData;

    public int CurrentTick { get; private set; }
    public uint RandomSeed { get; }

    public AetherServerSimulation(ServerAbilityStore abilityStore, MatchCharactersHolder characters)
    {
        _characters = characters;
        RandomSeed = (uint)System.Random.Shared.Next(1, int.MaxValue);
        _abilities.RegisterAbilities(abilityStore.All);
    }

    public void LoadMap(MapData mapData)
    {
        _mapData = mapData;
        _world.LoadMapGeometry(mapData);
    }

    public void EnsurePlayer(PlayerId player, int colorSlot, int teamId, CharacterId characterId)
    {
        if (!_knownPlayers.Add(player.Value)) return;

        var character = _characters.Catalog.Get(characterId);
        var stats = character.BaseStats;

        if (!_teamSlotCounters.TryGetValue(teamId, out int slotIndex))
            slotIndex = 0;

        var (spawnX, spawnZ) = SpawnResolver.GetSpawnPosition(_mapData, teamId, slotIndex);
        _world.EnsurePlayer(player.Value, spawnX, spawnZ, stats.MoveSpeed);
        _health.Initialize(player.Value, stats.MaxHealth);
        _health.SetInvulnerable(player.Value, HealthTable.DefaultSpawnInvulnTicks);
        _abilities.InitPlayer(player.Value, character.AutoAttackId, character.ActiveAbilityId);

        _teamSlotCounters[teamId] = slotIndex + 1;
    }

    public void ApplyInput(PlayerId player, InputCommand command)
    {
        _lastSeq[player.Value] = command.SequenceId;
        _world.ApplyInput(player.Value,
            MovementModel.DecodeAxis(command.MoveX),
            MovementModel.DecodeAxis(command.MoveY));

        if (command.ButtonMask != 0)
        {
            float aimYaw = MovementModel.DecodeAxis(command.AimYawQ) * 180f;
            _abilities.ProcessInput(player.Value, command.ButtonMask, aimYaw, _world, _health, CurrentTick);
        }
    }

    public void Step(double deltaSeconds)
    {
        _world.Step(deltaSeconds);
        _health.Tick();
        _abilities.Tick(_world, _health, CurrentTick);
        CurrentTick++;
    }

    public IReadOnlyList<MatchEvent> DrainAbilityEvents() => _abilities.DrainEvents();

    public ReadOnlyMemory<byte> EncodeDelta(int baselineTick)
    {
        var dtos = new List<PlayerStateDto>();
        foreach (var id in _world.PlayerIds)
        {
            var (x, z, yaw) = _world.GetPlayerState(id);
            dtos.Add(new PlayerStateDto
            {
                Id = new PlayerId(id),
                X = x,
                Z = z,
                Yaw = yaw,
                Health = _health.GetHealth(id),
                LastProcessedInputSeq = _lastSeq.TryGetValue(id, out var seq) ? seq : 0,
                IsInvulnerable = _health.IsInvulnerable(id),
            });
        }
        return MessagePackSerializer.Serialize(new WorldStatePacket { Players = dtos.ToArray() });
    }

    public void Dispose() => _world.Dispose();
}
