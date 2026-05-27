using ClashUp.Server.GameServer.Match;
using ClashUp.Shared.MessagePackObjects;
using ClashUp.Shared.Services;
using MagicOnion;
using MagicOnion.Server;

namespace ClashUp.Server.GameServer.Services;

/// <summary>
/// Receives match provisions from the Services tier and registers them
/// with the local MatchRegistry. Authoritative tick loop wiring comes
/// in step 10 of the plan.
/// </summary>
public sealed class MatchAdminService : ServiceBase<IMatchAdminService>, IMatchAdminService
{
    private readonly IMatchRegistry _matches;

    public MatchAdminService(IMatchRegistry matches)
    {
        _matches = matches;
    }

    public async UnaryResult<MatchReady> PrepareMatchAsync(MatchProvision provision)
    {
        // TODO step 7: validate the inter-tier JWT presented by Services.
        var context = _matches.Register(provision);
        await Task.Yield();
        return new MatchReady
        {
            MatchId = context.MatchId,
            ExpectedPlayers = provision.Players.Count,
            ReadyAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
    }
}
