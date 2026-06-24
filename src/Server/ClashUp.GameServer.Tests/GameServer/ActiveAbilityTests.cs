using ClashUp.Server.GameServer.Simulation;
using ClashUp.Shared.Abilities;
using FluentAssertions;
using Xunit;

namespace ClashUp.Server.GameServer.Tests.GameServer;

public class ActiveAbilityTests
{
    [Fact]
    public void Create_FlattensNodeTree_DepthFirst_ChildrenThenNext()
    {
        // Root(Parallel) -> Children[ A(Hitbox), B(Hitbox) ] -> Next C(Hitbox)
        var a = new AbilityNode { Type = AbilityNodeType.Hitbox };
        var b = new AbilityNode { Type = AbilityNodeType.Hitbox };
        var c = new AbilityNode { Type = AbilityNodeType.Hitbox };
        var root = new AbilityNode
        {
            Type = AbilityNodeType.Parallel,
            Children = new[] { a, b },
            Next = c,
        };
        var def = new AbilityDefinition { Id = new AbilityId("x"), RootNode = root };

        var active = ActiveAbility.Create("caster", aimYaw: 0f, def);

        active.FlatNodes.Should().Equal(root, a, b, c);
        active.NodeTicksElapsed.Should().HaveCount(4);
        active.NodeStarted.Should().HaveCount(4);
        active.NodeFinished.Should().HaveCount(4);
        active.HitboxHitEntities.Should().HaveCount(4);
        active.CasterId.Should().Be("caster");
    }

    [Fact]
    public void Create_WithNullRoot_ProducesEmptyFlatNodes()
    {
        var def = new AbilityDefinition { Id = new AbilityId("empty") };
        var active = ActiveAbility.Create("c", 0f, def);
        active.FlatNodes.Should().BeEmpty();
    }

    [Fact]
    public void Create_CarriesTargetPoint()
    {
        var def = new AbilityDefinition { Id = new AbilityId("t"), RootNode = new AbilityNode { Type = AbilityNodeType.Projectile } };
        var active = ActiveAbility.Create("c", 45f, def, hasTargetPoint: true, targetX: 3f, targetZ: -4f);

        active.HasTargetPoint.Should().BeTrue();
        active.TargetX.Should().Be(3f);
        active.TargetZ.Should().Be(-4f);
        active.AimYaw.Should().Be(45f);
    }
}
