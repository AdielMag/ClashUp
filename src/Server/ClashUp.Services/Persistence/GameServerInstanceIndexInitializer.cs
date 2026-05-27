using ClashUp.Server.Common.Mongo;
using MongoDB.Driver;

namespace ClashUp.Server.Services.Persistence;

public sealed class GameServerInstanceIndexInitializer : IIndexInitializer
{
    private readonly GameServerInstanceRepository _repository;

    public GameServerInstanceIndexInitializer(IGameServerInstanceRepository repository)
    {
        // Cast is safe — we own the only impl. If swapped for tests, the test repo can no-op EnsureIndexes.
        _repository = (GameServerInstanceRepository)repository;
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var keys = Builders<GameServerInstanceDoc>.IndexKeys.Ascending(x => x.LastHeartbeatAt);
        var model = new CreateIndexModel<GameServerInstanceDoc>(
            keys,
            new CreateIndexOptions { Name = "ix_lastHeartbeatAt" });
        await _repository.Collection.Indexes.CreateOneAsync(model, cancellationToken: cancellationToken);
    }
}
