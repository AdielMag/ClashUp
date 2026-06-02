using ClashUp.Server.GameServer.Match;
using ClashUp.Shared.Hubs;
using ClashUp.Shared.MessagePackObjects;
using MagicOnion.Server.Hubs;

namespace ClashUp.Server.GameServer.Simulation;

/// <summary>
/// One per match. Runs on a dedicated long-running task — NOT the
/// request thread pool. Drives the authoritative simulation tick and
/// broadcasts snapshots via the match's MagicOnion Group.
///
/// Per docs/rules/magiconion-hub-discipline.md, the hub only enqueues
/// inputs; this loop is the only place that broadcasts.
/// </summary>
public sealed class MatchTickLoop : IDisposable
{
    private readonly MatchContext _context;
    private readonly ILogger<MatchTickLoop> _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _runTask;

    public MatchTickLoop(MatchContext context, ILogger<MatchTickLoop> logger)
    {
        _context = context;
        _logger = logger;
        _runTask = Task.Factory.StartNew(
            RunAsync,
            _cts.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();
    }

    private async Task RunAsync()
    {
        var tickIntervalMs = Math.Max(1, 1000 / _context.Provision.TickRateHz);
        var durationSeconds = _context.Provision.DurationSeconds;
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(tickIntervalMs));

        try
        {
            while (await timer.WaitForNextTickAsync(_cts.Token))
            {
                Drain();
                _context.Simulation.Step(tickIntervalMs / 1000.0);
                await BroadcastAsync();

                if (durationSeconds > 0 && _context.Clock.ElapsedSeconds >= durationSeconds)
                {
                    var result = BuildMatchResult();
                    _context.IsEnded = true;
                    _context.EndResult = result;

                    // Notify Services immediately so the DB is updated before any client
                    // can loop back through CheckActiveMatchAsync (which takes ~2s via lobby reload).
                    _context.OnMatchEndedEarly?.Invoke(_context.MatchId);

                    _context.Group?.All.OnMatchEnded(result);
                    _logger.LogInformation("Match {MatchId} ended (timer expired)", _context.MatchId);

                    // Give the broadcast time to flush before tearing down connections.
                    await Task.Delay(2000, CancellationToken.None);

                    _context.OnMatchEnded?.Invoke(_context.MatchId);
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Match ended; expected.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tick loop failed for match {MatchId}", _context.MatchId);
        }
    }

    private MatchResult BuildMatchResult()
    {
        return new MatchResult
        {
            MatchId = _context.MatchId,
            WinningTeamId = 0,
            EndedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
    }

    private void Drain()
    {
        while (_context.Inputs.TryDequeue(out var command))
        {
            _context.Simulation.ApplyInput(command);
        }
    }

    private Task BroadcastAsync()
    {
        var group = _context.Group;
        if (group is null)
        {
            return Task.CompletedTask;
        }

        var snapshot = new SnapshotPacket
        {
            Tick = _context.Simulation.CurrentTick,
            ServerStampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            BaselineTick = 0,
            DeltaBlob = _context.Simulation.EncodeDelta(0),
        };

        group.All.OnSnapshot(snapshot);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _runTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch (Exception)
        {
            // Best effort.
        }
        _cts.Dispose();
    }
}
