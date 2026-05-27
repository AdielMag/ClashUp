using ClashUp.Server.Common.Mongo;
using MongoDB.Driver;

namespace ClashUp.Server.Services.Persistence;

public sealed class MatchRepository : IMatchRepository
{
    public const string CollectionName = "matches";

    private readonly IMongoCollection<MatchDoc> _collection;

    public MatchRepository(IMongoContext mongo)
    {
        _collection = mongo.GetCollection<MatchDoc>(CollectionName);
    }

    internal IMongoCollection<MatchDoc> Collection => _collection;

    public Task InsertAsync(MatchDoc doc, CancellationToken cancellationToken) =>
        _collection.InsertOneAsync(doc, cancellationToken: cancellationToken);

    public Task SetStateAsync(string matchId, string state, CancellationToken cancellationToken)
    {
        var update = Builders<MatchDoc>.Update.Set(x => x.State, state);
        if (state == "Ended")
        {
            update = update.Set(x => x.EndedAt, DateTime.UtcNow);
        }
        return _collection.UpdateOneAsync(x => x.MatchId == matchId, update, cancellationToken: cancellationToken);
    }

    public Task<MatchDoc?> GetByIdAsync(string matchId, CancellationToken cancellationToken) =>
        _collection.Find(x => x.MatchId == matchId).FirstOrDefaultAsync(cancellationToken)!;

    public Task<MatchDoc?> FindActiveForPlayerAsync(string playerId, CancellationToken cancellationToken) =>
        _collection
            .Find(x => x.State == "Active" && x.Players.Any(p => p.PlayerId == playerId))
            .FirstOrDefaultAsync(cancellationToken)!;
}
