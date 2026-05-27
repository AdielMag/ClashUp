using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Server.GameServer.Simulation;

/// <summary>
/// Placeholder until the real AetherNet adapter lands. Advances a tick
/// counter and emits empty deltas so the surrounding plumbing
/// (TickLoop, SnapshotEncoder, Group broadcast) can be exercised.
/// </summary>
public sealed class NullServerSimulation : IServerSimulation
{
    public int CurrentTick { get; private set; }

    public void ApplyInput(InputCommand command)
    {
        // No-op for the stub. The real AetherNet adapter feeds the command
        // into the authoritative world.
    }

    public void Step(double deltaSeconds) => CurrentTick++;

    public ReadOnlyMemory<byte> EncodeDelta(int baselineTick) => ReadOnlyMemory<byte>.Empty;

    public void Dispose() { }
}
