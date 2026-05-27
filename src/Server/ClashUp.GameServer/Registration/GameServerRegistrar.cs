using ClashUp.Server.GameServer.Match;
using ClashUp.Shared.MessagePackObjects;
using Microsoft.Extensions.Options;

namespace ClashUp.Server.GameServer.Registration;

/// <summary>
/// IHostedService that registers this GS with the Services tier when
/// the host starts. After successful registration the GameServerIdentity
/// is populated so the heartbeat service can pick it up.
/// </summary>
public sealed class GameServerRegistrar : IHostedService
{
    private readonly IServicesRegistryClient _registry;
    private readonly GameServerOptions _options;
    private readonly GameServerIdentity _identity;
    private readonly ILogger<GameServerRegistrar> _logger;

    public GameServerRegistrar(
        IServicesRegistryClient registry,
        IOptions<GameServerOptions> options,
        GameServerIdentity identity,
        ILogger<GameServerRegistrar> logger)
    {
        _registry = registry;
        _options = options.Value;
        _identity = identity;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var instanceId = string.IsNullOrWhiteSpace(_options.InstanceId)
            ? Guid.NewGuid().ToString("N")
            : _options.InstanceId;

        var registration = new GsRegistration
        {
            InstanceId = instanceId,
            PublicEndpoint = _options.PublicEndpoint,
            CapacityMax = _options.MaxConcurrentMatches,
            Version = "0.0.1",
        };

        try
        {
            var token = await _registry.RegisterAsync(registration, cancellationToken);
            _identity.InstanceId = token.InstanceId;
            _identity.BearerJwt = token.BearerJwt;
            _logger.LogInformation("Registered with Services as {InstanceId}", token.InstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GS registration failed; will retry on heartbeat tick");
            _identity.InstanceId = instanceId;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
