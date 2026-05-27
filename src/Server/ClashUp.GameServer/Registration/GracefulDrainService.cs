using ClashUp.Server.GameServer.Match;

namespace ClashUp.Server.GameServer.Registration;

/// <summary>
/// On SIGTERM / host shutdown, marks this GS as Draining with the
/// Services tier (matchmaker stops placing new matches here) and waits
/// for in-flight matches to drain before the process exits.
/// </summary>
public sealed class GracefulDrainService : IHostedService
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IServicesRegistryClient _registry;
    private readonly IMatchRegistry _matches;
    private readonly GameServerIdentity _identity;
    private readonly ILogger<GracefulDrainService> _logger;

    public GracefulDrainService(
        IHostApplicationLifetime lifetime,
        IServicesRegistryClient registry,
        IMatchRegistry matches,
        GameServerIdentity identity,
        ILogger<GracefulDrainService> logger)
    {
        _lifetime = lifetime;
        _registry = registry;
        _matches = matches;
        _identity = identity;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _lifetime.ApplicationStopping.Register(OnStopping);
        return Task.CompletedTask;
    }

    private void OnStopping()
    {
        if (string.IsNullOrEmpty(_identity.InstanceId))
        {
            return;
        }

        try
        {
            // Use a short bounded timeout — the host is already trying to exit.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            _registry.MarkDrainingAsync(_identity.InstanceId, cts.Token).GetAwaiter().GetResult();
            _logger.LogInformation("Marked Draining with Services. Waiting for {Count} match(es) to finish.", _matches.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to mark Draining with Services on shutdown");
        }

        // Bounded wait so we don't hold the host open forever if a match is stuck.
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (_matches.Count > 0 && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(250));
        }

        if (_matches.Count > 0)
        {
            _logger.LogWarning("Drain timeout: {Count} match(es) still active at shutdown.", _matches.Count);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
