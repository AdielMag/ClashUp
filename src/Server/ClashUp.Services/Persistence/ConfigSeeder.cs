namespace ClashUp.Server.Services.Persistence;

public sealed class ConfigSeeder : IHostedService
{
    private readonly IConfigRepository _configs;
    private readonly ILogger<ConfigSeeder> _logger;

    public ConfigSeeder(IConfigRepository configs, ILogger<ConfigSeeder> logger)
    {
        _configs = configs;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        const string key = "match:default";
        var existing = await _configs.GetByKeyAsync(key, cancellationToken);
        if (existing is not null)
        {
            _logger.LogInformation("Config '{Key}' already exists, skipping seed", key);
            return;
        }

        var doc = new ConfigDoc
        {
            Key = key,
            Value = """{"NumberOfTeams":1,"TeamSize":1,"DurationSeconds":120,"ObjectiveType":"survival"}""",
        };
        await _configs.UpsertAsync(doc, cancellationToken);
        _logger.LogInformation("Seeded config '{Key}'", key);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
