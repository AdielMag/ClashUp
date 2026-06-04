using System;
using System.Collections.Generic;
using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Client.Gameplay
{
    /// <summary>
    /// Placeholder until the AetherNet client world is wired. Advances a tick counter;
    /// ignores inputs and snapshots. Lets the rest of the prediction pipeline
    /// (input gather, send loop, reconciler) run without crashing.
    /// </summary>
    public sealed class NullClientSimulation : IClientSimulation
    {
        private static readonly IReadOnlyDictionary<string, PlayerRenderState> Empty =
            new Dictionary<string, PlayerRenderState>();

        public int CurrentTick { get; private set; }
        public IReadOnlyDictionary<string, PlayerRenderState> Players => Empty;
        public PlayerId LocalId { get; private set; }

        public void SetLocalPlayer(PlayerId id) => LocalId = id;
        public void ApplyLocalInput(InputCommand command) { }
        public void Step(double deltaSeconds) => CurrentTick++;

        public void ReconcileTo(int serverTick, ReadOnlyMemory<byte> deltaBlob)
            => CurrentTick = Math.Max(CurrentTick, serverTick);

        public void Dispose() { }
    }
}
