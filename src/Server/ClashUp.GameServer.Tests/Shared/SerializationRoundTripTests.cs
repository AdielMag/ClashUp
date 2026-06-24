using ClashUp.Shared.Characters;
using ClashUp.Shared.MessagePackObjects;
using FluentAssertions;
using MessagePack;
using Xunit;

namespace ClashUp.Server.GameServer.Tests.Shared;

/// <summary>
/// Guards the wire contract: the MessagePack DTOs that cross client/server must survive a
/// serialize/deserialize round-trip with every keyed field intact.
/// </summary>
public class SerializationRoundTripTests
{
    private static T RoundTrip<T>(T value)
    {
        byte[] bytes = MessagePackSerializer.Serialize(value);
        return MessagePackSerializer.Deserialize<T>(bytes);
    }

    [Fact]
    public void InputCommand_RoundTrips()
    {
        var cmd = new InputCommand
        {
            Tick = 42,
            ClientSendStampMs = 1234567890,
            ButtonMask = InputCommand.AutoAimFlag | 0x3u,
            MoveX = 100,
            MoveY = -200,
            AimYawQ = 16000,
            AimDistanceQ = 32000,
            SequenceId = 7,
        };

        var rt = RoundTrip(cmd);
        rt.Should().BeEquivalentTo(cmd);
    }

    [Fact]
    public void PlayerStateDto_RoundTrips_AllFields()
    {
        var dto = new PlayerStateDto
        {
            Id = new PlayerId("player-1"),
            X = 1.5f,
            Z = -2.25f,
            Yaw = 90f,
            Health = 73f,
            LastProcessedInputSeq = 99,
            IsInvulnerable = true,
            RespawnInTicks = 30,
            Points = 12,
            IsEliminated = false,
            MaxHealth = 120f,
        };

        var rt = RoundTrip(dto);
        rt.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public void WorldStatePacket_WithBoxesAndOrbs_RoundTrips()
    {
        var packet = new WorldStatePacket
        {
            Players = new[] { new PlayerStateDto { Id = new PlayerId("a"), Health = 50f } },
            Boxes = new[] { new BoxStateDto { Id = 1, X = 3f, Z = 4f, Health = 10f, MaxHealth = 20f } },
            Orbs = new[] { new OrbStateDto { Id = 2, X = 5f, Z = 6f, Value = 3 } },
        };

        var rt = RoundTrip(packet);
        rt.Should().BeEquivalentTo(packet);
    }

    [Fact]
    public void PlayerId_RoundTrips_AndEquates()
    {
        var id = new PlayerId("xyz");
        var rt = RoundTrip(id);
        rt.Should().Be(id);
        rt.Value.Should().Be("xyz");
    }

    [Fact]
    public void CharactersConfig_Default_RoundTrips()
    {
        var rt = RoundTrip(CharactersConfig.Default);

        rt.DefaultCharacterId.Should().Be("brawler");
        rt.Characters.Should().HaveCount(2);
        rt.Characters[0].BaseStats.MaxHealth.Should().Be(100f);
        rt.Characters[1].Id.Value.Should().Be("mage");
        rt.Characters[1].AutoAttackId.Value.Should().Be("mage_bolt");
    }

    [Fact]
    public void AbilitiesConfig_Default_RoundTrips_WithCastShapes()
    {
        var rt = RoundTrip(AbilitiesConfig.Default);

        rt.Abilities.Should().NotBeEmpty();
        var punch = rt.Abilities.Single(a => a.Id.Value == "brawler_punch");
        punch.CastShape!.Shape.Should().Be(ClashUp.Shared.Abilities.TelegraphShape.Capsule);
        punch.CastShape.Length.Should().Be(3f);
        punch.AutoRange.Should().Be(4f);
    }
}
