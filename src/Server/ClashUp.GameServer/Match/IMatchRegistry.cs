using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Server.GameServer.Match;

/// <summary>
/// Singleton bookkeeping for matches hosted by this GS instance. The
/// per-match tick loop, AetherWorld, input buffer, etc. land here in
/// the AetherNet wiring step. For now this is a thin scaffold.
/// </summary>
public interface IMatchRegistry
{
    int Count { get; }

    bool TryGet(MatchId matchId, out MatchContext context);

    MatchContext Register(MatchProvision provision);

    void Remove(MatchId matchId);
}
