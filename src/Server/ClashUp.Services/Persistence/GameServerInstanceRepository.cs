using ClashUp.Server.Common.Mongo;
using MongoDB.Driver;

namespace ClashUp.Server.Services.Persistence;

public sealed class GameServerInstanceRepository : IGameServerInstanceRepository
{
    public const string CollectionName = "gs_instances";

    private readonly IMongoCollection<GameServerInstanceDoc> _collection;

    public GameServerInstanceRepository(IMongoContext mongo)
    {
        _collection = mongo.GetCollection<GameServerInstanceDoc>(CollectionName);
    }

    internal IMongoCollection<GameServerInstanceDoc> Collection => _collection;

    public Task<GameServerInstanceDoc?> GetByIdAsync(string instanceId, CancellationToken cancellationToken) =>
        _collection
            .Find(x => x.InstanceId == instanceId)
            .FirstOrDefaultAsync(cancellationToken)!;

    public Task UpsertAsync(GameServerInstanceDoc doc, CancellationToken cancellationToken) =>
        _collection.ReplaceOneAsync(
            x => x.InstanceId == doc.InstanceId,
            doc,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);

    public Task UpdateHeartbeatAsync(string instanceId, int capacityUsed, double cpuLoad, CancellationToken cancellationToken)
    {
        var update = Builders<GameServerInstanceDoc>.Update
            .Set(x => x.CapacityUsed, capacityUsed)
            .Set(x => x.LastHeartbeatAt, DateTime.UtcNow);
        return _collection.UpdateOneAsync(x => x.InstanceId == instanceId, update, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<GameServerInstanceDoc>> ListHealthyAsync(CancellationToken cancellationToken) =>
        await _collection
            .Find(x => x.Status == "Healthy")
            .ToListAsync(cancellationToken);

    public Task MarkStatusAsync(string instanceId, string status, CancellationToken cancellationToken)
    {
        var update = Builders<GameServerInstanceDoc>.Update.Set(x => x.Status, status);
        return _collection.UpdateOneAsync(x => x.InstanceId == instanceId, update, cancellationToken: cancellationToken);
    }

    public Task DrainOthersByEndpointAsync(string excludeInstanceId, string internalEndpoint, CancellationToken cancellationToken)
    {
        var filter = Builders<GameServerInstanceDoc>.Filter.And(
            Builders<GameServerInstanceDoc>.Filter.Ne(x => x.InstanceId, excludeInstanceId),
            Builders<GameServerInstanceDoc>.Filter.Eq(x => x.InternalEndpoint, internalEndpoint));
        var update = Builders<GameServerInstanceDoc>.Update.Set(x => x.Status, "Draining");
        return _collection.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
    }
}
