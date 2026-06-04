using System;
using System.Collections.Generic;
using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Client.Gameplay
{
    /// <summary>
    /// Client-side simulation contract. AetherClientSimulation is the live implementation;
    /// NullClientSimulation and MovementClientSimulation are placeholders / fallbacks.
    /// </summary>
    public interface IClientSimulation : IDisposable
    {
        int CurrentTick { get; }

        /// <summary>All known players, keyed by PlayerId.Value. Read by PlayerViewSystem.</summary>
        IReadOnlyDictionary<string, PlayerRenderState> Players { get; }

        PlayerId LocalId { get; }

        void SetLocalPlayer(PlayerId id);

        void ApplyLocalInput(InputCommand command);

        void Step(double deltaSeconds);

        /// <summary>
        /// Rewind to <paramref name="serverTick"/>, apply the snapshot,
        /// then re-run any local inputs queued past <paramref name="serverTick"/>.
        /// </summary>
        void ReconcileTo(int serverTick, ReadOnlyMemory<byte> deltaBlob);
    }
}
