using ClashUp.Server.GameServer.Match;
using ClashUp.Server.GameServer.Simulation;
using ClashUp.Shared.Characters;
using ClashUp.Shared.MessagePackObjects;
using ClashUp.Shared.Simulation;
using FluentAssertions;
using MessagePack;
using Xunit;

namespace ClashUp.Server.GameServer.Tests.GameServer;

/// <summary>
/// Covers the lightweight (physics-free) movement-only server simulation: spacing, input-driven
/// movement, health seeding from the character roster, and snapshot encoding.
/// </summary>
public class MovementServerSimulationTests
{
    private static MovementServerSimulation NewSim() => new(new MatchCharactersHolder());

    private static WorldStatePacket Decode(MovementServerSimulation sim) =>
        MessagePackSerializer.Deserialize<WorldStatePacket>(sim.EncodeDelta(0));

    [Fact]
    public void EnsurePlayer_SpacesPlayersByColorSlot_AndSeedsHealth()
    {
        var sim = NewSim();
        sim.EnsurePlayer(new PlayerId("a"), colorSlot: 0, teamId: 0, new CharacterId("brawler"));
        sim.EnsurePlayer(new PlayerId("b"), colorSlot: 1, teamId: 0, new CharacterId("brawler"));

        var players = Decode(sim).Players.OrderBy(p => p.X).ToArray();
        players.Should().HaveCount(2);
        players[0].X.Should().Be(0f);
        players[1].X.Should().Be(3f, "slot 1 is spaced 3 units over");
        players[0].Health.Should().Be(100f, "brawler base max health");
    }

    [Fact]
    public void EnsurePlayer_IsIdempotent()
    {
        var sim = NewSim();
        sim.EnsurePlayer(new PlayerId("a"), 0, 0, new CharacterId("brawler"));
        sim.EnsurePlayer(new PlayerId("a"), 5, 0, new CharacterId("brawler"));
        Decode(sim).Players.Should().ContainSingle();
    }

    [Fact]
    public void ApplyInput_ThenStep_MovesPlayer_AndAdvancesTick()
    {
        var sim = NewSim();
        sim.EnsurePlayer(new PlayerId("a"), 0, 0, new CharacterId("brawler"));

        sim.ApplyInput(new PlayerId("a"), new InputCommand { MoveX = MovementModel.EncodeAxis(1f) });
        sim.Step(1.0 / 30.0);

        Decode(sim).Players.Single().X.Should().BeGreaterThan(0f);
        sim.CurrentTick.Should().Be(1);
    }

    [Fact]
    public void InertSurfaceArea_BehavesAsExpected()
    {
        var sim = NewSim();
        sim.IsEliminated("anyone").Should().BeFalse();
        sim.GetTeamScores().Should().BeEmpty();
        sim.DrainAbilityEvents().Should().BeEmpty();
        sim.TryGetBotView("bot", out _).Should().BeFalse();
        sim.RandomSeed.Should().BeGreaterThan(0u);
    }
}
