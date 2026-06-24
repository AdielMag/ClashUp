using ClashUp.Shared.Simulation;
using FluentAssertions;
using Xunit;

namespace ClashUp.Server.GameServer.Tests.Shared;

public class HealthTableTests
{
    [Fact]
    public void Initialize_SetsCurrentAndMaxToFull()
    {
        var t = new HealthTable();
        t.Initialize("p", 80f);

        t.GetHealth("p").Should().Be(80f);
        t.GetMaxHealth("p").Should().Be(80f);
        t.IsAlive("p").Should().BeTrue();
    }

    [Fact]
    public void Unknown_Player_ReadsAsZero_AndDead()
    {
        var t = new HealthTable();
        t.GetHealth("ghost").Should().Be(0f);
        t.GetMaxHealth("ghost").Should().Be(0f);
        t.IsAlive("ghost").Should().BeFalse();
    }

    [Fact]
    public void ApplyDamage_Subtracts_AndClampsAtZero()
    {
        var t = new HealthTable();
        t.Initialize("p", 100f);

        t.ApplyDamage("p", 30f).Should().Be(70f);
        t.ApplyDamage("p", 1000f).Should().Be(0f, "health never goes negative");
        t.IsAlive("p").Should().BeFalse();
    }

    [Fact]
    public void ApplyDamage_UsesAbsoluteAmount()
    {
        var t = new HealthTable();
        t.Initialize("p", 100f);
        // A negative amount still removes health (abs), never heals.
        t.ApplyDamage("p", -25f).Should().Be(75f);
    }

    [Fact]
    public void ApplyDamage_OnUnknownPlayer_ReturnsZero()
    {
        var t = new HealthTable();
        t.ApplyDamage("ghost", 10f).Should().Be(0f);
    }

    [Fact]
    public void Invulnerable_Player_TakesNoDamage()
    {
        var t = new HealthTable();
        t.Initialize("p", 100f);
        t.SetInvulnerable("p", 5);

        t.IsInvulnerable("p").Should().BeTrue();
        t.ApplyDamage("p", 40f).Should().Be(100f, "invuln blocks damage");
    }

    [Fact]
    public void Tick_DecrementsInvuln_AndExpires()
    {
        var t = new HealthTable();
        t.Initialize("p", 100f);
        t.SetInvulnerable("p", 2);

        t.Tick();
        t.IsInvulnerable("p").Should().BeTrue();
        t.Tick();
        t.IsInvulnerable("p").Should().BeFalse("the 2-tick window elapsed");

        // Damage now lands.
        t.ApplyDamage("p", 10f).Should().Be(90f);
    }

    [Fact]
    public void ApplyHeal_ClampsAtMax()
    {
        var t = new HealthTable();
        t.Initialize("p", 100f);
        t.ApplyDamage("p", 60f); // 40 left

        t.ApplyHeal("p", 30f, 100f).Should().Be(70f);
        t.ApplyHeal("p", 999f, 100f).Should().Be(100f, "heal caps at the passed max");
    }

    [Fact]
    public void SetMaxHealth_WithHealDelta_AddsTheGainedAmount()
    {
        var t = new HealthTable();
        t.Initialize("p", 100f);
        t.ApplyDamage("p", 50f); // 50/100

        t.SetMaxHealth("p", 120f, healDelta: true);

        t.GetMaxHealth("p").Should().Be(120f);
        t.GetHealth("p").Should().Be(70f, "current rose by the +20 max gain");
    }

    [Fact]
    public void SetMaxHealth_Lowering_ClampsCurrentToNewMax()
    {
        var t = new HealthTable();
        t.Initialize("p", 100f);

        t.SetMaxHealth("p", 60f, healDelta: false);

        t.GetMaxHealth("p").Should().Be(60f);
        t.GetHealth("p").Should().Be(60f, "current is clamped down to the lower max");
    }

    [Fact]
    public void SnapHealth_SetsExactValue()
    {
        var t = new HealthTable();
        t.Initialize("p", 100f);
        t.SnapHealth("p", 42f);
        t.GetHealth("p").Should().Be(42f);
    }
}
