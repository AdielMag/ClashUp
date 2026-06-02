using ClashUp.Server.Common.Mongo;
using MongoDB.Driver;

namespace ClashUp.Server.Services.Persistence;

public sealed class ConfigRepository : IConfigRepository
{
    private readonly IMongoCollection<ConfigDoc> _collection;

    public ConfigRepository(IMongoContext mongo)
    {
        _collection = mongo.Database.GetCollection<ConfigDoc>("configs");
    }

    public async Task<ConfigDoc?> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        return await _collection
            .Find(d => d.Key == key)
            .FirstOrDefaultAsync(ct);
    }

    public async Task UpsertAsync(ConfigDoc doc, CancellationToken ct = default)
    {
        var filter = Builders<ConfigDoc>.Filter.Eq(d => d.Key, doc.Key);
        await _collection.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true }, ct);
    }
}
