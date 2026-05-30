using System.Collections.Generic;
using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Client.Gameplay
{
    /// <summary>
    /// Wraps the client's IClientSimulation with the input-ring + reconcile
    /// pattern: locally-gathered inputs are stepped into the predicted world
    /// immediately and kept in a ring until the server acks them via a
    /// snapshot at or past that tick.
    /// </summary>
    public sealed class ClientPredictionWorld
    {
        private readonly IClientSimulation _sim;
        private readonly Queue<InputCommand> _pendingInputs = new();

        public ClientPredictionWorld(IClientSimulation sim)
        {
            _sim = sim;
        }

        public int CurrentTick => _sim.CurrentTick;

        public void Predict(InputCommand command)
        {
            _sim.ApplyLocalInput(command);
            _sim.Step(deltaSeconds: 0);
            _pendingInputs.Enqueue(command);
        }

        public void ApplyServerSnapshot(SnapshotPacket snapshot)
        {
            _sim.ReconcileTo(snapshot.Tick, snapshot.DeltaBlob);

            // Drop inputs the server has already seen.
            while (_pendingInputs.Count > 0 && _pendingInputs.Peek().Tick <= snapshot.Tick)
            {
                _pendingInputs.Dequeue();
            }

            // Re-apply the still-unacked inputs to catch the local world back up.
            foreach (var queued in _pendingInputs)
            {
                _sim.ApplyLocalInput(queued);
                _sim.Step(deltaSeconds: 0);
            }
        }
    }
}
