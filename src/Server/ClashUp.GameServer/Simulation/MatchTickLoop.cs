using System.Collections.Generic;
using System.Linq;

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

    // Once ≥2 teams have ever been present, dropping to ≤1 team (via forfeit) ends the match.
    // Once ≥1 player has ever joined, dropping to 0 (last player left) closes the match.
    // Both guards prevent an instant end at startup, before/while players are still joining.
    private bool _sawMultipleTeams;
    private bool _sawAnyPlayer;
    // Once a real (non-bot) player has been present, dropping to 0 real players closes the match
    // even if bots remain — so a bot-only match never lingers and no bot is awarded the win.
    private bool _sawRealPlayer;

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

                // End conditions. "Alive teams" excludes eliminated players (no-respawn modes);
                // in survival nobody is ever eliminated, so this matches the old forfeit-driven set.
                var aliveTeams = ComputeAliveTeams();
                if (aliveTeams.Count >= 1)
                    _sawAnyPlayer = true;
                if (aliveTeams.Count >= 2)
                    _sawMultipleTeams = true;

                int realCount = _context.RealPlayerCount();
                if (realCount >= 1)
                    _sawRealPlayer = true;

                // All human players have left — close the match even if bots remain. Checked before the
                // "last team standing" rule so a bot team is never declared the winner of an empty match.
                if (_sawRealPlayer && realCount == 0)
                {
                    await EndMatchAsync(0, "all human players left");
                    break;
                }

                // Last player left (e.g. a solo match) — just close it.
                if (_sawAnyPlayer && aliveTeams.Count == 0)
                {
                    await EndMatchAsync(0, "all players left");
                    break;
                }
                // Everyone but one team is gone/eliminated — that team wins.
                if (_sawMultipleTeams && aliveTeams.Count <= 1)
                {
                    await EndMatchAsync(aliveTeams.FirstOrDefault(), "last team standing");
                    break;
                }

                if (durationSeconds > 0 && _context.Clock.ElapsedSeconds >= durationSeconds)
                {
                    // Time-limited objective modes award the win to the highest-scoring team.
                    int winner = _context.ObjectiveType == "elimination" ? TeamWithMostPoints() : 0;
                    await EndMatchAsync(winner, "timer expired");
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

    private async Task EndMatchAsync(int winningTeamId, string reason)
    {
        var result = BuildMatchResult(winningTeamId);
        _context.IsEnded = true;
        _context.EndResult = result;

        // Notify Services immediately so the DB is updated before any client
        // can loop back through CheckActiveMatchAsync (which takes ~2s via lobby reload).
        _context.OnMatchEndedEarly?.Invoke(_context.MatchId);

        _context.Group?.All.OnMatchEnded(result);
        _logger.LogInformation("Match {MatchId} ended ({Reason}), winner team {WinningTeamId}",
            _context.MatchId, reason, winningTeamId);

        // Give the broadcast time to flush before tearing down connections.
        await Task.Delay(2000, CancellationToken.None);

        _context.OnMatchEnded?.Invoke(_context.MatchId);
    }

    private MatchResult BuildMatchResult(int winningTeamId)
    {
        return new MatchResult
        {
            MatchId = _context.MatchId,
            WinningTeamId = winningTeamId,
            TeamScores = _context.Simulation.GetTeamScores(),
            EndedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
    }

    // Distinct teams that still have at least one non-eliminated player. In survival no one is ever
    // eliminated, so this reduces to the roster-team set (legacy behavior).
    private IReadOnlyCollection<int> ComputeAliveTeams()
    {
        var sim = _context.Simulation;
        return _context.GetPlayers()
            .Where(p => !sim.IsEliminated(p.Id.Value))
            .Select(p => p.TeamId)
            .Distinct()
            .ToArray();
    }

    // Highest-scoring team by total points; ties resolve to 0 (draw).
    private int TeamWithMostPoints()
    {
        var scores = _context.Simulation.GetTeamScores();
        int bestTeam = 0, bestScore = -1;
        bool tie = false;
        foreach (var kvp in scores)
        {
            if (!int.TryParse(kvp.Key, out int team)) continue;
            if (kvp.Value > bestScore) { bestScore = kvp.Value; bestTeam = team; tie = false; }
            else if (kvp.Value == bestScore) { tie = true; }
        }
        return tie ? 0 : bestTeam;
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

            if (p.IsBot)
            {
                // Bots are server-driven: synthesize the same InputCommand a real player would send.
                var botInput = _context.Bots.Think(p.Id.Value, _context.Simulation, _context.Simulation.CurrentTick);
                _context.Simulation.ApplyInput(p.Id, botInput);
            }
            else if (_context.Inputs.TryDequeueOne(p.Id.Value, out var input))
            {
                _context.Simulation.ApplyInput(p.Id, input);
            }
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
