using ClashUp.Server.GameServer.Simulation;
using ClashUp.Shared.Abilities;
using ClashUp.Shared.MessagePackObjects;
using ClashUp.Shared.Simulation;
using FluentAssertions;
using Xunit;

namespace ClashUp.Server.GameServer.Tests.GameServer;

/// <summary>
/// Integration coverage for the ability pipeline over a real AetherNet physics world: slot setup,
/// auto vs manual triggering, cooldowns, self-immunity, and shaped (cone) hitbox containment.
/// Player ids are deliberately >= 6 chars to match real (GUID-length) ids the executor logs.
/// </summary>
public class AbilityExecutorTests
{
    private const double Dt = 1.0 / 30.0;

    private static AbilityDefinition CircleHit(string id, TriggerMode trigger, float amount,
                                               float radius, float autoRange, int cooldown = 30) => new()
    {
        Id = new AbilityId(id),
        TriggerMode = trigger,
        CastMode = CastMode.Instant,
        AutoRange = autoRange,
        CooldownTicks = cooldown,
        RootNode = new AbilityNode
        {
            Type = AbilityNodeType.Hitbox,
            Hitbox = new HitboxConfig
            {
                Effect = HitboxEffect.Damage,
                Amount = amount,
                Shape = HitboxShape.Circle,
                Radius = radius,
                DurationTicks = 1,
            },
        },
    };

    private static AbilityExecutor Executor(params AbilityDefinition[] defs)
    {
        var ex = new AbilityExecutor();
        ex.RegisterAbilities(defs);
        return ex;
    }

    [Fact]
    public void InitPlayer_RegistersSlots_OnlyForKnownAbilities()
    {
        var punch = CircleHit("punch", TriggerMode.Manual, 10f, 2f, 0f);
        var ex = Executor(punch);
        using var world = new MatchPhysicsWorld();
        var health = new HealthTable();
        world.EnsurePlayer("caster", 0f, 0f);
        health.Initialize("caster", 100f);

        // active ability is unknown → only the known auto-attack becomes a usable slot.
        ex.InitPlayer("caster", new AbilityId("punch"), new AbilityId("does-not-exist"));

        // Triggering slot 0 (punch) hits; there is no slot 1 to trigger.
        world.EnsurePlayer("target", 1f, 0f);
        health.Initialize("target", 100f);
        ex.ProcessInput("caster", buttonMask: 1u, aimYaw: 0f, aimMagnitude: 0f, world, health, currentTick: 1);
        ex.Tick(world, health, new ProjectileSimulation(), null, null, Dt, 1);

        health.GetHealth("target").Should().BeLessThan(100f);
    }

    [Fact]
    public void AutoAttack_HitsNearestEnemyInRange()
    {
        var auto = CircleHit("auto", TriggerMode.Auto, 10f, radius: 2f, autoRange: 4f);
        var ex = Executor(auto);
        using var world = new MatchPhysicsWorld();
        var health = new HealthTable();

        world.EnsurePlayer("caster", 0f, 0f);
        world.EnsurePlayer("target", 1f, 0f);
        health.Initialize("caster", 100f);
        health.Initialize("target", 100f);
        ex.InitPlayer("caster", new AbilityId("auto"), default);

        ex.Tick(world, health, new ProjectileSimulation(), null, null, Dt, 1);

        health.GetHealth("target").Should().Be(90f, "the auto-attack fires and lands in the same tick");
        health.GetHealth("caster").Should().Be(100f, "the caster is never hit by its own attack");
    }

    [Fact]
    public void AutoAttack_DoesNotFire_WhenNoEnemyInRange()
    {
        var auto = CircleHit("auto", TriggerMode.Auto, 10f, radius: 2f, autoRange: 4f);
        var ex = Executor(auto);
        using var world = new MatchPhysicsWorld();
        var health = new HealthTable();

        world.EnsurePlayer("caster", 0f, 0f);
        world.EnsurePlayer("target", 50f, 0f); // way out of range
        health.Initialize("caster", 100f);
        health.Initialize("target", 100f);
        ex.InitPlayer("caster", new AbilityId("auto"), default);

        ex.Tick(world, health, new ProjectileSimulation(), null, null, Dt, 1);

        health.GetHealth("target").Should().Be(100f);
    }

    [Fact]
    public void ManualAbility_RespectsCooldown()
    {
        var punch = CircleHit("punch", TriggerMode.Manual, 25f, radius: 2f, autoRange: 0f, cooldown: 30);
        var ex = Executor(punch);
        using var world = new MatchPhysicsWorld();
        var health = new HealthTable();

        world.EnsurePlayer("caster", 0f, 0f);
        world.EnsurePlayer("target", 1f, 0f);
        health.Initialize("caster", 100f);
        health.Initialize("target", 100f);
        ex.InitPlayer("caster", default, new AbilityId("punch"));

        // First cast lands.
        ex.ProcessInput("caster", 1u, 0f, 0f, world, health, 1);
        ex.Tick(world, health, new ProjectileSimulation(), null, null, Dt, 1);
        health.GetHealth("target").Should().Be(75f);

        // Immediate re-press is on cooldown → no further damage.
        ex.ProcessInput("caster", 1u, 0f, 0f, world, health, 2);
        ex.Tick(world, health, new ProjectileSimulation(), null, null, Dt, 2);
        health.GetHealth("target").Should().Be(75f, "the ability is still cooling down");
    }

    [Fact]
    public void DeadCaster_CannotCast()
    {
        var punch = CircleHit("punch", TriggerMode.Manual, 10f, 2f, 0f);
        var ex = Executor(punch);
        using var world = new MatchPhysicsWorld();
        var health = new HealthTable();

        world.EnsurePlayer("caster", 0f, 0f);
        world.EnsurePlayer("target", 1f, 0f);
        health.Initialize("caster", 0f); // dead
        health.Initialize("target", 100f);
        ex.InitPlayer("caster", default, new AbilityId("punch"));

        ex.ProcessInput("caster", 1u, 0f, 0f, world, health, 1);
        ex.Tick(world, health, new ProjectileSimulation(), null, null, Dt, 1);

        health.GetHealth("target").Should().Be(100f);
    }

    [Fact]
    public void Cast_EmitsAbilityCastEvent()
    {
        var punch = CircleHit("punch", TriggerMode.Manual, 10f, 2f, 0f);
        var ex = Executor(punch);
        using var world = new MatchPhysicsWorld();
        var health = new HealthTable();
        world.EnsurePlayer("caster", 0f, 0f);
        health.Initialize("caster", 100f);
        ex.InitPlayer("caster", default, new AbilityId("punch"));

        ex.ProcessInput("caster", 1u, 0f, 0f, world, health, 7);

        ex.DrainEvents().Should().ContainSingle(e => e.Kind == "ability_cast");
    }

    [Fact]
    public void ConeHitbox_HitsTargetsInArc_AndMissesThoseOutside()
    {
        var cone = new AbilityDefinition
        {
            Id = new AbilityId("cone"),
            TriggerMode = TriggerMode.Manual,
            CastMode = CastMode.Instant,
            CooldownTicks = 0,
            RootNode = new AbilityNode
            {
                Type = AbilityNodeType.Hitbox,
                Hitbox = new HitboxConfig
                {
                    Effect = HitboxEffect.Damage,
                    Amount = 20f,
                    Shape = HitboxShape.Cone,
                    Length = 4f,
                    Angle = 60f, // ±30° around aim
                    DurationTicks = 1,
                },
            },
        };
        var ex = Executor(cone);
        using var world = new MatchPhysicsWorld();
        var health = new HealthTable();

        world.EnsurePlayer("caster", 0f, 0f);
        world.EnsurePlayer("frontTgt", 0f, 2f); // straight ahead (+Z), aimYaw 0
        world.EnsurePlayer("sideTgt", 3f, 0f);  // 90° off-axis, well outside the 30° half-angle
        health.Initialize("caster", 100f);
        health.Initialize("frontTgt", 100f);
        health.Initialize("sideTgt", 100f);
        ex.InitPlayer("caster", default, new AbilityId("cone"));

        ex.ProcessInput("caster", 1u, aimYaw: 0f, aimMagnitude: 0f, world, health, 1);
        ex.Tick(world, health, new ProjectileSimulation(), null, null, Dt, 1);

        health.GetHealth("frontTgt").Should().Be(80f, "the target is inside the cone");
        health.GetHealth("sideTgt").Should().Be(100f, "the target is outside the cone arc");
    }
}
