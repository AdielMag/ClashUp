using System.Collections.Concurrent;
using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Server.GameServer.Simulation;

public readonly record struct PlayerInput(PlayerId Player, InputCommand Command);

/// <summary>
/// Per-player input queue. The hub enqueues inputs as they arrive (off the request thread
/// pool); the tick loop consumes exactly one per player per tick, immediately — no playout
/// delay. Latency here is felt directly as post-release overshoot (the server keeps draining
/// buffered "move" inputs after the finger lifts), so we consume as soon as input is
/// available and keep only a tiny backlog: a hard cap with drop-oldest means a fresh "stop"
/// pushes any stale "move" out fast, and the client's continuous send stream keeps the queue
/// near empty. Client and server both run at 30 Hz, so steady-state depth is ~0–1.
/// </summary>
public sealed class InputBuffer
{
    /// <summary>Hard cap; oldest inputs are dropped beyond this so a backlog can't outlive a release.</summary>
    private const int MaxQueueDepth = 2;

    private readonly ConcurrentDictionary<string, ConcurrentQueue<InputCommand>> _queues = new();

    public void Enqueue(PlayerInput input)
    {
        var queue = _queues.GetOrAdd(input.Player.Value, _ => new ConcurrentQueue<InputCommand>());
        queue.Enqueue(input.Command);

        // Overflow: drop oldest so a stale move can't sit in front of a newer stop.
        while (queue.Count > MaxQueueDepth && queue.TryDequeue(out _)) { }
    }

    /// <summary>Consume a single input for the given player. False = nothing queued (caller holds).</summary>
    public bool TryDequeueOne(string playerId, out InputCommand command)
    {
        if (_queues.TryGetValue(playerId, out var queue) && queue.TryDequeue(out command!))
            return true;
        command = default!;
        return false;
    }

    public void Remove(string playerId) => _queues.TryRemove(playerId, out _);
}
