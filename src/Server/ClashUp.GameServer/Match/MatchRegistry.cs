using System.Collections.Concurrent;
using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Server.GameServer.Match;

public sealed class MatchRegistry : IMatchRegistry
{
    private readonly ConcurrentDictionary<MatchId, MatchContext> _matches = new();

    public int Count => _matches.Count;

    public bool TryGet(MatchId matchId, out MatchContext context) =>
        _matches.TryGetValue(matchId, out context!);

    public MatchContext Register(MatchProvision provision)
    {
        var context = new MatchContext(provision);
        if (!_matches.TryAdd(provision.MatchId, context))
        {
            throw new InvalidOperationException(
                $"Match {provision.MatchId} is already registered on this instance.");
        }
        return context;
    }

    public void Remove(MatchId matchId) => _matches.TryRemove(matchId, out _);
}
