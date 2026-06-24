using ClashUp.Server.GameServer.Simulation;
using ClashUp.Shared.MessagePackObjects;
using FluentAssertions;
using Xunit;

namespace ClashUp.Server.GameServer.Tests.GameServer;

public class InputBufferTests
{
    private static PlayerInput Input(string player, int tick) =>
        new(new PlayerId(player), new InputCommand { Tick = tick });

    [Fact]
    public void Dequeue_OnEmpty_ReturnsFalse()
    {
        var buf = new InputBuffer();
        buf.TryDequeueOne("p", out _).Should().BeFalse();
    }

    [Fact]
    public void Enqueue_Then_Dequeue_IsFifo()
    {
        var buf = new InputBuffer();
        buf.Enqueue(Input("p", 1));
        buf.Enqueue(Input("p", 2));

        buf.TryDequeueOne("p", out var first).Should().BeTrue();
        first.Tick.Should().Be(1);
        buf.TryDequeueOne("p", out var second).Should().BeTrue();
        second.Tick.Should().Be(2);
        buf.TryDequeueOne("p", out _).Should().BeFalse();
    }

    [Fact]
    public void Overflow_DropsOldest_KeepingTheLatestTwo()
    {
        var buf = new InputBuffer();
        // MaxQueueDepth is 2 — enqueuing a third drops the oldest so a stale move can't sit
        // in front of a fresh stop.
        buf.Enqueue(Input("p", 1));
        buf.Enqueue(Input("p", 2));
        buf.Enqueue(Input("p", 3));

        buf.TryDequeueOne("p", out var a).Should().BeTrue();
        buf.TryDequeueOne("p", out var b).Should().BeTrue();
        buf.TryDequeueOne("p", out _).Should().BeFalse();

        a.Tick.Should().Be(2, "tick 1 was dropped as the oldest");
        b.Tick.Should().Be(3);
    }

    [Fact]
    public void Queues_AreIsolatedPerPlayer()
    {
        var buf = new InputBuffer();
        buf.Enqueue(Input("a", 10));
        buf.Enqueue(Input("b", 20));

        buf.TryDequeueOne("a", out var a).Should().BeTrue();
        a.Tick.Should().Be(10);
        buf.TryDequeueOne("b", out var b).Should().BeTrue();
        b.Tick.Should().Be(20);
    }

    [Fact]
    public void Remove_ClearsThePlayersQueue()
    {
        var buf = new InputBuffer();
        buf.Enqueue(Input("p", 1));
        buf.Remove("p");
        buf.TryDequeueOne("p", out _).Should().BeFalse();
    }
}
