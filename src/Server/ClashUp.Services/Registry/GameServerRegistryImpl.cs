using ClashUp.Server.Services.Persistence;
using ClashUp.Shared.MessagePackObjects;
using ClashUp.Shared.Services;
using MagicOnion;
using MagicOnion.Server;

namespace ClashUp.Server.Services.Registry;

/// <summary>
/// Services-side implementation of IGameServerRegistry. Called by GS
/// instances over the wire. Inter-tier JWT validation is a TODO for the
/// hardening step — see docs/rules/jwt-auth.md.
/// </summary>
public sealed class GameServerRegistryImpl : ServiceBase<IGameServerRegistry>, IGameServerRegistry
{
    private readonly IGameServerInstanceRepository _repository;
    private readonly ILogger<GameServerRegistryImpl> _logger;

    public GameServerRegistryImpl(IGameServerInstanceRepository repository, ILogger<GameServerRegistryImpl> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async UnaryResult<GsToken> RegisterAsync(GsRegistration registration)
    {
        var ct = Context.CallContext.CancellationToken;
        var doc = new GameServerInstanceDoc
        {
            InstanceId = registration.InstanceId,
            PublicEndpoint = registration.PublicEndpoint,
            CapacityMax = registration.CapacityMax,
            CapacityUsed = 0,
            Version = registration.Version,
            LastHeartbeatAt = DateTime.UtcNow,
            Status = "Healthy",
        };
        await _repository.UpsertAsync(doc, ct);
        _logger.LogInformation("GS registered: {InstanceId} at {Endpoint}", registration.InstanceId, registration.PublicEndpoint);

        // TODO: mint a real inter-tier JWT here. For now hand back a placeholder.
        return new GsToken
        {
            InstanceId = registration.InstanceId,
            BearerJwt = $"placeholder-{Guid.NewGuid():N}",
        };
    }

    public async UnaryResult HeartbeatAsync(GsHeartbeat heartbeat)
    {
        await _repository.UpdateHeartbeatAsync(
            heartbeat.InstanceId,
            heartbeat.CapacityUsed,
            heartbeat.CpuLoad,
            Context.CallContext.CancellationToken);
    }

    public UnaryResult ReportMatchStartedAsync(GsMatchStarted notice)
    {
        _logger.LogInformation("Match started: {MatchId} on {InstanceId}", notice.MatchId, notice.InstanceId);
        return default;
    }

    public UnaryResult ReportMatchEndedAsync(GsMatchEnded notice)
    {
        _logger.LogInformation("Match ended: {MatchId} on {InstanceId}", notice.MatchId, notice.InstanceId);
        return default;
    }

    public async UnaryResult MarkDrainingAsync(string instanceId)
    {
        await _repository.MarkStatusAsync(instanceId, "Draining", Context.CallContext.CancellationToken);
        _logger.LogInformation("GS marked draining: {InstanceId}", instanceId);
    }
}
