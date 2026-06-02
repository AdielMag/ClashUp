using System.Collections.Concurrent;
using System.Text.Json;
using ClashUp.Server.Services.Persistence;
using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Server.Services.Matchmaking;

public sealed class MatchConfigProvider
{
    private readonly IConfigRepository _configs;
    private readonly ConcurrentDictionary<string, (MatchConfig Config, DateTime ExpiresAt)> _cache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public MatchConfigProvider(IConfigRepository configs)
    {
        _configs = configs;
    }

    public async Task<MatchConfig> GetAsync(string modeId, CancellationToken ct = default)
    {
        var key = $"match:{modeId}";
        if (_cache.TryGetValue(key, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
        {
            return cached.Config;
        }

        var doc = await _configs.GetByKeyAsync(key, ct);
        if (doc is null)
        {
            var fallback = new MatchConfig();
            _cache[key] = (fallback, DateTime.UtcNow.Add(CacheTtl));
            return fallback;
        }

        var config = JsonSerializer.Deserialize<MatchConfig>(doc.Value, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? new MatchConfig();

        _cache[key] = (config, DateTime.UtcNow.Add(CacheTtl));
        return config;
    }
}
