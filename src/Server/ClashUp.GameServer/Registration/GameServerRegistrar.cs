using ClashUp.Server.Common;
using ClashUp.Server.Common.Gce;
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
        var instanceId = await ResolveInstanceIdAsync(cancellationToken);
        var publicEndpoint = await ResolvePublicEndpointAsync(cancellationToken);
        var internalEndpoint = string.IsNullOrWhiteSpace(_options.InternalEndpoint)
            ? publicEndpoint
            : _options.InternalEndpoint;

        // Cache resolved identity so the heartbeat retry path stays consistent.
        _identity.InstanceId = instanceId;
        _identity.PublicEndpoint = publicEndpoint;
        _identity.InternalEndpoint = internalEndpoint;

        var registration = new GsRegistration
        {
            InstanceId = instanceId,
            PublicEndpoint = publicEndpoint,
            InternalEndpoint = internalEndpoint,
            CapacityMax = _options.MaxConcurrentMatches,
            Version = ServerVersion.Current,
        };

        try
        {
            var token = await _registry.RegisterAsync(registration, cancellationToken);
            _identity.InstanceId = token.InstanceId;
            _identity.BearerJwt = token.BearerJwt;
            _logger.LogInformation(
                "Registered with Services as {InstanceId} at {Endpoint}", token.InstanceId, publicEndpoint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GS registration failed; will retry on heartbeat tick");
        }
    }

    /// <summary>
    /// Config-provided id wins; otherwise the stable GCE instance id when on
    /// Compute Engine; otherwise a fresh GUID (local/dev).
    /// </summary>
    private async Task<string> ResolveInstanceIdAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.InstanceId))
        {
            return _options.InstanceId;
        }

        var gceId = await GceMetadataHelper.GetInstanceIdAsync(cancellationToken);
        return string.IsNullOrEmpty(gceId) ? Guid.NewGuid().ToString("N") : gceId;
    }

    /// <summary>
    /// Uses the configured endpoint as-is unless it is empty or loopback. In that
    /// case, when running on GCE, substitute the instance's external IP (keeping
    /// the configured port) so clients can actually reach this host.
    /// </summary>
    private async Task<string> ResolvePublicEndpointAsync(CancellationToken cancellationToken)
    {
        var configured = _options.PublicEndpoint;
        if (!IsLoopbackOrEmpty(configured))
        {
            return configured;
        }

        var externalIp = await GceMetadataHelper.GetExternalIpAsync(cancellationToken);
        if (string.IsNullOrEmpty(externalIp))
        {
            return configured; // Off GCE (local/dev) — keep config value.
        }

        var port = TryGetPort(configured) ?? 5101;
        var resolved = $"http://{externalIp}:{port}";
        _logger.LogInformation("Resolved public endpoint from GCE metadata: {Endpoint}", resolved);
        return resolved;
    }

    private static bool IsLoopbackOrEmpty(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return true;
        }

        return endpoint.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            || endpoint.Contains("127.0.0.1", StringComparison.Ordinal)
            || endpoint.Contains("0.0.0.0", StringComparison.Ordinal);
    }

    private static int? TryGetPort(string endpoint)
    {
        return Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && uri.Port > 0 ? uri.Port : null;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
