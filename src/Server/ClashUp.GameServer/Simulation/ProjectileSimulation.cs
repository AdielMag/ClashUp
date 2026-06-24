using System.Text.Json;
using ClashUp.Shared.Abilities;
using ClashUp.Shared.MessagePackObjects;
using ClashUp.Shared.Simulation;

namespace ClashUp.Server.GameServer.Simulation;

/// <summary>
/// Server-authoritative projectile system. The AbilityExecutor spawns projectiles from Projectile
/// nodes; this class advances them each tick, resolves collisions, applies single-target and AoE
/// damage, and emits <c>projectile_spawn</c> / <c>projectile_destroy</c> events plus reuses
/// <c>ability_hit</c> for victims (so the existing client hit-VFX path works unchanged).
/// </summary>
public sealed class ProjectileSimulation
{
    private sealed class ActiveProjectile
    {
        public int Id;
        public string CasterId = string.Empty;
        public string AbilityId = string.Empty;
        public float X, Z;
        public float DirX, DirZ;
        public float StepPerTick;   // distance advanced each fixed tick
        public float Radius;
        public float MaxRange;
        public float Traveled;
        public int RemainingLifetimeTicks;
        public HitboxEffect OnHitEffect;
        public float OnHitAmount;
        public float AoeRadius;
        public float AoeAmount;
        public HitboxEffect AoeEffect;
    }

    private readonly List<ActiveProjectile> _projectiles = new();
    private readonly List<MatchEvent> _pendingEvents = new();
    private readonly int[] _overlapScratch = new int[64];
    private int _nextProjectileId = 1;

    public int Spawn(string casterId, string abilityId, float originX, float originZ,
                     float aimYaw, ProjectileConfig cfg, double dt, int currentTick,
                     bool detonateAtOrigin = false)
    {
        float yawRad = aimYaw * (MathF.PI / 180f);
        float dirX = MathF.Sin(yawRad);
        float dirZ = MathF.Cos(yawRad);

        // detonateAtOrigin (TargetPoint casts): the projectile appears at the point and explodes
        // there on the next tick — no travel. Everything else flies until first hit / MaxRange.
        float stepPerTick = detonateAtOrigin ? 0f : cfg.Speed * (float)dt;
        float maxRange = detonateAtOrigin ? 0f : cfg.MaxRange;
        float wireSpeed = detonateAtOrigin ? 0f : cfg.Speed;

        int lifetime = detonateAtOrigin
            ? 1
            : (cfg.LifetimeTicks > 0
                ? cfg.LifetimeTicks
                : (stepPerTick > 1e-5f && cfg.MaxRange > 0f ? (int)MathF.Ceiling(cfg.MaxRange / stepPerTick) : 1));

        int id = _nextProjectileId++;
        _projectiles.Add(new ActiveProjectile
        {
            Id = id,
            CasterId = casterId,
            AbilityId = abilityId,
            X = originX,
            Z = originZ,
            DirX = dirX,
            DirZ = dirZ,
            StepPerTick = stepPerTick,
            Radius = cfg.Radius,
            MaxRange = maxRange,
            Traveled = 0f,
            RemainingLifetimeTicks = lifetime,
            OnHitEffect = cfg.OnHitEffect,
            OnHitAmount = cfg.OnHitAmount,
            AoeRadius = cfg.AoeRadius,
            AoeAmount = cfg.AoeAmount,
            AoeEffect = cfg.AoeEffect,
        });

        Console.WriteLine($"[PROJ] spawn #{id} {casterId[..6]} {abilityId} spd={wireSpeed} range={maxRange} aoe={cfg.AoeRadius}{(detonateAtOrigin ? " @target" : "")}");

        _pendingEvents.Add(new MatchEvent
        {
            Tick = currentTick,
            Kind = "projectile_spawn",
            Payload = JsonSerializer.Serialize(new
            {
                id,
                abilityId,
                x = originX,
                z = originZ,
                yaw = aimYaw,
                speed = wireSpeed,
                maxRange,
                aoeRadius = cfg.AoeRadius,
            }),
        });

        return id;
    }

    public void Tick(MatchPhysicsWorld world, HealthTable health, BoxSimulation? boxes,
                     PlayerProgression? progression, int currentTick)
    {
        for (int i = _projectiles.Count - 1; i >= 0; i--)
        {
            var p = _projectiles[i];

            // Advance one fixed step.
            p.X += p.DirX * p.StepPerTick;
            p.Z += p.DirZ * p.StepPerTick;
            p.Traveled += p.StepPerTick;
            p.RemainingLifetimeTicks--;

            // Broad-phase: nearest valid enemy (and nearest breakable box) within the collision radius.
            string? directHit = null;
            float bestDistSq = float.MaxValue;
            int boxHit = -1;
            float bestBoxDistSq = float.MaxValue;
            int hitCount = world.OverlapCircle(p.X, p.Z, p.Radius, _overlapScratch);
            for (int h = 0; h < hitCount; h++)
            {
                int eid = _overlapScratch[h];
                string? targetId = world.GetPlayerByEntityId(eid);
                if (targetId != null)
                {
                    if (targetId == p.CasterId) continue;
                    if (!health.IsAlive(targetId)) continue;

                    var (tx, tz, _) = world.GetPlayerState(targetId);
                    float dx = tx - p.X;
                    float dz = tz - p.Z;
                    float distSq = dx * dx + dz * dz;
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        directHit = targetId;
                    }
                }
                else if (boxes != null && world.IsBoxEntity(eid))
                {
                    var (bx, bz) = world.GetEntityPosition(eid);
                    float dx = bx - p.X;
                    float dz = bz - p.Z;
                    float distSq = dx * dx + dz * dz;
                    if (distSq < bestBoxDistSq)
                    {
                        bestBoxDistSq = distSq;
                        boxHit = eid;
                    }
                }
            }

            bool expired = p.Traveled >= p.MaxRange || p.RemainingLifetimeTicks <= 0;

            if (directHit != null || boxHit != -1 || expired)
            {
                Detonate(p, directHit, boxHit, boxes, progression, world, health, currentTick);
                _projectiles.RemoveAt(i);
            }
        }
    }

    private void Detonate(ActiveProjectile p, string? directHit, int boxHit, BoxSimulation? boxes,
                          PlayerProgression? progression, MatchPhysicsWorld world,
                          HealthTable health, int currentTick)
    {
        float mult = progression?.GetDamageMultiplier(p.CasterId) ?? 1f;

        // Direct single-target hit (player or box).
        if (directHit != null)
            ApplyAndEmit(p, directHit, p.OnHitEffect, ScaleDamage(p.OnHitEffect, p.OnHitAmount, mult), health, currentTick);
        else if (boxHit != -1 && boxes != null)
            boxes.ApplyDamage(world, boxHit, p.OnHitAmount * mult, p.CasterId, currentTick);

        // AoE explosion at the impact point (exclude the caster and the already-hit direct target).
        if (p.AoeRadius > 0f)
        {
            int hitCount = world.OverlapCircle(p.X, p.Z, p.AoeRadius, _overlapScratch);
            for (int h = 0; h < hitCount; h++)
            {
                int eid = _overlapScratch[h];
                string? targetId = world.GetPlayerByEntityId(eid);
                if (targetId != null)
                {
                    if (targetId == p.CasterId || targetId == directHit) continue;
                    if (!health.IsAlive(targetId)) continue;
                    ApplyAndEmit(p, targetId, p.AoeEffect, ScaleDamage(p.AoeEffect, p.AoeAmount, mult), health, currentTick);
                }
                else if (boxes != null && eid != boxHit && world.IsBoxEntity(eid))
                {
                    boxes.ApplyDamage(world, eid, p.AoeAmount * mult, p.CasterId, currentTick);
                }
            }
        }

        Console.WriteLine($"[PROJ] detonate #{p.Id} at ({p.X:0.0},{p.Z:0.0}) direct={(directHit != null ? directHit[..6] : "none")} aoe={p.AoeRadius}");

        _pendingEvents.Add(new MatchEvent
        {
            Tick = currentTick,
            Kind = "projectile_destroy",
            Payload = JsonSerializer.Serialize(new
            {
                id = p.Id,
                x = p.X,
                z = p.Z,
                aoeRadius = p.AoeRadius,
                reason = directHit != null || boxHit != -1 ? "hit" : "expire",
            }),
        });
    }

    private static float ScaleDamage(HitboxEffect effect, float amount, float multiplier) =>
        effect == HitboxEffect.Damage ? amount * multiplier : amount;

    private void ApplyAndEmit(ActiveProjectile p, string targetId, HitboxEffect effect,
                              float amount, HealthTable health, int currentTick)
    {
        string effectStr;
        if (effect == HitboxEffect.Damage)
        {
            float newHealth = health.ApplyDamage(targetId, amount);
            effectStr = "Damage";
            Console.WriteLine($"[HIT] {p.CasterId[..6]}→{targetId[..6]} {p.AbilityId} -{amount} hp={newHealth}");
        }
        else
        {
            health.ApplyHeal(targetId, amount, 100f);
            effectStr = "Heal";
        }

        _pendingEvents.Add(new MatchEvent
        {
            Tick = currentTick,
            Kind = "ability_hit",
            Payload = JsonSerializer.Serialize(new { source = p.CasterId, target = targetId, abilityId = p.AbilityId, effect = effectStr, amount }),
        });
    }

    public IReadOnlyList<MatchEvent> DrainEvents()
    {
        if (_pendingEvents.Count == 0) return Array.Empty<MatchEvent>();
        var events = new List<MatchEvent>(_pendingEvents);
        _pendingEvents.Clear();
        return events;
    }
}
