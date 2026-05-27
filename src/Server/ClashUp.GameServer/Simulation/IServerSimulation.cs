using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Server.GameServer.Simulation;

/// <summary>
/// The seam AetherNet plugs into on the server. Phase-1 ships a
/// NullServerSimulation that emits empty snapshots so the tick loop
/// runs end-to-end without the real physics package. Swap this for
/// the AetherNet adapter once the submodule lands under external/AetherNet/.
/// </summary>
public interface IServerSimulation : IDisposable
{
    int CurrentTick { get; }

    void ApplyInput(InputCommand command);

    void Step(double deltaSeconds);

    /// <summary>
    /// Encodes a delta from <paramref name="baselineTick"/> to the current tick.
    /// The returned buffer is owned by the simulation until the next call.
    /// </summary>
    ReadOnlyMemory<byte> EncodeDelta(int baselineTick);
}
