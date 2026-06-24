using System;

namespace ClashUp.Shared.Simulation
{
    /// <summary>
    /// Single source of truth for how a player's POINTS translate into power. Placeholder formula —
    /// expected to be re-balanced later. Keep all tunables here so design iteration is one-file.
    ///
    /// Used server-side: max-health is pushed into <see cref="HealthTable"/> on every points change,
    /// and the damage multiplier is applied to outgoing ability/projectile damage.
    /// </summary>
    public static class PointScaling
    {
        /// <summary>Extra max-health granted per point held.</summary>
        public const float HealthPerPoint = 5f;

        /// <summary>Cap on bonus health from points (so a runaway leader isn't unkillable).</summary>
        public const float MaxHealthBonus = 200f;

        /// <summary>Extra outgoing-damage fraction per point held (0.02 = +2% per point).</summary>
        public const float DamagePerPoint = 0.02f;

        /// <summary>Cap on the damage multiplier (3 = at most 3x base damage).</summary>
        public const float MaxDamageMultiplier = 3f;

        public static float MaxHealthFor(float baseMaxHealth, int points)
        {
            float bonus = MathF.Min(MaxHealthBonus, MathF.Max(0, points) * HealthPerPoint);
            return baseMaxHealth + bonus;
        }

        public static float DamageMultiplierFor(int points)
        {
            float mult = 1f + MathF.Max(0, points) * DamagePerPoint;
            return MathF.Min(MaxDamageMultiplier, mult);
        }
    }
}
