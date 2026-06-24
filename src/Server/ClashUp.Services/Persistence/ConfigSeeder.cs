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
        await SeedIfMissingAsync(
            "match:default",
            """{"NumberOfTeams":1,"TeamSize":1,"DurationSeconds":20,"ObjectiveType":"survival","MapId":"arena_tdm"}""",
            cancellationToken);

        // No-respawn, point-economy, last-team-standing mode. Teams of 1 (FFA-style), 2+ teams required.
        // Bot-fill enabled: after 10s of waiting, empty slots are filled with AI bots (enemies, since
        // teams differ). Existing Mongo docs must be dropped/updated to pick up these fields.
        await SeedIfMissingAsync(
            "match:elimination",
            """{"NumberOfTeams":2,"TeamSize":1,"DurationSeconds":180,"ObjectiveType":"elimination","MapId":"arena_pickup","FillWithBots":true,"BotFillWaitSeconds":10,"MinRealPlayers":1}""",
            cancellationToken);

        await SeedIfMissingAsync(
            "characters:registry",
            """
            {
              "DefaultCharacterId": "brawler",
              "Characters": [
                {
                  "Id": { "Value": "brawler" },
                  "DisplayName": "Brawler",
                  "BaseStats": {
                    "MaxHealth": 100,
                    "Damage": 10,
                    "MoveSpeed": 5
                  },
                  "AutoAttackId": { "Value": "brawler_punch" },
                  "ActiveAbilityId": { "Value": "brawler_charge" }
                },
                {
                  "Id": { "Value": "mage" },
                  "DisplayName": "Mage",
                  "BaseStats": {
                    "MaxHealth": 70,
                    "Damage": 8,
                    "MoveSpeed": 4.5
                  },
                  "AutoAttackId": { "Value": "mage_bolt" },
                  "ActiveAbilityId": { "Value": "mage_blast" }
                }
              ]
            }
            """,
            cancellationToken);
    }

    private async Task SeedIfMissingAsync(string key, string value, CancellationToken ct)
    {
        var existing = await _configs.GetByKeyAsync(key, ct);
        if (existing is not null)
        {
            _logger.LogInformation("Config '{Key}' already exists, skipping seed", key);
            return;
        }

        await _configs.UpsertAsync(new ConfigDoc { Key = key, Value = value }, ct);
        _logger.LogInformation("Seeded config '{Key}'", key);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
