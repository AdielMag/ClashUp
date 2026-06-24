using ClashUp.Server.GameServer.Simulation;
using ClashUp.Server.Services.Matchmaking;
using ClashUp.Shared.Simulation;
using FluentAssertions;
using Xunit;

namespace ClashUp.Server.GameServer.Tests;

public class PointScalingTests
{
    [Fact]
    public void MaxHealth_GrowsLinearly_AndCaps()
    {
        PointScaling.MaxHealthFor(100f, 0).Should().Be(100f);
        PointScaling.MaxHealthFor(100f, 10).Should().Be(100f + 10 * PointScaling.HealthPerPoint);
        // Far above the cap → base + MaxHealthBonus.
        PointScaling.MaxHealthFor(100f, 100000).Should().Be(100f + PointScaling.MaxHealthBonus);
    }

    [Fact]
    public void DamageMultiplier_GrowsLinearly_AndCaps()
    {
        PointScaling.DamageMultiplierFor(0).Should().Be(1f);
        PointScaling.DamageMultiplierFor(10).Should().BeApproximately(1f + 10 * PointScaling.DamagePerPoint, 1e-4f);
        PointScaling.DamageMultiplierFor(100000).Should().Be(PointScaling.MaxDamageMultiplier);
    }
}

public class PlayerProgressionTests
{
    [Fact]
    public void AddPoints_RaisesMaxHealth_AndDamage()
    {
        var health = new HealthTable();
        health.Initialize("p", 100f);
        var prog = new PlayerProgression(health);
        prog.RegisterPlayer("p", 100f);

        prog.Add("p", 10);

        prog.GetPoints("p").Should().Be(10);
        health.GetMaxHealth("p").Should().Be(PointScaling.MaxHealthFor(100f, 10));
        prog.GetDamageMultiplier("p").Should().BeApproximately(PointScaling.DamageMultiplierFor(10), 1e-4f);
    }

    [Fact]
    public void Take_ZeroesPoints_AndReturnsThem()
    {
        var health = new HealthTable();
        health.Initialize("p", 100f);
        var prog = new PlayerProgression(health);
        prog.RegisterPlayer("p", 100f);
        prog.Add("p", 25);

        int dropped = prog.Take("p");

        dropped.Should().Be(25);
        prog.GetPoints("p").Should().Be(0);
        health.GetMaxHealth("p").Should().Be(100f); // back to base
    }
}

public class MatchmakingQueueModeTests
{
    [Fact]
    public void TryDrain_OnlyBatchesMatchingMode_AndPreservesOthers()
    {
        var queue = new MatchmakingQueue();
        queue.Enqueue("elim1", "elimination");
        queue.Enqueue("default1", "default");
        queue.Enqueue("elim2", "elimination");

        var batch = queue.TryDrain(2, "elimination");

        batch.Should().NotBeNull();
        batch!.Select(t => t.PlayerId).Should().BeEquivalentTo(new[] { "elim1", "elim2" });

        // The default ticket was preserved (not consumed by an elimination drain).
        queue.GetQueuedModeIds().Should().BeEquivalentTo(new[] { "default" });
    }

    [Fact]
    public void TryDrain_ReturnsNull_WhenNotEnoughOfMode()
    {
        var queue = new MatchmakingQueue();
        queue.Enqueue("default1", "default");

        queue.TryDrain(2, "default").Should().BeNull();
        // Re-enqueued — still discoverable.
        queue.GetQueuedModeIds().Should().BeEquivalentTo(new[] { "default" });
    }
}
