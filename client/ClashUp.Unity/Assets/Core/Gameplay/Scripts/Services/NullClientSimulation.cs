using System;
using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Client.Gameplay
{
    /// <summary>
    /// Placeholder until the AetherNet client world is wired. Advances a
    /// tick counter; ignores inputs and snapshots. Lets the rest of the
    /// prediction pipeline (input gather, send loop, reconciler) run.
    /// </summary>
    public sealed class NullClientSimulation : IClientSimulation
    {
        public int CurrentTick { get; private set; }

        public void ApplyLocalInput(InputCommand command) { }

        public void Step(double deltaSeconds) => CurrentTick++;

        public void ReconcileTo(int serverTick, ReadOnlyMemory<byte> deltaBlob)
        {
            CurrentTick = Math.Max(CurrentTick, serverTick);
        }

        public void Dispose() { }
    }
}
