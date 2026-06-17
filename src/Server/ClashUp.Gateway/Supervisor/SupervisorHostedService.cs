using Microsoft.Extensions.Options;

namespace ClashUp.Server.Gateway.Supervisor;

/// <summary>
/// Drives the supervisor lifecycle: pre-warm configured versions at startup,
/// then health-check / idle-evict on a timer. Spawning on demand is handled by
/// the request path; this only covers proactive upkeep.
/// </summary>
public sealed class SupervisorHostedService : BackgroundService
{
    private readonly IProcessSupervisor _supervisor;
    private readonly GatewayOptions _options;
    private readonly ILogger<SupervisorHostedService> _logger;

    public SupervisorHostedService(
        IProcessSupervisor supervisor,
        IOptions<GatewayOptions> options,
        ILogger<SupervisorHostedService> logger)
    {
        _supervisor = supervisor;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Gateway supervisor starting for tier {Tier} (listen :{ListenPort}, backend image {Image}).",
            _options.Tier, _options.ListenPort, _options.ImageRepository);

        await _supervisor.PrewarmAsync(stoppingToken).ConfigureAwait(false);

        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.MaintenanceIntervalSeconds));
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await _supervisor.RunMaintenanceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Supervisor maintenance cycle failed");
            }
        }
    }
}
