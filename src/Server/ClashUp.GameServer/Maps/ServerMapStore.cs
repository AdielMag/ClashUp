using System.Text.Json;
using ClashUp.Shared.Maps;

namespace ClashUp.Server.GameServer.Maps;

public sealed class ServerMapStore
{
    private readonly Dictionary<string, MapData> _maps = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<ServerMapStore> _logger;

    public ServerMapStore(ILogger<ServerMapStore> logger)
    {
        _logger = logger;
        LoadAll();
    }

    public MapData? GetMap(string mapId)
    {
        return _maps.TryGetValue(mapId, out var map) ? map : null;
    }

    private void LoadAll()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Maps", "Data");
        if (!Directory.Exists(dir))
        {
            _logger.LogWarning("Map data directory not found: {Dir}", dir);
            return;
        }

        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var map = JsonSerializer.Deserialize<MapData>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });
                if (map == null) continue;

                var key = Path.GetFileNameWithoutExtension(file);
                _maps[key] = map;
                _logger.LogInformation("Loaded map '{MapId}' with {Count} entities", key, map.Entities.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load map from {File}", file);
            }
        }
    }
}
