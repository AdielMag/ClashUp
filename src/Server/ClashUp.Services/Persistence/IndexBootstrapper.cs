using ClashUp.Server.Common.Mongo;

namespace ClashUp.Server.Services.Persistence;

/// <summary>
/// Runs all IIndexInitializer registrations at startup. Idempotent — Mongo
/// CreateIndex returns success for an index that already exists with the
/// same key+name.
/// </summary>
public sealed class IndexBootstrapper : IHostedService
{
    private readonly IEnumerable<IIndexInitializer> _initializers;
    private readonly ILogger<IndexBootstrapper> _logger;

    public IndexBootstrapper(IEnumerable<IIndexInitializer> initializers, ILogger<IndexBootstrapper> logger)
    {
        _initializers = initializers;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var initializer in _initializers)
        {
            try
            {
                await initializer.EnsureIndexesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Index initializer {Initializer} failed", initializer.GetType().Name);
                throw;
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
