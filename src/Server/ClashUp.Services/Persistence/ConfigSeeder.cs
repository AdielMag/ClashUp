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
        var doc = new ConfigDoc
        {
            Key = key,
            Value = """{"NumberOfTeams":1,"TeamSize":1,"DurationSeconds":20,"ObjectiveType":"survival"}""",
        };
        await _configs.UpsertAsync(doc, cancellationToken);
        _logger.LogInformation("Seeded config '{Key}'", key);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
