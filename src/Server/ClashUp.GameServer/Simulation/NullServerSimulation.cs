using ClashUp.Shared.Maps;
using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Server.GameServer.Simulation;

public sealed class NullServerSimulation : IServerSimulation
{
    public int CurrentTick { get; private set; }
    public uint RandomSeed => 0;

    public void LoadMap(MapData mapData) { }

    public void EnsurePlayer(PlayerId player, int colorSlot, int teamId) { }

    public void ApplyInput(PlayerId player, InputCommand command) { }

    public void Step(double deltaSeconds) => CurrentTick++;

    public ReadOnlyMemory<byte> EncodeDelta(int baselineTick) => ReadOnlyMemory<byte>.Empty;

    public void Dispose() { }
}
