using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Server.GameServer.Simulation;

public interface IServerSimulation : IDisposable
{
    int CurrentTick { get; }

    void EnsurePlayer(PlayerId player, int colorSlot);

    void ApplyInput(PlayerId player, InputCommand command);

    void Step(double deltaSeconds);

    ReadOnlyMemory<byte> EncodeDelta(int baselineTick);
}
