using System.Collections.Concurrent;
using ClashUp.Server.GameServer.Match;
using Microsoft.Extensions.Options;

namespace ClashUp.Server.GameServer.Ccu;

/// <summary>
/// Thread-safe <see cref="ICcuTracker"/>. A player is counted from their first
/// connect until a disconnect grace period elapses without a reconnect.
/// </summary>
public sealed class CcuTracker : ICcuTracker, IDisposable
{
    private readonly TimeSpan _gracePeriod;
    private readonly ILogger<CcuTracker> _logger;

    // Players currently counted toward CCU (value byte is unused — set semantics).
    private readonly ConcurrentDictionary<string, byte> _counted = new(StringComparer.Ordinal);

    // Pending grace-period removals keyed by player id; cancelled on reconnect.
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _pending = new(StringComparer.Ordinal);

    private int _ccu;
    private bool _disposed;

    public CcuTracker(IOptions<GameServerOptions> options, ILogger<CcuTracker> logger)
    {
        _gracePeriod = TimeSpan.FromSeconds(Math.Max(0, options.Value.CcuGracePeriodSeconds));
        _logger = logger;
    }

    public int CurrentCcu => Volatile.Read(ref _ccu);

    public void PlayerConnected(string playerId)
    {
        if (string.IsNullOrEmpty(playerId))
        {
            return;
        }

        // Reconnect within the grace window: cancel the pending removal, stay counted.
        if (_pending.TryRemove(playerId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        // Count a brand-new player exactly once.
        if (_counted.TryAdd(playerId, 0))
        {
            var value = Interlocked.Increment(ref _ccu);
            _logger.LogDebug("CCU +1 ({PlayerId}) → {Ccu}", playerId, value);
        }
    }

    public void PlayerDisconnected(string playerId)
    {
        if (string.IsNullOrEmpty(playerId) || !_counted.ContainsKey(playerId))
        {
            return;
        }

        var cts = new CancellationTokenSource();

        // Replace any prior pending removal for this player (shouldn't normally happen).
        if (_pending.TryGetValue(playerId, out var existing) &&
            _pending.TryUpdate(playerId, cts, existing))
        {
            existing.Cancel();
            existing.Dispose();
        }
        else
        {
            _pending[playerId] = cts;
        }

        if (_gracePeriod <= TimeSpan.Zero)
        {
            Remove(playerId);
            return;
        }

        _ = ScheduleRemovalAsync(playerId, cts);
    }

    private async Task ScheduleRemovalAsync(string playerId, CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(_gracePeriod, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return; // Reconnected within grace — keep counted.
        }

        // Only remove if this exact cts is still the active pending entry.
        if (_pending.TryRemove(playerId, out var current) && ReferenceEquals(current, cts))
        {
            Remove(playerId);
        }

        cts.Dispose();
    }

    private void Remove(string playerId)
    {
        if (_counted.TryRemove(playerId, out _))
        {
            var value = Interlocked.Decrement(ref _ccu);
            _logger.LogDebug("CCU -1 ({PlayerId}) → {Ccu}", playerId, value);
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
