using ClashUp.Server.GameServer.Abilities;
using ClashUp.Server.GameServer.Maps;
using ClashUp.Shared.Abilities;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClashUp.Server.GameServer.Tests.GameServer;

/// <summary>
/// Exercises the disk-backed stores against the authored data (mirrored into the test output by the
/// .csproj). Covers loading + lookup, and the client-config cast-shape derivation.
/// </summary>
public class ServerMapStoreTests
{
    [Fact]
    public void LoadsAuthoredMaps_ByFileName()
    {
        var store = new ServerMapStore(NullLogger<ServerMapStore>.Instance);

        var tdm = store.GetMap("arena_tdm");
        tdm.Should().NotBeNull();
        tdm!.Entities.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void UnknownMap_ReturnsNull()
    {
        var store = new ServerMapStore(NullLogger<ServerMapStore>.Instance);
        store.GetMap("no_such_map").Should().BeNull();
    }

    [Fact]
    public void MapLookup_IsCaseInsensitive()
    {
        var store = new ServerMapStore(NullLogger<ServerMapStore>.Instance);
        store.GetMap("ARENA_TDM").Should().NotBeNull();
    }
}

public class ServerAbilityStoreTests
{
    private static ServerAbilityStore Store() => new(NullLogger<ServerAbilityStore>.Instance);

    [Fact]
    public void LoadsAuthoredAbilities()
    {
        var store = Store();
        store.GetAbility("brawler_punch").Should().NotBeNull();
        store.GetAbility("mage_bolt").Should().NotBeNull();
        store.All.Should().HaveCountGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void UnknownAbility_ReturnsNull()
    {
        Store().GetAbility("nope").Should().BeNull();
    }

    [Fact]
    public void BuildClientConfig_DerivesCapsuleCastShape_FromHitboxRoot()
    {
        var info = Store().BuildClientConfig().Abilities.Single(a => a.Id.Value == "brawler_punch");

        info.CastShape.Should().NotBeNull();
        info.CastShape!.Shape.Should().Be(TelegraphShape.Capsule);
        info.CastShape.Length.Should().Be(3f);
        info.CastShape.Width.Should().Be(2f, "width is 2 * hitbox radius (1.0)");
    }

    [Fact]
    public void BuildClientConfig_DerivesTargetCircleCastShape_FromProjectileRoot()
    {
        var info = Store().BuildClientConfig().Abilities.Single(a => a.Id.Value == "mage_bolt");

        info.CastShape.Should().NotBeNull();
        info.CastShape!.Shape.Should().Be(TelegraphShape.TargetCircle);
        info.CastShape.Radius.Should().BeApproximately(0.35f, 1e-4f, "no AoE → marker sized to the projectile radius");
    }
}
