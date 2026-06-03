using System.Collections.Concurrent;
using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Server.GameServer.Simulation;

public readonly record struct PlayerInput(PlayerId Player, InputCommand Command);

public sealed class InputBuffer
{
    private readonly ConcurrentQueue<PlayerInput> _queue = new();

    public void Enqueue(PlayerInput input) => _queue.Enqueue(input);

    public bool TryDequeue(out PlayerInput input) => _queue.TryDequeue(out input!);

    public int Count => _queue.Count;
}
