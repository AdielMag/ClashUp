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

    public MatchmakingServiceImpl(MatchmakingQueue queue, IMatchRepository matchRepo)
    {
        _queue = queue;
        _matchRepo = matchRepo;
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
