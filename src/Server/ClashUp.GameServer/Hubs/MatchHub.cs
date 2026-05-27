using ClashUp.Server.GameServer.Match;
using ClashUp.Shared.Hubs;
using ClashUp.Shared.MessagePackObjects;
using MagicOnion.Server.Hubs;

namespace ClashUp.Server.GameServer.Hubs;

/// <summary>
/// Per-match StreamingHub. Validates the join token, registers the
/// connection with the match's Group, and enqueues inputs into the
/// per-match InputBuffer. No sim work happens here — see
/// docs/rules/magiconion-hub-discipline.md.
///
/// JWT validation of the MatchToken is a TODO for step 11 (reconnect
/// hardening). The skeleton is in place; the validator call is the
/// only missing piece.
/// </summary>
public sealed class MatchHub : StreamingHubBase<IMatchHub, IMatchHubReceiver>, IMatchHub
{
    private readonly IMatchRegistry _matches;
    private MatchContext? _context;

    public MatchHub(IMatchRegistry matches)
    {
        _matches = matches;
    }

    public async Task<JoinResult> JoinAsync(MatchJoinRequest request)
    {
        if (!_matches.TryGet(request.MatchId, out var context))
        {
            throw new InvalidOperationException($"Match {request.MatchId} not hosted on this instance.");
        }
        _context = context;

        var group = await Group.AddAsync(context.MatchId.Value);
        context.Group ??= group;

        // Notify everyone (including the joiner) so HUD lists rebuild.
        var summary = new PlayerSummary
        {
            // TODO step 11: pull PlayerId from validated MatchToken claims.
            Id = new PlayerId("anonymous"),
            DisplayName = "Player",
            TeamId = 0,
        };
        context.Group?.All.OnPlayerJoined(summary);

        return new JoinResult
        {
            You = summary.Id,
            TickRateHz = context.Provision.TickRateHz,
            CurrentTick = context.Simulation.CurrentTick,
        };
    }

    public async Task LeaveAsync()
    {
        if (_context?.Group is not null)
        {
            await _context.Group.RemoveAsync(Context);
        }
    }

    public Task SubmitInputAsync(InputCommand command)
    {
        _context?.Inputs.Enqueue(command);
        return Task.CompletedTask;
    }

    public Task<PongResult> PingAsync(long clientStampMs)
    {
        var result = new PongResult
        {
            ClientStampMs = clientStampMs,
            ServerStampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
        return Task.FromResult(result);
    }

    protected override async ValueTask OnDisconnected()
    {
        if (_context?.Group is not null)
        {
            await _context.Group.RemoveAsync(Context);
        }
    }
}
