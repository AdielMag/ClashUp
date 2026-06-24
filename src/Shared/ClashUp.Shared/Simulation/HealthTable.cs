using System;
using System.Collections.Generic;

namespace ClashUp.Shared.Simulation
{
    public sealed class HealthTable
    {
        // 3 seconds at 30 Hz tick rate
        public const int DefaultSpawnInvulnTicks = 90;

        private readonly Dictionary<string, float> _health = new(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _maxHealth = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _invulnTicks = new(StringComparer.Ordinal);

        public void Initialize(string playerId, float maxHealth)
        {
            _health[playerId] = maxHealth;
            _maxHealth[playerId] = maxHealth;
        }

        public float GetHealth(string playerId)
        {
            return _health.TryGetValue(playerId, out var h) ? h : 0f;
        }

        public float GetMaxHealth(string playerId)
        {
            return _maxHealth.TryGetValue(playerId, out var m) ? m : 0f;
        }

        /// <summary>
        /// Raise/lower a player's max health (e.g. as points scale it). When <paramref name="healDelta"/>
        /// is true an increase also heals the player by the gained amount (feels good on pickup);
        /// current health is always clamped to the new max.
        /// </summary>
        public void SetMaxHealth(string playerId, float newMax, bool healDelta)
        {
            float oldMax = GetMaxHealth(playerId);
            _maxHealth[playerId] = newMax;
            if (!_health.TryGetValue(playerId, out var h)) return;
            if (healDelta && newMax > oldMax)
                h += newMax - oldMax;
            _health[playerId] = MathF.Min(newMax, h);
        }

        public bool IsAlive(string playerId)
        {
            return GetHealth(playerId) > 0f;
        }

        public void SetInvulnerable(string playerId, int durationTicks)
        {
            _invulnTicks[playerId] = durationTicks;
        }

        public bool IsInvulnerable(string playerId)
        {
            return _invulnTicks.TryGetValue(playerId, out var ticks) && ticks > 0;
        }

        public void Tick()
        {
            var toRemove = new List<string>();
            foreach (var key in _invulnTicks.Keys)
            {
                var remaining = _invulnTicks[key] - 1;
                if (remaining <= 0)
                    toRemove.Add(key);
                else
                    _invulnTicks[key] = remaining;
            }
            foreach (var key in toRemove)
                _invulnTicks.Remove(key);
        }

        public float ApplyDamage(string playerId, float amount)
        {
            if (!_health.TryGetValue(playerId, out var h)) return 0f;
            if (IsInvulnerable(playerId)) return h;
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
