using ClashUp.Server.Common.Auth;
using ClashUp.Server.Services.Matchmaking;
using ClashUp.Server.Services.Persistence;
using ClashUp.Shared.MessagePackObjects;
using ClashUp.Shared.Services;
using MagicOnion;
using MagicOnion.Server;

namespace ClashUp.Server.Services.Services;

public sealed class MatchmakingServiceImpl : ServiceBase<IMatchmakingService>, IMatchmakingService
{
    private readonly MatchmakingQueue _queue;
    private readonly IMatchRepository _matchRepo;
    private readonly IJwtTokenIssuer _tokens;
    private readonly IGameServerInstanceRepository _gsRepo;
    private readonly ILogger<MatchmakingServiceImpl> _logger;

    public MatchmakingServiceImpl(
        MatchmakingQueue queue,
        IMatchRepository matchRepo,
        IJwtTokenIssuer tokens,
        IGameServerInstanceRepository gsRepo,
        ILogger<MatchmakingServiceImpl> logger)
    {
        _queue = queue;
        _matchRepo = matchRepo;
        _tokens = tokens;
        _gsRepo = gsRepo;
        _logger = logger;
    }

    public UnaryResult<QueueTicket> EnqueueAsync(QueueRequest request)
    {
        // TODO: extract playerId from validated end-user JWT. Phase-1 stub uses a placeholder.
        var playerId = ResolveCurrentPlayerId();
        var entry = _queue.Enqueue(playerId, request.ModeId);
        return new UnaryResult<QueueTicket>(new QueueTicket { TicketId = entry.TicketId });
    }

    public UnaryResult CancelAsync(QueueTicket ticket)
    {
        _queue.TryCancel(ticket.TicketId);
        return default;
    }

    public UnaryResult<TicketPoll> PollTicketAsync(QueueTicket ticket)
    {
        if (!_queue.TryGet(ticket.TicketId, out var entry))
        {
            return new UnaryResult<TicketPoll>(new TicketPoll
            {
                Status = TicketStatus.Failed,
                FailureReason = "unknown_ticket",
            });
        }

        return new UnaryResult<TicketPoll>(new TicketPoll
        {
            Status = entry.Status,
            Handoff = entry.Handoff,
            FailureReason = entry.FailureReason,
        });
    }

    public async UnaryResult<TicketPoll> CheckActiveMatchAsync()
    {
        var ct = Context.CallContext.CancellationToken;
        var playerId = ResolveCurrentPlayerId();
        _logger.LogInformation("CheckActiveMatch for player {PlayerId}", playerId);

        var match = await _matchRepo.FindActiveForPlayerAsync(playerId, ct);
        if (match is null)
        {
            _logger.LogInformation("No active match found for player {PlayerId}", playerId);
            return new TicketPoll { Status = TicketStatus.Queued };
        }

        _logger.LogInformation("Found active match {MatchId} on GS {GsInstanceId} for player {PlayerId}",
            match.MatchId, match.GsInstanceId, playerId);

        if (match.DurationSeconds > 0)
        {
            var elapsed = (DateTime.UtcNow - match.CreatedAt).TotalSeconds;
            var remaining = match.DurationSeconds - elapsed;
            if (remaining < 10)
            {
                _logger.LogInformation("Match {MatchId} has {Remaining:F1}s remaining — treating as ended for player {PlayerId}",
                    match.MatchId, remaining, playerId);
                await _matchRepo.SetStateAsync(match.MatchId, "Ended", ct);
                return new TicketPoll { Status = TicketStatus.Queued };
            }
        }

        var gs = await _gsRepo.GetByIdAsync(match.GsInstanceId, ct);
        if (gs is null || gs.Status != "Healthy")
        {
            _logger.LogWarning("GS {GsInstanceId} for match {MatchId} is gone/unhealthy — ending orphaned match",
                match.GsInstanceId, match.MatchId);
            await _matchRepo.SetStateAsync(match.MatchId, "Ended", ct);
            return new TicketPoll { Status = TicketStatus.Queued };
        }

        var token = _tokens.IssueMatchToken(playerId, match.MatchId, match.GsInstanceId);
        _logger.LogInformation("Issuing reconnect token for player {PlayerId} to match {MatchId}", playerId, match.MatchId);
        return new TicketPoll
        {
            Status = TicketStatus.Matched,
            Handoff = new MatchHandoff
            {
                MatchId = new MatchId(match.MatchId),
                GsEndpoint = gs.PublicEndpoint,
                MatchToken = token.Jwt,
                MatchTokenExpiresAtMs = new DateTimeOffset(token.ExpiresAt).ToUnixTimeMilliseconds(),
            },
        };
    }

    public async UnaryResult<MatchHandoff> ResolveMatchAsync(MatchId matchId)
    {
        var ct = Context.CallContext.CancellationToken;
        var doc = await _matchRepo.GetByIdAsync(matchId.Value, ct)
            ?? throw new InvalidOperationException($"Match {matchId} not found.");

        // Note: this returns the stored MatchToken-less handoff. Sticky-reconnect
        // JWT reissuing happens in step 11 (reconnect hardening).
        return new MatchHandoff
        {
            MatchId = matchId,
            GsEndpoint = doc.GsEndpoint,
            MatchToken = string.Empty,
        };
    }

    private string ResolveCurrentPlayerId()
    {
        // TODO: pull from validated JWT once the JwtBearer auth pipeline is wired.
        // For the bring-up smoke we accept a header-injected id or a synthetic one.
        var headers = Context.CallContext.RequestHeaders;
        var fromHeader = headers.GetValue("x-clashup-player");
        return string.IsNullOrEmpty(fromHeader) ? Guid.NewGuid().ToString("N") : fromHeader;
    }
}
