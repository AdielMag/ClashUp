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

    /// <summary>Distinct modes that currently have at least one still-queued ticket.</summary>
    public IReadOnlyCollection<string> GetQueuedModeIds()
    {
        var modes = new HashSet<string>();
        foreach (var entry in _queue)
            if (entry.Status == TicketStatus.Queued)
                modes.Add(entry.ModeId);
        return modes;
    }

    /// <summary>
    /// Drain up to <paramref name="batchSize"/> still-queued tickets for a SINGLE
    /// <paramref name="modeId"/>, in FIFO order. Cancelled/matched tickets are dropped; tickets
    /// for other modes are preserved (re-enqueued). Returns null (and re-enqueues) if fewer than
    /// a full batch of this mode is available.
    /// </summary>
    public List<TicketEntry>? TryDrain(int batchSize, string modeId)
    {
        var batch = new List<TicketEntry>(batchSize);
        var playerIds = new HashSet<string>(batchSize);
        var skipped = new List<TicketEntry>(); // valid tickets of other modes — keep them

        while (batch.Count < batchSize && _queue.TryDequeue(out var entry))
        {
            if (entry.Status != TicketStatus.Queued)
                continue; // cancelled / already matched — drop
            if (entry.ModeId != modeId)
            {
                skipped.Add(entry);
                continue;
            }
            if (playerIds.Add(entry.PlayerId))
                batch.Add(entry);
        }

        foreach (var entry in skipped)
            _queue.Enqueue(entry);

        if (batch.Count == batchSize)
            return batch;

        return Reenqueue(batch);
    }

    private List<TicketEntry>? Reenqueue(List<TicketEntry> partial)
    {
        foreach (var entry in partial)
        {
            _queue.Enqueue(entry);
        }
        return null;
    }

    /// <summary>Number of still-queued tickets for a single mode.</summary>
    public int CountQueued(string modeId)
    {
        int count = 0;
        foreach (var entry in _queue)
            if (entry.Status == TicketStatus.Queued && entry.ModeId == modeId)
                count++;
        return count;
    }

    /// <summary>Enqueue time of the oldest still-queued ticket for a mode (drives the bot-fill wait timer).</summary>
    public DateTime? OldestEnqueuedAt(string modeId)
    {
        DateTime? oldest = null;
        foreach (var entry in _queue)
        {
            if (entry.Status != TicketStatus.Queued || entry.ModeId != modeId) continue;
            if (oldest is null || entry.EnqueuedAt < oldest)
                oldest = entry.EnqueuedAt;
        }
        return oldest;
    }

    /// <summary>
    /// Drain 1..<paramref name="max"/> still-queued tickets for a single <paramref name="modeId"/> in FIFO
    /// order. Unlike <see cref="TryDrain"/> this does NOT require a full batch and does NOT re-enqueue the
    /// partial result — used by bot-fill once the wait timer has elapsed. Tickets for other modes are
    /// preserved; cancelled/matched tickets are dropped. Returns an empty list when none are available.
    /// </summary>
    public List<TicketEntry> DrainUpTo(int max, string modeId)
    {
        var batch = new List<TicketEntry>(max);
        var playerIds = new HashSet<string>(max);
        var skipped = new List<TicketEntry>(); // valid tickets of other modes — keep them

        while (batch.Count < max && _queue.TryDequeue(out var entry))
        {
            if (entry.Status != TicketStatus.Queued)
                continue; // cancelled / already matched — drop
            if (entry.ModeId != modeId)
            {
                skipped.Add(entry);
                continue;
            }
            if (playerIds.Add(entry.PlayerId))
                batch.Add(entry);
        }

        foreach (var entry in skipped)
            _queue.Enqueue(entry);

        return batch;
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
