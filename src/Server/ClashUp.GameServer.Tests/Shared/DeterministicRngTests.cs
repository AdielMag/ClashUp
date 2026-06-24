using ClashUp.Shared.Simulation;
using FluentAssertions;
using Xunit;

namespace ClashUp.Server.GameServer.Tests.Shared;

public class DeterministicRngTests
{
    [Fact]
    public void SameSeed_ProducesIdenticalSequence()
    {
        var a = new DeterministicRng(12345);
        var b = new DeterministicRng(12345);

        for (int i = 0; i < 100; i++)
            a.Next().Should().Be(b.Next());
    }

    [Fact]
    public void DifferentSeeds_Diverge()
    {
        var a = new DeterministicRng(1);
        var b = new DeterministicRng(2);
        a.Next().Should().NotBe(b.Next());
    }

    [Fact]
    public void ZeroSeed_IsTreatedAsOne()
    {
        var zero = new DeterministicRng(0);
        var one = new DeterministicRng(1);
        // Seed 0 is remapped to 1 (xorshift can't escape the all-zero state).
        zero.Next().Should().Be(one.Next());
    }

    [Fact]
    public void NextFloat_StaysInUnitInterval()
    {
        var rng = new DeterministicRng(99);
        for (int i = 0; i < 1000; i++)
        {
            float f = rng.NextFloat();
            f.Should().BeGreaterThanOrEqualTo(0f).And.BeLessThanOrEqualTo(1f);
        }
    }

    [Fact]
    public void NextRange_StaysWithinBounds()
    {
        var rng = new DeterministicRng(7);
        for (int i = 0; i < 1000; i++)
        {
            float v = rng.NextRange(-3f, 10f);
            v.Should().BeGreaterThanOrEqualTo(-3f).And.BeLessThanOrEqualTo(10f);
        }
    }

    [Fact]
    public void ForTick_IsDeterministicPerTick()
    {
        var a = DeterministicRng.ForTick(1000u, 42);
        var b = DeterministicRng.ForTick(1000u, 42);
        a.Next().Should().Be(b.Next());

        var other = DeterministicRng.ForTick(1000u, 43);
        // Different ticks almost always diverge on the first draw.
        other.Next().Should().NotBe(a.Next());
    }
}
