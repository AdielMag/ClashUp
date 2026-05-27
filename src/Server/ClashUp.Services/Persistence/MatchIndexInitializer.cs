using ClashUp.Server.Common.Mongo;
using MongoDB.Driver;

namespace ClashUp.Server.Services.Persistence;

public sealed class MatchIndexInitializer : IIndexInitializer
{
    private readonly MatchRepository _repository;

    public MatchIndexInitializer(IMatchRepository repository)
    {
        _repository = (MatchRepository)repository;
    }

    public Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var keys = Builders<MatchDoc>.IndexKeys.Ascending("Players.PlayerId");
        var model = new CreateIndexModel<MatchDoc>(
            keys,
            new CreateIndexOptions { Name = "ix_playersPlayerId" });
        return _repository.Collection.Indexes.CreateOneAsync(model, cancellationToken: cancellationToken);
    }
}
