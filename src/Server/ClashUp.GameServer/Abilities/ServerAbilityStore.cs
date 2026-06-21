using System.Text.Json;
using ClashUp.Shared.Abilities;
using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Server.GameServer.Abilities;

public sealed class ServerAbilityStore
{
    private readonly Dictionary<string, AbilityDefinition> _abilities = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<ServerAbilityStore> _logger;

    public ServerAbilityStore(ILogger<ServerAbilityStore> logger)
    {
        _logger = logger;
        LoadAll();
    }

    public AbilityDefinition? GetAbility(string abilityId) =>
        _abilities.TryGetValue(abilityId, out var def) ? def : null;

    public IEnumerable<AbilityDefinition> All => _abilities.Values;

    public AbilitiesConfig BuildClientConfig() => new()
    {
        Abilities = _abilities.Values.Select(d => new AbilityClientInfo
        {
            Id = d.Id,
            Telegraph = d.Telegraph,
            AutoRange = d.AutoRange,
            CastMode = d.CastMode,
            CastShape = DeriveCastShape(d.RootNode),
        }).ToList(),
    };

    // The triggered cast flash should render the exact damage footprint, so derive its shape
    // directly from the ability's root hitbox (single source of truth with the damage logic).
    private static TelegraphConfig? DeriveCastShape(AbilityNode? root)
    {
        // Projectile-rooted abilities (e.g. the Mage) flash a circle at the impact point: the AoE
        // radius when they explode, otherwise a small marker the size of the projectile.
        if (root is { Type: AbilityNodeType.Projectile, Projectile: { } projectile })
        {
            return new TelegraphConfig
            {
                Shape = TelegraphShape.TargetCircle,
                Radius = projectile.AoeRadius > 0f ? projectile.AoeRadius : MathF.Max(projectile.Radius, 0.3f),
            };
        }

        if (root is not { Type: AbilityNodeType.Hitbox, Hitbox: { } hitbox })
            return null;

        return hitbox.Shape switch
        {
            HitboxShape.Capsule => new TelegraphConfig
            {
                Shape = TelegraphShape.Capsule,
                Length = hitbox.Length,
                Width = hitbox.Radius * 2f,
            },
            HitboxShape.Cone => new TelegraphConfig
            {
                Shape = TelegraphShape.ForwardCone,
                Length = hitbox.Length,
                Angle = hitbox.Angle,
            },
            _ => new TelegraphConfig
            {
                Shape = TelegraphShape.TargetCircle,
                Radius = hitbox.Radius,
            },
        };
    }

    private void LoadAll()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Abilities", "Data");
        if (!Directory.Exists(dir))
        {
            _logger.LogWarning("Abilities data directory not found: {Dir}", dir);
            return;
        }

        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        };
        foreach (var file in Directory.GetFiles(dir, "ability_*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var def = JsonSerializer.Deserialize<AbilityDefinition>(json, opts);
                if (def == null) continue;

                _abilities[def.Id.Value] = def;
                _logger.LogInformation("Loaded ability '{AbilityId}'", def.Id.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load ability from {File}", file);
            }
        }
    }
}
