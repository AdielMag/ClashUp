namespace ClashUp.Server.Services.Persistence;

public interface IGameServerInstanceRepository
{
    Task<GameServerInstanceDoc?> GetByIdAsync(string instanceId, CancellationToken cancellationToken);

    Task UpsertAsync(GameServerInstanceDoc doc, CancellationToken cancellationToken);

    Task UpdateHeartbeatAsync(string instanceId, int capacityUsed, double cpuLoad, CancellationToken cancellationToken);

    Task<IReadOnlyList<GameServerInstanceDoc>> ListHealthyAsync(CancellationToken cancellationToken);

    Task MarkStatusAsync(string instanceId, string status, CancellationToken cancellationToken);

    Task DrainOthersByEndpointAsync(string excludeInstanceId, string internalEndpoint, CancellationToken cancellationToken);
}
