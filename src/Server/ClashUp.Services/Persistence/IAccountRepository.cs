namespace ClashUp.Server.Services.Persistence;

public interface IAccountRepository
{
    Task<AccountDoc?> GetByDeviceIdAsync(string deviceId, CancellationToken cancellationToken);
    Task<AccountDoc> CreateAsync(AccountDoc doc, CancellationToken cancellationToken);
    Task<AccountDoc?> GetByIdAsync(string playerId, CancellationToken cancellationToken);
}
