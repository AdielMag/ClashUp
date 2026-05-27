using System.Diagnostics;

namespace ClashUp.Server.GameServer.Simulation;

/// <summary>
/// Monotonic clock for a single match. Lets the tick loop maintain a
/// stable cadence even when the underlying timer wakes up late.
/// </summary>
public sealed class MatchClock
{
    private readonly Stopwatch _watch = Stopwatch.StartNew();

    public double ElapsedSeconds => _watch.Elapsed.TotalSeconds;
}
