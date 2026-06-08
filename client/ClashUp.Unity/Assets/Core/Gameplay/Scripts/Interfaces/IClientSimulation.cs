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

        void SetRandomSeed(uint seed);

        void ApplyLocalInput(InputCommand command);

        void Step(double deltaSeconds);

        /// <summary>
        /// Advances physics without updating render state (PrevX/X). Used during
        /// reconciliation replay so the render interpolation targets stay clean.
        /// </summary>
        void StepPhysicsOnly(double deltaSeconds);

        /// <summary>
        /// Returns the local player's current physics position (not render state).
        /// Used for computing reconciliation corrections at the physics level.
        /// </summary>
        bool TryGetPhysicsPosition(out float x, out float z);

        /// <summary>
        /// Applies the authoritative state for the <b>local</b> player from a snapshot
        /// (remote players are handled by <see cref="RemotePlayerInterpolator"/>, not here).
        /// Returns the sequence id of the last local input the server processed, so the
        /// caller can discard acked pending inputs and replay the rest (reconciliation).
        /// </summary>
        int ReconcileTo(int serverTick, WorldStatePacket packet);
    }
}
