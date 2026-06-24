using ClashUp.Server.GameServer.Simulation;
using ClashUp.Shared.Abilities;
using ClashUp.Shared.Simulation;
using FluentAssertions;
using Xunit;

namespace ClashUp.Server.GameServer.Tests.GameServer;

// Player ids are >= 6 chars to match real (GUID-length) ids the projectile sim logs.
public class ProjectileSimulationTests
{
    private const double Dt = 1.0 / 30.0;

    [Fact]
    public void Spawn_EmitsProjectileSpawnEvent()
    {
        using var world = new MatchPhysicsWorld();
        var sim = new ProjectileSimulation();
        var cfg = new ProjectileConfig { Speed = 30f, Radius = 0.5f, MaxRange = 10f, OnHitEffect = HitboxEffect.Damage, OnHitAmount = 8f };

        sim.Spawn("caster", "bolt", 0f, 0f, aimYaw: 90f, cfg, Dt, currentTick: 1);

        sim.DrainEvents().Should().ContainSingle(e => e.Kind == "projectile_spawn");
    }

    [Fact]
    public void Projectile_TravelsAndHitsEnemy_DealingDamage()
    {
        using var world = new MatchPhysicsWorld();
        var health = new HealthTable();
        var sim = new ProjectileSimulation();

        world.EnsurePlayer("caster", 0f, 0f);
        world.EnsurePlayer("victim", 3f, 0f);
        health.Initialize("caster", 100f);
        health.Initialize("victim", 100f);

        // aimYaw 90° → direction +X, toward the victim. Speed 30 * (1/30) = 1 unit/tick.
        var cfg = new ProjectileConfig { Speed = 30f, Radius = 0.5f, MaxRange = 10f, OnHitEffect = HitboxEffect.Damage, OnHitAmount = 8f };
        sim.Spawn("caster", "bolt", 0f, 0f, aimYaw: 90f, cfg, Dt, 1);

        for (int t = 0; t < 6 && health.GetHealth("victim") == 100f; t++)
            sim.Tick(world, health, null, null, t);

        health.GetHealth("victim").Should().Be(92f);
        var events = sim.DrainEvents();
        events.Should().Contain(e => e.Kind == "ability_hit");
        events.Should().Contain(e => e.Kind == "projectile_destroy");
    }

    [Fact]
    public void Projectile_ThatHitsNothing_ExpiresAtMaxRange()
    {
        using var world = new MatchPhysicsWorld();
        var health = new HealthTable();
        var sim = new ProjectileSimulation();

        world.EnsurePlayer("caster", 0f, 0f); // only the caster; nothing to hit
        health.Initialize("caster", 100f);

        var cfg = new ProjectileConfig { Speed = 30f, Radius = 0.3f, MaxRange = 2f, OnHitEffect = HitboxEffect.Damage, OnHitAmount = 8f };
        sim.Spawn("caster", "bolt", 0f, 0f, aimYaw: 90f, cfg, Dt, 1);
        sim.DrainEvents(); // clear spawn

        for (int t = 0; t < 5; t++)
            sim.Tick(world, health, null, null, t);

        var destroy = sim.DrainEvents().Should().ContainSingle(e => e.Kind == "projectile_destroy").Subject;
        destroy.Payload.Should().Contain("expire");
        health.GetHealth("caster").Should().Be(100f, "the caster is immune to its own projectile");
    }

    [Fact]
    public void DetonateAtOrigin_ExplodesImmediately_ApplyingDirectAndAoeDamage()
    {
        using var world = new MatchPhysicsWorld();
        var health = new HealthTable();
        var sim = new ProjectileSimulation();

        world.EnsurePlayer("caster", 0f, 0f);     // caster, far away
        world.EnsurePlayer("direct", 5f, 0f);     // direct hit at the target point
        world.EnsurePlayer("splash", 6.5f, 0f);   // within AoE radius (2) of the impact, not a direct hit
        health.Initialize("caster", 100f);
        health.Initialize("direct", 100f);
        health.Initialize("splash", 100f);

        var cfg = new ProjectileConfig
        {
            Speed = 11f, Radius = 0.3f, MaxRange = 10f,
            OnHitEffect = HitboxEffect.Damage, OnHitAmount = 12f,
            AoeRadius = 2f, AoeAmount = 18f, AoeEffect = HitboxEffect.Damage,
        };
        sim.Spawn("caster", "blast", originX: 5f, originZ: 0f, aimYaw: 0f, cfg, Dt, 1, detonateAtOrigin: true);
        sim.Tick(world, health, null, null, 2);

        health.GetHealth("direct").Should().Be(88f, "direct hit for 12");
        health.GetHealth("splash").Should().Be(82f, "AoE splash for 18");
        health.GetHealth("caster").Should().Be(100f, "the caster is excluded from its own blast");
    }
}
