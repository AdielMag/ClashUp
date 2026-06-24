using ClashUp.Shared.Simulation;
using FluentAssertions;
using Xunit;

namespace ClashUp.Server.GameServer.Tests.Shared;

public class MovementModelTests
{
    [Fact]
    public void EncodeAxis_ClampsToFullScale()
    {
        MovementModel.EncodeAxis(1f).Should().Be((short)32767);
        MovementModel.EncodeAxis(-1f).Should().Be((short)-32767);
        MovementModel.EncodeAxis(5f).Should().Be((short)32767, "values past 1 saturate");
        MovementModel.EncodeAxis(-5f).Should().Be((short)-32767);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.5f)]
    [InlineData(-0.25f)]
    [InlineData(1f)]
    public void EncodeDecode_RoundTrips_WithinQuantization(float value)
    {
        float decoded = MovementModel.DecodeAxis(MovementModel.EncodeAxis(value));
        decoded.Should().BeApproximately(value, 1e-4f);
    }

    [Fact]
    public void Step_NormalizesDiagonalInput_ToConstantSpeed()
    {
        float x = 0f, z = 0f, yaw = 0f;
        MovementModel.Step(ref x, ref z, ref yaw, 1f, 1f, dt: 1.0, moveSpeed: 5f);

        // Diagonal input is normalized: total displacement == moveSpeed * dt, not 5*sqrt(2).
        float dist = MathF.Sqrt(x * x + z * z);
        dist.Should().BeApproximately(5f, 1e-3f);
    }

    [Fact]
    public void Step_RespectsMoveSpeed()
    {
        float x = 0f, z = 0f, yaw = 0f;
        MovementModel.Step(ref x, ref z, ref yaw, 1f, 0f, dt: 1.0, moveSpeed: 8f);
        x.Should().BeApproximately(8f, 1e-3f);
    }

    [Fact]
    public void Step_FacesMovementDirection()
    {
        float x = 0f, z = 0f, yaw = 123f;
        MovementModel.Step(ref x, ref z, ref yaw, 1f, 0f, dt: 0.1);
        yaw.Should().BeApproximately(90f, 1e-2f, "moving along +X faces east (atan2(1,0))");

        MovementModel.Step(ref x, ref z, ref yaw, 0f, 1f, dt: 0.1);
        yaw.Should().BeApproximately(0f, 1e-2f, "moving along +Z faces north");
    }

    [Fact]
    public void Step_WithNoInput_LeavesYawUnchanged()
    {
        float x = 0f, z = 0f, yaw = 47f;
        MovementModel.Step(ref x, ref z, ref yaw, 0f, 0f, dt: 0.1);
        yaw.Should().Be(47f, "a stationary player keeps its facing");
        x.Should().Be(0f);
        z.Should().Be(0f);
    }
}
