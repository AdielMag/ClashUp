using System.Collections.Concurrent;
using ClashUp.Server.GameServer.Simulation;
using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Server.GameServer.Match;

public sealed class MatchRegistry : IMatchRegistry, IDisposable
{
    private readonly ConcurrentDictionary<MatchId, MatchContext> _matches = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerFactory _loggerFactory;

    public MatchRegistry(IServiceScopeFactory scopeFactory, ILoggerFactory loggerFactory)
    {
        _scopeFactory = scopeFactory;
        _loggerFactory = loggerFactory;
    }

    public int Count => _matches.Count;

    public bool TryGet(MatchId matchId, out MatchContext context) =>
        _matches.TryGetValue(matchId, out context!);

    public MatchContext Register(MatchProvision provision)
    {
        var scope = _scopeFactory.CreateScope();
        var context = new MatchContext(provision, scope);
        if (!_matches.TryAdd(provision.MatchId, context))
        {
            scope.Dispose();
            throw new InvalidOperationException(
                $"Match {provision.MatchId} is already registered on this instance.");
        }

        context.OnMatchEnded = id => Remove(id);
        context.TickLoop = new MatchTickLoop(context, _loggerFactory.CreateLogger<MatchTickLoop>());
        return context;
    }

    public void Remove(MatchId matchId)
    {
        if (_matches.TryRemove(matchId, out var ctx))
        {
            ctx.Dispose();
        }
    }

    public void Dispose()
    {
        foreach (var (_, ctx) in _matches)
        {
            ctx.Dispose();
        }
        _matches.Clear();
    }
}
