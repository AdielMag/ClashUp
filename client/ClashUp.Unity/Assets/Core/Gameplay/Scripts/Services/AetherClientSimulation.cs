using System;
using System.Collections.Generic;
using ClashUp.Shared.MessagePackObjects;
using ClashUp.Shared.Simulation;
using MessagePack;

namespace ClashUp.Client.Gameplay
{
    /// <summary>
    /// Client-side simulation backed by AetherNet (Box2D) physics.
    /// Runs the same MatchPhysicsWorld as the server so client prediction
    /// and server reconciliation share identical collision geometry.
    /// </summary>
    public sealed class AetherClientSimulation : IClientSimulation
    {
        private readonly MatchPhysicsWorld _world = new();
        private readonly Dictionary<string, PlayerRenderState> _players = new();

        public int CurrentTick { get; private set; }
        public PlayerId LocalId { get; private set; }
        public IReadOnlyDictionary<string, PlayerRenderState> Players => _players;

        public void SetLocalPlayer(PlayerId id) => LocalId = id;

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
            foreach (var dto in packet.Players)
            {
                // EnsurePlayer uses colorSlot=0 (spawn position) as a placeholder;
                // SnapPlayerPosition immediately overwrites it with the authoritative state.
                _world.EnsurePlayer(dto.Id.Value, 0);
                _world.SnapPlayerPosition(dto.Id.Value, dto.X, dto.Z);
            }
            SyncRenderStates();
        }

        private void SyncRenderStates()
        {
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
            }
        }

        public void Dispose() => _world.Dispose();
    }
}
