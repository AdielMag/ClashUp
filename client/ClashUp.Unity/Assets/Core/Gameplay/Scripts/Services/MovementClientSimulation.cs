using System;
using System.Collections.Generic;
using ClashUp.Shared.Maps;
using ClashUp.Shared.MessagePackObjects;
using ClashUp.Shared.Simulation;

namespace ClashUp.Client.Gameplay
{
    public sealed class MovementClientSimulation : IClientSimulation
    {
        private readonly Dictionary<string, PlayerRenderState> _players = new();
        private float _pendingMoveX;
        private float _pendingMoveZ;

        public int CurrentTick { get; private set; }
        public PlayerId LocalId { get; private set; }
        public IReadOnlyDictionary<string, PlayerRenderState> Players => _players;

        public void SetLocalPlayer(PlayerId id) => LocalId = id;
        public void SetRandomSeed(uint seed) { }
        public void LoadMap(MapData mapData) { }

        public void ApplyLocalInput(InputCommand command)
        {
            _pendingMoveX = MovementModel.DecodeAxis(command.MoveX);
            _pendingMoveZ = MovementModel.DecodeAxis(command.MoveY);
        }

        public void Step(double deltaSeconds)
        {
            if (LocalId.Value != null && _players.TryGetValue(LocalId.Value, out var state))
            {
                MovementModel.Step(ref state.X, ref state.Z, ref state.Yaw,
                    _pendingMoveX, _pendingMoveZ, deltaSeconds);
            }
            CurrentTick++;
        }

        public void StepPhysicsOnly(double deltaSeconds) => Step(deltaSeconds);

        public bool TryGetPhysicsPosition(out float x, out float z)
        {
            var lid = LocalId.Value;
            if (lid != null && _players.TryGetValue(lid, out var state))
            {
                x = state.X;
                z = state.Z;
                return true;
            }
            x = z = 0f;
            return false;
        }

        public int ReconcileTo(int serverTick, WorldStatePacket packet)
        {
            CurrentTick = Math.Max(CurrentTick, serverTick);

            if (packet == null) return 0;

            int ack = 0;
            foreach (var dto in packet.Players)
            {
                if (!_players.TryGetValue(dto.Id.Value, out var state))
                {
                    state = new PlayerRenderState { Id = dto.Id };
                    _players[dto.Id.Value] = state;
                }
                state.X = dto.X;
                state.Z = dto.Z;
                state.Yaw = dto.Yaw;
                if (dto.Id.Equals(LocalId)) ack = dto.LastProcessedInputSeq;
            }
            return ack;
        }

        public void Dispose() { }
    }
}
