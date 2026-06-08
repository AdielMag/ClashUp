using ClashUp.Shared.Maps;
using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Server.GameServer.Simulation;

public interface IServerSimulation : IDisposable
{
    int CurrentTick { get; }

    uint RandomSeed { get; }

    void LoadMap(MapData mapData);

    void EnsurePlayer(PlayerId player, int colorSlot, int teamId);

    void ApplyInput(PlayerId player, InputCommand command);

    void Step(double deltaSeconds);

    ReadOnlyMemory<byte> EncodeDelta(int baselineTick);
}
