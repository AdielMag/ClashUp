using System;
using System.Collections.Generic;

namespace ClashUp.Shared.Simulation
{
    public sealed class HealthTable
    {
        private readonly Dictionary<string, float> _health = new(StringComparer.Ordinal);

        public void Initialize(string playerId, float maxHealth)
        {
            _health[playerId] = maxHealth;
        }

        public float GetHealth(string playerId)
        {
            return _health.TryGetValue(playerId, out var h) ? h : 0f;
        }

        public bool IsAlive(string playerId)
        {
            return GetHealth(playerId) > 0f;
        }

        public float ApplyDamage(string playerId, float amount)
        {
            if (!_health.TryGetValue(playerId, out var h)) return 0f;
            h = MathF.Max(0f, h - MathF.Abs(amount));
            _health[playerId] = h;
            return h;
        }

        public float ApplyHeal(string playerId, float amount, float maxHealth)
        {
            if (!_health.TryGetValue(playerId, out var h)) return 0f;
            h = MathF.Min(maxHealth, h + MathF.Abs(amount));
            _health[playerId] = h;
            return h;
        }

        public void SnapHealth(string playerId, float health)
        {
            _health[playerId] = health;
        }
    }
}
