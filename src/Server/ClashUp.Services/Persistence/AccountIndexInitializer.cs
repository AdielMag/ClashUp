using ClashUp.Server.Common.Mongo;
using MongoDB.Driver;

namespace ClashUp.Server.Services.Persistence;

public sealed class AccountIndexInitializer : IIndexInitializer
{
    private readonly AccountRepository _repository;

    public AccountIndexInitializer(IAccountRepository repository)
    {
        _repository = (AccountRepository)repository;
    }

    public Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var keys = Builders<AccountDoc>.IndexKeys.Ascending(x => x.DeviceId);
        var model = new CreateIndexModel<AccountDoc>(
            keys,
            new CreateIndexOptions { Name = "ux_deviceId", Unique = true });
        return _repository.Collection.Indexes.CreateOneAsync(model, cancellationToken: cancellationToken);
    }
}
