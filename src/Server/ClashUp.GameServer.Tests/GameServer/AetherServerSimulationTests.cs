using ClashUp.Server.GameServer.Abilities;
using ClashUp.Server.GameServer.Maps;
using ClashUp.Server.GameServer.Match;
using ClashUp.Server.GameServer.Simulation;
using ClashUp.Shared.Characters;
using ClashUp.Shared.MessagePackObjects;
using FluentAssertions;
using MessagePack;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClashUp.Server.GameServer.Tests.GameServer;

/// <summary>
/// End-to-end integration over the whole authoritative simulation: spawn players on the real map,
/// drive input, step the fixed loop, and read back the encoded world snapshot.
/// </summary>
public class AetherServerSimulationTests
{
    private const double Dt = 1.0 / 30.0;

    private static AetherServerSimulation NewSim(string objective = "survival")
    {
        var abilityStore = new ServerAbilityStore(NullLogger<ServerAbilityStore>.Instance);
        var characters = new MatchCharactersHolder(); // baked-in default roster
        var sim = new AetherServerSimulation(abilityStore, characters);

        var map = new ServerMapStore(NullLogger<ServerMapStore>.Instance).GetMap("arena_tdm");
        if (map != null) sim.LoadMap(map);
        sim.Configure(objective);
        return sim;
    }

    private static WorldStatePacket Decode(AetherServerSimulation sim) =>
        MessagePackSerializer.Deserialize<WorldStatePacket>(sim.EncodeDelta(0));

    [Fact]
    public void EnsurePlayer_SpawnsPlayersIntoSnapshot()
    {
        var sim = NewSim();
        sim.EnsurePlayer(new PlayerId("a"), colorSlot: 0, teamId: 0, new CharacterId("brawler"));
        sim.EnsurePlayer(new PlayerId("b"), colorSlot: 1, teamId: 1, new CharacterId("mage"));

        var packet = Decode(sim);
        packet.Players.Select(p => p.Id.Value).Should().BeEquivalentTo(new[] { "a", "b" });
        packet.Players.Should().OnlyContain(p => p.Health > 0f);
        packet.Players.Should().OnlyContain(p => p.IsInvulnerable, "players spawn with invulnerability");
    }

    [Fact]
    public void Step_AdvancesCurrentTick()
    {
        var sim = NewSim();
        sim.CurrentTick.Should().Be(0);
        for (int i = 0; i < 5; i++) sim.Step(Dt);
        sim.CurrentTick.Should().Be(5);
    }

    [Fact]
    public void ApplyInput_MovesThePlayer()
    {
        var sim = NewSim();
        sim.EnsurePlayer(new PlayerId("a"), 0, 0, new CharacterId("brawler"));

        float startX = Decode(sim).Players.Single().X;

        // Push +X every tick (the world clears pending velocity each step).
        var move = new InputCommand { MoveX = ClashUp.Shared.Simulation.MovementModel.EncodeAxis(1f), SequenceId = 1 };
        for (int i = 0; i < 10; i++)
        {
            sim.ApplyInput(new PlayerId("a"), move);
            sim.Step(Dt);
        }

        float endX = Decode(sim).Players.Single().X;
        endX.Should().BeGreaterThan(startX);
    }

    [Fact]
    public void ApplyInput_RecordsLastProcessedSequence()
    {
        var sim = NewSim();
        sim.EnsurePlayer(new PlayerId("a"), 0, 0, new CharacterId("brawler"));

        sim.ApplyInput(new PlayerId("a"), new InputCommand { SequenceId = 77 });

        Decode(sim).Players.Single().LastProcessedInputSeq.Should().Be(77);
    }

    [Fact]
    public void Survival_DoesNotEliminate()
    {
        var sim = NewSim("survival");
        sim.EnsurePlayer(new PlayerId("a"), 0, 0, new CharacterId("brawler"));
        sim.Step(Dt);
        sim.IsEliminated("a").Should().BeFalse();
    }

    [Fact]
    public void GetTeamScores_IsNeverNull()
    {
        var sim = NewSim("elimination");
        sim.GetTeamScores().Should().NotBeNull();
    }
}
