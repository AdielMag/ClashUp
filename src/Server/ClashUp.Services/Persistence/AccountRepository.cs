using ClashUp.Server.Common.Mongo;
using MongoDB.Driver;

namespace ClashUp.Server.Services.Persistence;

public sealed class AccountRepository : IAccountRepository
{
    public const string CollectionName = "accounts";

    private readonly IMongoCollection<AccountDoc> _collection;

    public AccountRepository(IMongoContext mongo)
    {
        _collection = mongo.GetCollection<AccountDoc>(CollectionName);
    }

    internal IMongoCollection<AccountDoc> Collection => _collection;

    public Task<AccountDoc?> GetByDeviceIdAsync(string deviceId, CancellationToken cancellationToken) =>
        _collection.Find(x => x.DeviceId == deviceId).FirstOrDefaultAsync(cancellationToken)!;

    public Task<AccountDoc?> GetByIdAsync(string playerId, CancellationToken cancellationToken) =>
        _collection.Find(x => x.PlayerId == playerId).FirstOrDefaultAsync(cancellationToken)!;

    public async Task<AccountDoc> CreateAsync(AccountDoc doc, CancellationToken cancellationToken)
    {
        await _collection.InsertOneAsync(doc, cancellationToken: cancellationToken);
        return doc;
    }
}
