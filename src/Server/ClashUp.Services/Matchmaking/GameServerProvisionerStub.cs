using ClashUp.Shared.Services;

namespace ClashUp.Server.Services.Matchmaking;

/// <summary>
/// Phase-1 placeholder for IGameServerProvisioner. Returns "not started"
/// with a reason so the matchmaker can fail the ticket cleanly. The real
/// implementation will spin up a GS instance via k8s/docker/cloud API.
/// </summary>
public sealed class GameServerProvisionerStub : IGameServerProvisioner
{
    private readonly ILogger<GameServerProvisionerStub> _logger;

    public GameServerProvisionerStub(ILogger<GameServerProvisionerStub> logger)
    {
        _logger = logger;
    }

    public Task<ProvisionerResponse> RequestNewInstanceAsync(CancellationToken cancellationToken)
    {
        // TODO: spin up a new GS instance (k8s/docker/cloud API).
        _logger.LogWarning("IGameServerProvisioner.RequestNewInstanceAsync stub called — no GS will be created.");
        return Task.FromResult(new ProvisionerResponse
        {
            Started = false,
            Reason = "Provisioner not implemented (phase 1 stub).",
        });
    }
}
