using ClashUp.Server.GameServer.Match;
using ClashUp.Shared.Hubs;
using ClashUp.Shared.MessagePackObjects;
using MagicOnion.Server.Hubs;

namespace ClashUp.Server.GameServer.Hubs;

/// <summary>
/// Per-match StreamingHub stub. Real validation, JWT-claim driven
/// reconnect, Group binding, and input enqueue land in steps 9–11 of
/// the plan. This skeleton keeps the surface compiling.
/// </summary>
public sealed class MatchHub : StreamingHubBase<IMatchHub, IMatchHubReceiver>, IMatchHub
{
    private readonly IMatchRegistry _matches;

    public MatchHub(IMatchRegistry matches)
    {
        _matches = matches;
    }

    public Task<JoinResult> JoinAsync(MatchJoinRequest request)
    {
        // TODO step 9: validate JWT, AddAsync to Group, return JoinResult populated from MatchContext.
        if (!_matches.TryGet(request.MatchId, out var context))
        {
            throw new InvalidOperationException($"Match {request.MatchId} not hosted on this instance.");
        }

        var result = new JoinResult
        {
            TickRateHz = context.Provision.TickRateHz,
            CurrentTick = 0,
        };
        return Task.FromResult(result);
    }

    public Task LeaveAsync() => Task.CompletedTask;

    public Task SubmitInputAsync(InputCommand command) =>
        // TODO step 10: enqueue into MatchContext.InputBuffer.
        Task.CompletedTask;

    public Task<PongResult> PingAsync(long clientStampMs)
    {
        var result = new PongResult
        {
            ClientStampMs = clientStampMs,
            ServerStampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
        return Task.FromResult(result);
    }
}
