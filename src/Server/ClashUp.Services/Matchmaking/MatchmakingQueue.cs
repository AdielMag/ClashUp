using System.Collections.Concurrent;
using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Server.Services.Matchmaking;

/// <summary>
/// In-memory FIFO queue + ticket tracking. Phase-1 implementation; in
/// production this would back to Redis so Services can scale horizontally.
/// </summary>
public sealed class MatchmakingQueue
{
    private readonly ConcurrentQueue<TicketEntry> _queue = new();
    private readonly ConcurrentDictionary<string, TicketEntry> _tickets = new();

    public TicketEntry Enqueue(string playerId, string modeId)
    {
        var entry = new TicketEntry
        {
            TicketId = Guid.NewGuid().ToString("N"),
            PlayerId = playerId,
            ModeId = modeId,
            Status = TicketStatus.Queued,
            EnqueuedAt = DateTime.UtcNow,
        };
        _tickets[entry.TicketId] = entry;
        _queue.Enqueue(entry);
        return entry;
    }

    public bool TryGet(string ticketId, out TicketEntry entry) =>
        _tickets.TryGetValue(ticketId, out entry!);

    public bool TryCancel(string ticketId)
    {
        if (!_tickets.TryGetValue(ticketId, out var entry))
        {
            return false;
        }
        entry.Status = TicketStatus.Cancelled;
        return true;
    }

    /// <summary>
    /// Drain up to <paramref name="batchSize"/> still-queued tickets in
    /// FIFO order. Cancelled or already-matched tickets are skipped.
    /// </summary>
    public List<TicketEntry>? TryDrain(int batchSize)
    {
        var batch = new List<TicketEntry>(batchSize);
        while (batch.Count < batchSize && _queue.TryPeek(out var head))
        {
            if (!_queue.TryDequeue(out var entry))
            {
                break;
            }
            if (entry.Status == TicketStatus.Queued)
            {
                batch.Add(entry);
            }
        }
        return batch.Count == batchSize ? batch : Reenqueue(batch);
    }

    private List<TicketEntry>? Reenqueue(List<TicketEntry> partial)
    {
        foreach (var entry in partial)
        {
            _queue.Enqueue(entry);
        }
        return null;
    }
}

public sealed class TicketEntry
{
    public string TicketId { get; init; } = string.Empty;
    public string PlayerId { get; init; } = string.Empty;
    public string ModeId { get; init; } = string.Empty;
    public DateTime EnqueuedAt { get; init; }
    public TicketStatus Status { get; set; }
    public MatchHandoff? Handoff { get; set; }
    public string? FailureReason { get; set; }
}
