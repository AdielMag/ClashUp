namespace ClashUp.Server.Services.Persistence;

public interface IConfigRepository
{
    Task<ConfigDoc?> GetByKeyAsync(string key, CancellationToken ct = default);
    Task UpsertAsync(ConfigDoc doc, CancellationToken ct = default);
}
