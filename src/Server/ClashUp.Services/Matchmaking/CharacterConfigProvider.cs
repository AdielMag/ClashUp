using System.Collections.Concurrent;
using System.Text.Json;
using ClashUp.Server.Services.Persistence;
using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Server.Services.Matchmaking;

public sealed class CharacterConfigProvider
{
    private readonly IConfigRepository _configs;
    private readonly ConcurrentDictionary<string, (CharactersConfig Config, DateTime ExpiresAt)> _cache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public CharacterConfigProvider(IConfigRepository configs)
    {
        _configs = configs;
    }

    public async Task<CharactersConfig> GetAsync(CancellationToken ct = default)
    {
        const string key = "characters:registry";
        if (_cache.TryGetValue(key, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
            return cached.Config;

        var doc = await _configs.GetByKeyAsync(key, ct);
        if (doc is null)
        {
            var fallback = CharactersConfig.Default;
            _cache[key] = (fallback, DateTime.UtcNow.Add(CacheTtl));
            return fallback;
        }

        var config = JsonSerializer.Deserialize<CharactersConfig>(doc.Value, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? CharactersConfig.Default;

        _cache[key] = (config, DateTime.UtcNow.Add(CacheTtl));
        return config;
    }
}
