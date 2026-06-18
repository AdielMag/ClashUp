using ClashUp.Server.Common.Ccu;
using Microsoft.Extensions.Logging;

namespace ClashUp.Server.Services.Ccu;

/// <summary>
/// Tracks Concurrent Connected Users (CCU) for this Services instance as the
/// number of live client streaming-hub sessions (one per connected player in
/// lobby/matchmaking). This is the per-instance scaling signal consumed by the
/// Services MIG autoscaler via <c>custom.googleapis.com/services/ccu</c>.
///
/// Keyed by hub connection id: a reconnect is a new connection, so no grace is
/// applied (a brief disconnect dips the count rather than leaving a phantom).
/// </summary>
public sealed class ServicesCcuTracker : ICcuSource, IDisposable
{
    private readonly GraceCcuCounter _counter;

    public ServicesCcuTracker(ILogger<ServicesCcuTracker> logger)
    {
        _counter = new GraceCcuCounter(TimeSpan.Zero, logger);
    }

    public int CurrentCcu => _counter.Count;

    /// <summary>Record that a client hub connection opened.</summary>
    public void Connected(string connectionId) => _counter.Add(connectionId);

    /// <summary>Record that a client hub connection closed.</summary>
    public void Disconnected(string connectionId) => _counter.Remove(connectionId);

    public void Dispose() => _counter.Dispose();
}
