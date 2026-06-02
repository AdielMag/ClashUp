using System.Linq;
using ClashUp.Server.Common.Auth;
using ClashUp.Server.GameServer.Match;
using ClashUp.Server.GameServer.Registration;
using ClashUp.Shared.Hubs;
using ClashUp.Shared.MessagePackObjects;
using MagicOnion.Server.Hubs;

namespace ClashUp.Server.GameServer.Hubs;

/// <summary>
/// Per-match StreamingHub. Validates the MatchToken JWT, registers the
/// connection with the match's Group, and enqueues inputs into the
/// per-match InputBuffer. No sim work happens here — see
/// docs/rules/magiconion-hub-discipline.md.
/// </summary>
public sealed class MatchHub : StreamingHubBase<IMatchHub, IMatchHubReceiver>, IMatchHub
{
    private readonly IMatchRegistry _matches;
    private readonly IMatchTokenValidator _tokens;
    private readonly GameServerIdentity _identity;

    private MatchContext? _context;
    private MatchTokenClaims _claims;

    public MatchHub(IMatchRegistry matches, IMatchTokenValidator tokens, GameServerIdentity identity)
    {
        _matches = matches;
        _tokens = tokens;
        _identity = identity;
    }

    public async Task<JoinResult> JoinAsync(MatchJoinRequest request)
    {
        _claims = _tokens.Validate(request.MatchToken);

        if (!string.Equals(_claims.MatchId, request.MatchId.Value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("MatchToken.matchId does not match the requested match.");
        }

        if (!string.IsNullOrEmpty(_identity.InstanceId)
            && !string.Equals(_claims.GsInstanceId, _identity.InstanceId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"MatchToken issued for GS {_claims.GsInstanceId}, not this instance ({_identity.InstanceId}).");
        }

        if (!_matches.TryGet(request.MatchId, out var context))
        {
            throw new InvalidOperationException($"Match {request.MatchId} not hosted on this instance.");
        }
        _context = context;

        var group = await Group.AddAsync(context.MatchId.Value);
        context.Group ??= group;

        PlayerSummary summary;
        if (context.IsPlayerInMatch(_claims.PlayerId))
        {
            context.MarkConnected(_claims.PlayerId);
            summary = context.GetPlayers().First(p => p.Id.Value == _claims.PlayerId);
        }
        else
        {
            summary = new PlayerSummary
            {
                Id = new PlayerId(_claims.PlayerId),
                DisplayName = $"Player-{_claims.PlayerId[..6]}",
                TeamId = 0,
            };
            context.AddPlayer(summary);
        }

        context.Group?.All.OnPlayerJoined(summary);

        // If the match already ended (client reconnected during the 2-second grace window),
        // replay OnMatchEnded directly to this client so it doesn't get stuck at 00:00.
        if (context.IsEnded && context.EndResult is { } endResult)
            Client.OnMatchEnded(endResult);

        return new JoinResult
        {
            You = summary.Id,
            Players = context.GetPlayers(),
            TickRateHz = context.Provision.TickRateHz,
            CurrentTick = context.Simulation.CurrentTick,
            DurationSeconds = context.Provision.DurationSeconds,
            ElapsedSeconds = context.Clock.ElapsedSeconds,
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
        if (_context is not null)
        {
            _context.MarkDisconnected(_claims.PlayerId);
            _context.Group?.All.OnPlayerLeft(new PlayerId(_claims.PlayerId), LeaveReason.Disconnect);
            if (_context.Group is not null)
                await _context.Group.RemoveAsync(Context);
        }
    }
}
