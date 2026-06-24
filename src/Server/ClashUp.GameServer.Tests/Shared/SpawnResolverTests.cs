using ClashUp.Shared.Maps;
using ClashUp.Shared.Simulation;
using FluentAssertions;
using Xunit;

namespace ClashUp.Server.GameServer.Tests.Shared;

public class SpawnResolverTests
{
    [Fact]
    public void NullMap_FallsBackToLinearLayout()
    {
        var (x, z) = SpawnResolver.GetSpawnPosition(null, teamId: 0, slotIndex: 2);
        x.Should().Be(2 * MovementModel.SpawnSpacing);
        z.Should().Be(0f);
    }

    [Fact]
    public void MatchingTeamArea_ReturnsAuthoredPosition()
    {
        var map = new MapData
        {
            SpawnAreas = new[]
            {
                new SpawnArea { TeamIndex = 0, PositionsX = new[] { 1f, 2f }, PositionsZ = new[] { 5f, 6f } },
                new SpawnArea { TeamIndex = 1, PositionsX = new[] { -1f }, PositionsZ = new[] { -5f } },
            },
        };

        SpawnResolver.GetSpawnPosition(map, teamId: 1, slotIndex: 0).Should().Be((-1f, -5f));
        SpawnResolver.GetSpawnPosition(map, teamId: 0, slotIndex: 1).Should().Be((2f, 6f));
    }

    [Fact]
    public void SlotIndex_WrapsModuloAvailablePositions()
    {
        var map = new MapData
        {
            SpawnAreas = new[]
            {
                new SpawnArea { TeamIndex = 0, PositionsX = new[] { 1f, 2f }, PositionsZ = new[] { 5f, 6f } },
            },
        };

        // slot 2 wraps back to index 0.
        SpawnResolver.GetSpawnPosition(map, teamId: 0, slotIndex: 2).Should().Be((1f, 5f));
        SpawnResolver.GetSpawnPosition(map, teamId: 0, slotIndex: 3).Should().Be((2f, 6f));
    }

    [Fact]
    public void UnknownTeam_FallsBackToLinearLayout()
    {
        var map = new MapData
        {
            SpawnAreas = new[]
            {
                new SpawnArea { TeamIndex = 0, PositionsX = new[] { 1f }, PositionsZ = new[] { 5f } },
            },
        };

        var (x, z) = SpawnResolver.GetSpawnPosition(map, teamId: 9, slotIndex: 1);
        x.Should().Be(1 * MovementModel.SpawnSpacing);
        z.Should().Be(0f);
    }

    [Fact]
    public void EmptyPositions_FallsBackToLinearLayout()
    {
        var map = new MapData
        {
            SpawnAreas = new[]
            {
                new SpawnArea { TeamIndex = 0, PositionsX = System.Array.Empty<float>(), PositionsZ = System.Array.Empty<float>() },
            },
        };

        var (x, z) = SpawnResolver.GetSpawnPosition(map, teamId: 0, slotIndex: 1);
        x.Should().Be(1 * MovementModel.SpawnSpacing);
        z.Should().Be(0f);
    }
}
