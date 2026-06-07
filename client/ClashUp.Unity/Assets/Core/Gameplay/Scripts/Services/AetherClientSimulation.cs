using System;
using System.Collections.Generic;
using AetherNet;
using ClashUp.Shared.Characters;
using ClashUp.Shared.MessagePackObjects;
using ClashUp.Shared.Simulation;
using MessagePack;
using UnityEngine;

namespace ClashUp.Client.Gameplay
{
    public sealed class AetherClientSimulation : IClientSimulation
    {
        private readonly MatchPhysicsWorld _world;
        private readonly HealthTable _health = new();
        private readonly Dictionary<string, PlayerRenderState> _players = new();
        private uint _randomSeed;

        public AetherClientSimulation(GameObject playerPrefab)
        {
            var collider = playerPrefab.GetComponent<AetherCircleCollider>();
            float radius = collider != null ? collider.Radius : MatchPhysicsWorld.DefaultPlayerRadius;
            _world = new MatchPhysicsWorld(radius);
        }

        public int CurrentTick { get; private set; }
        public PlayerId LocalId { get; private set; }
        public IReadOnlyDictionary<string, PlayerRenderState> Players => _players;

        public void SetLocalPlayer(PlayerId id) => LocalId = id;
        public void SetRandomSeed(uint seed) => _randomSeed = seed;

        public void ApplyLocalInput(InputCommand command)
        {
            if (LocalId.Value == null) return;
            _world.ApplyInput(LocalId.Value,
                MovementModel.DecodeAxis(command.MoveX),
                MovementModel.DecodeAxis(command.MoveY));
        }

        public void Step(double deltaSeconds)
        {
            _world.Step(deltaSeconds);
            CurrentTick++;
            SyncRenderStates();
        }

        public void ReconcileTo(int serverTick, ReadOnlyMemory<byte> deltaBlob)
        {
            CurrentTick = Math.Max(CurrentTick, serverTick);
            if (deltaBlob.Length == 0) return;

            var packet = MessagePackSerializer.Deserialize<WorldStatePacket>(deltaBlob);
            var maxHealth = CharacterRegistry.Default.BaseStats.MaxHealth;
            foreach (var dto in packet.Players)
            {
                _world.EnsurePlayer(dto.Id.Value, 0);
                _world.SnapPlayerPosition(dto.Id.Value, dto.X, dto.Z);
                _health.Initialize(dto.Id.Value, maxHealth);
                _health.SnapHealth(dto.Id.Value, dto.Health);
            }
            SyncRenderStates();
        }

        private void SyncRenderStates()
        {
            var maxHealth = CharacterRegistry.Default.BaseStats.MaxHealth;
            foreach (var id in _world.PlayerIds)
            {
                var (x, z, yaw) = _world.GetPlayerState(id);
                if (!_players.TryGetValue(id, out var rs))
                {
                    rs = new PlayerRenderState { Id = new PlayerId(id) };
                    _players[id] = rs;
                }
                rs.X = x;
                rs.Z = z;
                rs.Yaw = yaw;
                rs.Health = _health.GetHealth(id);
                rs.MaxHealth = maxHealth;
            }
        }

        public void Dispose() => _world.Dispose();
    }
}
