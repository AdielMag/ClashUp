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
        double tickIntervalSec = 1.0 / _context.Provision.TickRateHz;
        var durationSeconds = _context.Provision.DurationSeconds;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(tickIntervalSec));

        try
        {
            while (await timer.WaitForNextTickAsync(_cts.Token))
            {
                Drain();
                _context.Simulation.Step(tickIntervalSec);
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
        // Consume exactly one input per player per tick so the server advances 1:1 with the
        // client's predicted steps. On underflow we apply nothing — the player holds (zero
        // velocity) rather than repeating stale movement, which would push the server past
        // what the client predicted (e.g. sinking into a wall after the finger lifts). The
        // playout buffer in InputBuffer keeps underflow rare during real movement.
        foreach (var p in _context.GetPlayers())
        {
            _context.Simulation.EnsurePlayer(p.Id, p.ColorSlot, p.TeamId, p.CharacterId);

            if (_context.Inputs.TryDequeueOne(p.Id.Value, out var input))
                _context.Simulation.ApplyInput(p.Id, input);
        }
    }

    private Task BroadcastAsync()
    {
        var group = _context.Group;
        if (group is null)
        {
            _context.Simulation.DrainAbilityEvents();
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

        var abilityEvents = _context.Simulation.DrainAbilityEvents();
        foreach (var evt in abilityEvents)
            group.All.OnMatchEvent(evt);

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
