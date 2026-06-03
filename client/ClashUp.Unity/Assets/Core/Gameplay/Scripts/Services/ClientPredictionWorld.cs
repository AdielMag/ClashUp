using System.Collections.Generic;
using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Client.Gameplay
{
    public sealed class ClientPredictionWorld
    {
        private readonly IClientSimulation _sim;
        private readonly Queue<InputCommand> _pendingInputs = new();
        private double _tickInterval;

        public ClientPredictionWorld(IClientSimulation sim)
        {
            _sim = sim;
        }

        public int CurrentTick => _sim.CurrentTick;

        public void Configure(int tickRateHz)
        {
            _tickInterval = 1.0 / tickRateHz;
        }

        public void Predict(InputCommand command)
        {
            _sim.ApplyLocalInput(command);
            _sim.Step(_tickInterval);
            _pendingInputs.Enqueue(command);
        }

        public void ApplyServerSnapshot(SnapshotPacket snapshot)
        {
            _sim.ReconcileTo(snapshot.Tick, snapshot.DeltaBlob);

            while (_pendingInputs.Count > 0 && _pendingInputs.Peek().Tick <= snapshot.Tick)
            {
                _pendingInputs.Dequeue();
            }

            foreach (var queued in _pendingInputs)
            {
                _sim.ApplyLocalInput(queued);
                _sim.Step(_tickInterval);
            }
        }
    }
}
