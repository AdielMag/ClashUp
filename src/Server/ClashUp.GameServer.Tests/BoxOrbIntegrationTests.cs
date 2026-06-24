using ClashUp.Server.GameServer.Simulation;
using ClashUp.Shared.Simulation;
using FluentAssertions;
using Xunit;

namespace ClashUp.Server.GameServer.Tests;

/// <summary>
/// Exercises the real AetherNet-backed box/orb pipeline. Asserts deterministic counts (not physics
/// positions) so it stays stable: a box spawns, breaking it removes it and drops point orbs, and a
/// box_broken event is emitted.
/// </summary>
public class BoxOrbIntegrationTests
{
    [Fact]
    public void BreakingBox_RemovesIt_DropsOrbs_AndEmitsEvent()
    {
        using var world = new MatchPhysicsWorld();
        var orbs = new PointOrbSimulation(seed: 12345);
        var boxes = new BoxSimulation(orbs);

        boxes.Initialize(world, new[] { new ClashUp.Shared.Maps.BoxSpawnDef { X = 0f, Z = 0f } });

        var snapshot = boxes.Snapshot();
        snapshot.Should().HaveCount(1, "one box was authored");
        orbs.Snapshot(world).Should().BeEmpty("no boxes have been broken yet");

        int boxEntityId = snapshot[0].Id;
        boxes.ApplyDamage(world, boxEntityId, amount: 9999f, breakerId: "killer", currentTick: 1);

        boxes.Snapshot().Should().BeEmpty("the box was destroyed and is now respawning");
        orbs.Snapshot(world).Should().NotBeEmpty("breaking a box drops point orbs");

        boxes.DrainEvents().Should().Contain(e => e.Kind == "box_broken");
    }
}
