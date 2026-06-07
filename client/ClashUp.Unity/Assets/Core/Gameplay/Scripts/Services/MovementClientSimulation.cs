using System;
using System.Collections.Generic;
using ClashUp.Shared.MessagePackObjects;
using ClashUp.Shared.Simulation;
using MessagePack;

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

        public void ReconcileTo(int serverTick, ReadOnlyMemory<byte> deltaBlob)
        {
            CurrentTick = Math.Max(CurrentTick, serverTick);

            if (deltaBlob.Length == 0) return;

            var world = MessagePackSerializer.Deserialize<WorldStatePacket>(deltaBlob);
            foreach (var dto in world.Players)
            {
                if (!_players.TryGetValue(dto.Id.Value, out var state))
                {
                    state = new PlayerRenderState { Id = dto.Id };
                    _players[dto.Id.Value] = state;
                }
                state.X = dto.X;
                state.Z = dto.Z;
                state.Yaw = dto.Yaw;
            }
        }

        public void Dispose() { }
    }
}
