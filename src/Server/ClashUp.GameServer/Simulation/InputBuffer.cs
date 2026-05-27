using System.Collections.Concurrent;
using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Server.GameServer.Simulation;

/// <summary>
/// Bounded ring of input commands for one match. The hub method enqueues;
/// the tick loop drains. See docs/rules/magiconion-hub-discipline.md.
/// </summary>
public sealed class InputBuffer
{
    private readonly ConcurrentQueue<InputCommand> _queue = new();

    public void Enqueue(InputCommand command) => _queue.Enqueue(command);

    public bool TryDequeue(out InputCommand command) => _queue.TryDequeue(out command!);

    public int Count => _queue.Count;
}
