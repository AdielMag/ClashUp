using ClashUp.Shared.Simulation;
using FluentAssertions;
using Xunit;

namespace ClashUp.Server.GameServer.Tests.Shared;

/// <summary>
/// Exercises the real AetherNet-backed shared physics world. Assertions check directional movement
/// and entity bookkeeping (not exact float positions) so they stay determinism-stable.
/// </summary>
public class MatchPhysicsWorldTests
{
    [Fact]
    public void DefaultPlayerRadius_MatchesPrefabContract()
    {
        using var world = new MatchPhysicsWorld();
        world.PlayerRadius.Should().Be(MatchPhysicsWorld.DefaultPlayerRadius);
        MatchPhysicsWorld.DefaultPlayerRadius.Should().Be(0.5f);
    }

    [Fact]
    public void EnsurePlayer_SpawnsAtGivenPosition_AndIsIdempotent()
    {
        using var world = new MatchPhysicsWorld();
        world.EnsurePlayer("p", spawnX: 4f, spawnZ: -2f);

        var (x, z, _) = world.GetPlayerState("p");
        x.Should().BeApproximately(4f, 1e-3f);
        z.Should().BeApproximately(-2f, 1e-3f);

        // A second EnsurePlayer must not re-spawn / move the body.
        world.EnsurePlayer("p", spawnX: 100f, spawnZ: 100f);
        var (x2, _, _) = world.GetPlayerState("p");
        x2.Should().BeApproximately(4f, 1e-3f, "the player was already created");
        world.PlayerIds.Should().ContainSingle();
    }

    [Fact]
    public void ApplyInput_ThenStep_MovesPlayerInInputDirection()
    {
        using var world = new MatchPhysicsWorld();
        world.EnsurePlayer("p", 0f, 0f);

        world.ApplyInput("p", 1f, 0f);
        world.Step(1.0 / 30.0);

        var (x, _, _) = world.GetPlayerState("p");
        x.Should().BeGreaterThan(0f, "+X input moves the player along +X");
    }

    [Fact]
    public void HigherMoveSpeed_TravelsFarther()
    {
        using var world = new MatchPhysicsWorld();
        world.EnsurePlayer("slow", 0f, 0f, moveSpeed: 2f);
        world.EnsurePlayer("fast", 0f, 0f, moveSpeed: 10f);

        world.ApplyInput("slow", 1f, 0f);
        world.ApplyInput("fast", 1f, 0f);
        for (int i = 0; i < 5; i++)
        {
            world.Step(1.0 / 30.0);
            world.ApplyInput("slow", 1f, 0f);
            world.ApplyInput("fast", 1f, 0f);
        }

        var slowX = world.GetPlayerState("slow").Item1;
        var fastX = world.GetPlayerState("fast").Item1;
        fastX.Should().BeGreaterThan(slowX);
    }

    [Fact]
    public void SnapPlayerPosition_Teleports()
    {
        using var world = new MatchPhysicsWorld();
        world.EnsurePlayer("p", 0f, 0f);
        world.SnapPlayerPosition("p", 12f, -7f);

        var (x, z, _) = world.GetPlayerState("p");
        x.Should().BeApproximately(12f, 1e-3f);
        z.Should().BeApproximately(-7f, 1e-3f);
    }

    [Fact]
    public void EntityIdMapping_RoundTrips()
    {
        using var world = new MatchPhysicsWorld();
        world.EnsurePlayer("p", 0f, 0f);

        int id = world.GetEntityIdForPlayer("p");
        id.Should().BeGreaterThanOrEqualTo(0);
        world.GetPlayerByEntityId(id).Should().Be("p");
        world.GetEntityIdForPlayer("ghost").Should().Be(-1);
        world.GetPlayerByEntityId(99999).Should().BeNull();
    }

    [Fact]
    public void SpawnBox_AndOrb_AreTaggedAndQueryable()
    {
        using var world = new MatchPhysicsWorld();
        int boxId = world.SpawnBox(0f, 0f, halfExtent: 0.5f);
        int orbId = world.SpawnOrb(0.1f, 0f, velX: 0f, velZ: 0f, radius: 0.2f);

        world.IsBoxEntity(boxId).Should().BeTrue();
        world.IsOrbEntity(orbId).Should().BeTrue();
        world.IsBoxEntity(orbId).Should().BeFalse();

        // The orb query mask finds the orb but not the box.
        var hits = new int[8];
        int count = world.OverlapCircle(0.1f, 0f, 0.5f, hits, MatchPhysicsWorld.OrbQueryMask);
        var found = new System.ArraySegment<int>(hits, 0, count);
        found.Should().Contain(orbId);
        found.Should().NotContain(boxId);
    }

    [Fact]
    public void DestroyEntity_RecyclesId_AndUntags()
    {
        using var world = new MatchPhysicsWorld();
        int boxId = world.SpawnBox(0f, 0f, 0.5f);
        world.DestroyEntity(boxId);

        world.IsBoxEntity(boxId).Should().BeFalse();

        // The freed id is recycled for the next spawned entity.
        int reused = world.SpawnBox(5f, 5f, 0.5f);
        reused.Should().Be(boxId);
    }
}
