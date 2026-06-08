using System;
using System.Collections.Generic;
using AetherNet;
using ClashUp.Shared.Characters;
using ClashUp.Shared.Maps;
using ClashUp.Shared.MessagePackObjects;
using ClashUp.Shared.Simulation;
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

        public void LoadMap(MapData mapData)
        {
            _world.LoadMapGeometry(mapData);
        }

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

        public void StepPhysicsOnly(double deltaSeconds)
        {
            _world.Step(deltaSeconds);
            CurrentTick++;
        }

        public bool TryGetPhysicsPosition(out float x, out float z)
        {
            var lid = LocalId.Value;
            if (lid != null)
            {
                var (px, pz, _) = _world.GetPlayerState(lid);
                x = px;
                z = pz;
                return true;
            }
            x = z = 0f;
            return false;
        }

        public int ReconcileTo(int serverTick, WorldStatePacket packet)
        {
            CurrentTick = Math.Max(CurrentTick, serverTick);
            if (LocalId.Value == null || packet == null) return 0;

            // Only the local player is simulated on the client. Remote players are rendered
            // from RemotePlayerInterpolator and intentionally ignored here.
            var stats = CharacterRegistry.Default.BaseStats;
            foreach (var dto in packet.Players)
            {
                if (!dto.Id.Equals(LocalId)) continue;

                _world.EnsurePlayer(dto.Id.Value, 0, stats.MoveSpeed);
                _world.SnapPlayerPosition(dto.Id.Value, dto.X, dto.Z);
                _health.Initialize(dto.Id.Value, stats.MaxHealth);
                _health.SnapHealth(dto.Id.Value, dto.Health);
                // Do NOT touch render state here. Replay uses StepPhysicsOnly so
                // PrevX/X stay untouched. Only normal Predict→Step updates them.
                return dto.LastProcessedInputSeq;
            }
            return 0;
        }

        private void SyncRenderStates()
        {
            var maxHealth = CharacterRegistry.Default.BaseStats.MaxHealth;
            foreach (var id in _world.PlayerIds)
            {
                var (x, z, yaw) = _world.GetPlayerState(id);
                if (!_players.TryGetValue(id, out var rs))
                {
                    rs = new PlayerRenderState { Id = new PlayerId(id), X = x, Z = z, Yaw = yaw };
                    _players[id] = rs;
                }
                rs.PrevX = rs.X;
                rs.PrevZ = rs.Z;
                rs.PrevYaw = rs.Yaw;
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
