using ClashUp.Server.Common.Ccu;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClashUp.Server.GameServer.Tests.Common;

public class GraceCcuCounterTests
{
    private static GraceCcuCounter Counter(TimeSpan grace) => new(grace, NullLogger.Instance);

    [Fact]
    public void Add_CountsEachIdOnce()
    {
        using var c = Counter(TimeSpan.Zero);
        c.Add("a");
        c.Add("a"); // duplicate — no double count
        c.Add("b");
        c.Count.Should().Be(2);
    }

    [Fact]
    public void Add_IgnoresEmptyId()
    {
        using var c = Counter(TimeSpan.Zero);
        c.Add("");
        c.Add(null!);
        c.Count.Should().Be(0);
    }

    [Fact]
    public void Remove_WithZeroGrace_RemovesImmediately()
    {
        using var c = Counter(TimeSpan.Zero);
        c.Add("a");
        c.Remove("a");
        c.Count.Should().Be(0);
    }

    [Fact]
    public void Remove_UnknownId_IsNoOp()
    {
        using var c = Counter(TimeSpan.Zero);
        c.Add("a");
        c.Remove("ghost");
        c.Count.Should().Be(1);
    }

    [Fact]
    public async Task Remove_WithGrace_KeepsCounted_UntilWindowElapses()
    {
        using var c = Counter(TimeSpan.FromMilliseconds(150));
        c.Add("a");
        c.Remove("a");

        c.Count.Should().Be(1, "still within the grace window");
        await Task.Delay(350);
        c.Count.Should().Be(0, "the grace window elapsed without a reconnect");
    }

    [Fact]
    public async Task Reconnect_WithinGrace_CancelsRemoval()
    {
        using var c = Counter(TimeSpan.FromMilliseconds(200));
        c.Add("a");
        c.Remove("a");
        c.Add("a"); // reconnect before the timer fires

        await Task.Delay(350);
        c.Count.Should().Be(1, "the reconnect cancelled the pending removal");
    }
}
