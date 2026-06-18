using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ClashUp.Server.Common.Ccu;

/// <summary>
/// Thread-safe concurrent-user counter, reusable across tiers. An entity (keyed
/// by an arbitrary id — a player id, a hub connection id) is counted from its
/// first <see cref="Add"/> until a disconnect grace period elapses without a
/// re-<see cref="Add"/>; reconnecting within the window keeps it counted. A zero
/// grace removes immediately.
/// </summary>
public sealed class GraceCcuCounter : IDisposable
{
    private readonly TimeSpan _gracePeriod;
    private readonly ILogger _logger;

    // Ids currently counted (value byte unused — set semantics).
    private readonly ConcurrentDictionary<string, byte> _counted = new(StringComparer.Ordinal);

    // Pending grace-period removals keyed by id; cancelled on re-add.
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _pending = new(StringComparer.Ordinal);

    private int _count;
    private bool _disposed;

    public GraceCcuCounter(TimeSpan gracePeriod, ILogger logger)
    {
        _gracePeriod = gracePeriod < TimeSpan.Zero ? TimeSpan.Zero : gracePeriod;
        _logger = logger;
    }

    /// <summary>Current number of counted ids (live + within disconnect grace).</summary>
    public int Count => Volatile.Read(ref _count);

    /// <summary>
    /// Record that an id connected. Cancels any pending grace-period removal
    /// (reconnect); counts a brand-new id exactly once.
    /// </summary>
    public void Add(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        // Reconnect within the grace window: cancel the pending removal, stay counted.
        if (_pending.TryRemove(id, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        if (_counted.TryAdd(id, 0))
        {
            var value = Interlocked.Increment(ref _count);
            _logger.LogDebug("CCU +1 ({Id}) → {Count}", id, value);
        }
    }

    /// <summary>
    /// Record that an id disconnected. Starts the grace timer; the id is removed
    /// from the count only if it does not re-add before the timer elapses.
    /// </summary>
    public void Remove(string id)
    {
        if (string.IsNullOrEmpty(id) || !_counted.ContainsKey(id))
        {
            return;
        }

        var cts = new CancellationTokenSource();

        // Replace any prior pending removal for this id (shouldn't normally happen).
        if (_pending.TryGetValue(id, out var existing) &&
            _pending.TryUpdate(id, cts, existing))
        {
            existing.Cancel();
            existing.Dispose();
        }
        else
        {
            _pending[id] = cts;
        }

        if (_gracePeriod <= TimeSpan.Zero)
        {
            _pending.TryRemove(id, out _);
            cts.Dispose();
            RemoveNow(id);
            return;
        }

        _ = ScheduleRemovalAsync(id, cts);
    }

    private async Task ScheduleRemovalAsync(string id, CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(_gracePeriod, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return; // Re-added within grace — keep counted.
        }

        // Only remove if this exact cts is still the active pending entry.
        if (_pending.TryRemove(id, out var current) && ReferenceEquals(current, cts))
        {
            RemoveNow(id);
        }

        cts.Dispose();
    }

    private void RemoveNow(string id)
    {
        if (_counted.TryRemove(id, out _))
        {
            var value = Interlocked.Decrement(ref _count);
            _logger.LogDebug("CCU -1 ({Id}) → {Count}", id, value);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var cts in _pending.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }

        _pending.Clear();
    }
}
