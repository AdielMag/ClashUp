using ClashUp.Server.GameServer.Simulation;
using ClashUp.Shared.Simulation;
using FluentAssertions;
using Xunit;

namespace ClashUp.Server.GameServer.Tests.GameServer;

public class PointOrbSimulationTests
{
    [Fact]
    public void SpawnBurst_CreatesOrbs_SplittingTheValue()
    {
        using var world = new MatchPhysicsWorld();
        var orbs = new PointOrbSimulation(seed: 1);

        orbs.SpawnBurst(world, 0f, 0f, totalValue: 10, count: 5);

        var snap = orbs.Snapshot(world);
        snap.Should().HaveCount(5);
        snap.Sum(o => o.Value).Should().Be(10);
    }

    [Fact]
    public void DropFromPlayer_CapsOrbCount()
    {
        using var world = new MatchPhysicsWorld();
        var orbs = new PointOrbSimulation(seed: 1);

        // 100 points would be 100 orbs, but the drop is capped (MaxDropOrbs = 12).
        orbs.DropFromPlayer(world, 0f, 0f, totalPoints: 100);

        orbs.Snapshot(world).Should().HaveCount(12);
    }

    [Fact]
    public void ZeroValue_SpawnsNothing()
    {
        using var world = new MatchPhysicsWorld();
        var orbs = new PointOrbSimulation(seed: 1);
        orbs.SpawnBurst(world, 0f, 0f, totalValue: 0, count: 4);
        orbs.Snapshot(world).Should().BeEmpty();
    }

    [Fact]
    public void Orb_IsNotCollectible_BeforeItArms()
    {
        using var world = new MatchPhysicsWorld();
        var health = new HealthTable();
        var orbs = new PointOrbSimulation(seed: 1);
        var prog = new PlayerProgression(health);

        world.EnsurePlayer("a", 0f, 0f);
        health.Initialize("a", 100f);
        prog.RegisterPlayer("a", 100f);
        orbs.SpawnBurst(world, 0f, 0f, totalValue: 5, count: 1);

        // One tick: orb age is below the arming threshold, so even while overlapping it isn't picked up.
        SnapPlayerOntoOrb(world, orbs, "a");
        orbs.Tick(world, health, prog, currentTick: 1);

        prog.GetPoints("a").Should().Be(0);
        orbs.Snapshot(world).Should().HaveCount(1);
    }

    [Fact]
    public void Player_CollectsArmedOrb_GainingPoints_AndEmittingEvent()
    {
        using var world = new MatchPhysicsWorld();
        var health = new HealthTable();
        var orbs = new PointOrbSimulation(seed: 1);
        var prog = new PlayerProgression(health);

        world.EnsurePlayer("a", 0f, 0f);
        health.Initialize("a", 100f);
        prog.RegisterPlayer("a", 100f);
        orbs.SpawnBurst(world, 0f, 0f, totalValue: 5, count: 1);

        // Keep the player on top of the orb until it arms (~12 ticks) and is collected.
        for (int t = 1; t <= 15 && prog.GetPoints("a") == 0; t++)
        {
            SnapPlayerOntoOrb(world, orbs, "a");
            orbs.Tick(world, health, prog, currentTick: t);
        }

        prog.GetPoints("a").Should().Be(5);
        orbs.Snapshot(world).Should().BeEmpty("the orb was consumed on pickup");
        orbs.DrainEvents().Should().Contain(e => e.Kind == "points_collected");
    }

    private static void SnapPlayerOntoOrb(MatchPhysicsWorld world, PointOrbSimulation orbs, string playerId)
    {
        var snap = orbs.Snapshot(world);
        if (snap.Length > 0)
            world.SnapPlayerPosition(playerId, snap[0].X, snap[0].Z);
    }
}
