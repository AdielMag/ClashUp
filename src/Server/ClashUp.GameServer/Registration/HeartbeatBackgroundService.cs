using ClashUp.Server.GameServer.Match;
using ClashUp.Shared.MessagePackObjects;
using Microsoft.Extensions.Options;

namespace ClashUp.Server.GameServer.Registration;

/// <summary>
/// Periodically reports capacity to the Services tier. Interval is
/// GameServerOptions.HeartbeatIntervalSeconds (default 2s). See
/// docs/rules/async-discipline.md re: linking to the host stopping token.
/// </summary>
public sealed class HeartbeatBackgroundService : BackgroundService
{
    private readonly IServicesRegistryClient _registry;
    private readonly IMatchRegistry _matches;
    private readonly GameServerIdentity _identity;
    private readonly GameServerOptions _options;
    private readonly ILogger<HeartbeatBackgroundService> _logger;

    public HeartbeatBackgroundService(
        IServicesRegistryClient registry,
        IMatchRegistry matches,
        GameServerIdentity identity,
        IOptions<GameServerOptions> options,
        ILogger<HeartbeatBackgroundService> logger)
    {
        _registry = registry;
        _matches = matches;
        _identity = identity;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.HeartbeatIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (string.IsNullOrEmpty(_identity.InstanceId))
            {
                continue;
            }

            // Retry registration if the initial attempt failed
            if (string.IsNullOrEmpty(_identity.BearerJwt))
            {
                try
                {
                    var internalEndpoint = string.IsNullOrWhiteSpace(_options.InternalEndpoint)
                        ? _options.PublicEndpoint
                        : _options.InternalEndpoint;
                    var token = await _registry.RegisterAsync(
                        new GsRegistration
                        {
                            InstanceId = _identity.InstanceId,
                            PublicEndpoint = _options.PublicEndpoint,
                            InternalEndpoint = internalEndpoint,
                            CapacityMax = _options.MaxConcurrentMatches,
                            Version = "0.0.1",
                        },
                        stoppingToken);
                    _identity.BearerJwt = token.BearerJwt;
                    _logger.LogInformation("Retry registration succeeded as {InstanceId}", token.InstanceId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Retry registration failed; will try again next tick");
                    continue;
                }
            }

            try
            {
                await _registry.HeartbeatAsync(
                    new GsHeartbeat
                    {
                        InstanceId = _identity.InstanceId,
                        CapacityUsed = _matches.Count,
                        CpuLoad = 0,
                    },
                    stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Heartbeat failed");
            }
        }
    }
}
