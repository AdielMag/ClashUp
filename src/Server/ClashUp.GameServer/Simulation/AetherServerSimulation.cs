using System.Collections.Generic;
using ClashUp.Shared.Characters;
using ClashUp.Shared.MessagePackObjects;
using ClashUp.Shared.Simulation;
using MessagePack;

namespace ClashUp.Server.GameServer.Simulation;

public sealed class AetherServerSimulation : IServerSimulation
{
    private readonly MatchPhysicsWorld _world = new();
    private readonly HealthTable _health = new();

    public int CurrentTick { get; private set; }
    public uint RandomSeed { get; }

    public AetherServerSimulation()
    {
        RandomSeed = (uint)System.Random.Shared.Next(1, int.MaxValue);
    }

    public void EnsurePlayer(PlayerId player, int colorSlot)
    {
        _world.EnsurePlayer(player.Value, colorSlot);
        var stats = CharacterRegistry.Default.BaseStats;
        _health.Initialize(player.Value, stats.MaxHealth);
    }

    public void ApplyInput(PlayerId player, InputCommand command)
        => _world.ApplyInput(player.Value,
            MovementModel.DecodeAxis(command.MoveX),
            MovementModel.DecodeAxis(command.MoveY));

    public void Step(double deltaSeconds)
    {
        _world.Step(deltaSeconds);
        CurrentTick++;
    }

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
            });
        }
        return MessagePackSerializer.Serialize(new WorldStatePacket { Players = dtos.ToArray() });
    }

    public void Dispose() => _world.Dispose();
}
