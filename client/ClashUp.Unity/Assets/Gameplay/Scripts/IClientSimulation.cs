using System;
using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Client.Gameplay;

/// <summary>
/// Client-side counterpart of IServerSimulation. AetherNet plugs in
/// here for prediction. NullClientSimulation ships as the phase-1
/// placeholder so the surrounding plumbing compiles and runs.
/// </summary>
public interface IClientSimulation : IDisposable
{
    int CurrentTick { get; }

    void ApplyLocalInput(InputCommand command);

    void Step(double deltaSeconds);

    /// <summary>
    /// Rewind to <paramref name="serverTick"/>, apply the snapshot,
    /// then re-run any local inputs queued past <paramref name="serverTick"/>.
    /// </summary>
    void ReconcileTo(int serverTick, ReadOnlyMemory<byte> deltaBlob);
}
