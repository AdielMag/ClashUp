using System.Collections.Generic;
using ClashUp.Shared.Simulation;

namespace ClashUp.Server.GameServer.Simulation;

/// <summary>
/// Per-player point economy for objective modes (e.g. "elimination"). Points scale a player's max
/// health (pushed into <see cref="HealthTable"/>) and outgoing damage (queried at hit time). All the
/// actual numbers live in <see cref="PointScaling"/> — this just tracks balances and applies them.
/// </summary>
public sealed class PlayerProgression
{
    private readonly HealthTable _health;
    private readonly Dictionary<string, int> _points = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _baseMaxHealth = new(StringComparer.Ordinal);

    public PlayerProgression(HealthTable health)
    {
        _health = health;
    }

    /// <summary>Record a player's base (point-less) max health so scaling is relative to it.</summary>
    public void RegisterPlayer(string playerId, float baseMaxHealth)
    {
        _baseMaxHealth[playerId] = baseMaxHealth;
        if (!_points.ContainsKey(playerId))
            _points[playerId] = 0;
    }

    public int GetPoints(string playerId) =>
        _points.TryGetValue(playerId, out var p) ? p : 0;

    public void Add(string playerId, int amount)
    {
        if (amount == 0) return;
        int pts = GetPoints(playerId) + amount;
        if (pts < 0) pts = 0;
        _points[playerId] = pts;
        Recompute(playerId);
    }

    /// <summary>Zero out and return a player's points (used when they die and drop everything).</summary>
    public int Take(string playerId)
    {
        int pts = GetPoints(playerId);
        if (pts == 0) return 0;
        _points[playerId] = 0;
        Recompute(playerId);
        return pts;
    }

    public float GetDamageMultiplier(string playerId) =>
        PointScaling.DamageMultiplierFor(GetPoints(playerId));

    private void Recompute(string playerId)
    {
        float baseMax = _baseMaxHealth.TryGetValue(playerId, out var b) ? b : 100f;
        _health.SetMaxHealth(playerId, PointScaling.MaxHealthFor(baseMax, GetPoints(playerId)), healDelta: true);
    }
}
