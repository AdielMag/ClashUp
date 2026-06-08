using System.Collections.Concurrent;
using ClashUp.Server.GameServer.Maps;
using ClashUp.Server.GameServer.Registration;
using ClashUp.Server.GameServer.Simulation;
using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Server.GameServer.Match;

public sealed class MatchRegistry : IMatchRegistry, IDisposable
{
    private readonly ConcurrentDictionary<MatchId, MatchContext> _matches = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServicesRegistryClient _servicesClient;
    private readonly GameServerIdentity _identity;
    private readonly ServerMapStore _mapStore;

    public MatchRegistry(
        IServiceScopeFactory scopeFactory,
        ILoggerFactory loggerFactory,
        IServicesRegistryClient servicesClient,
        GameServerIdentity identity,
        ServerMapStore mapStore)
    {
        _scopeFactory = scopeFactory;
        _loggerFactory = loggerFactory;
        _servicesClient = servicesClient;
        _identity = identity;
        _mapStore = mapStore;
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

        var mapData = _mapStore.GetMap(provision.MapId);
        if (mapData != null)
            context.Simulation.LoadMap(mapData);

        context.OnMatchEndedEarly = id => _ = NotifyMatchEndedAsync(id);
        context.OnMatchEnded = id => RemoveAndDispose(id);
        context.TickLoop = new MatchTickLoop(context, _loggerFactory.CreateLogger<MatchTickLoop>());
        return context;
    }

    public void Remove(MatchId matchId)
    {
        if (_matches.TryRemove(matchId, out var ctx))
        {
            ctx.Dispose();
            _ = NotifyMatchEndedAsync(matchId);
        }
    }

    private void RemoveAndDispose(MatchId matchId)
    {
        if (_matches.TryRemove(matchId, out var ctx))
            ctx.Dispose();
    }

    private async Task NotifyMatchEndedAsync(MatchId matchId)
    {
        try
        {
            await _servicesClient.ReportMatchEndedAsync(
                new GsMatchEnded
                {
                    InstanceId = _identity.InstanceId,
                    MatchId = matchId,
                },
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _loggerFactory.CreateLogger<MatchRegistry>()
                .LogWarning(ex, "Failed to report match {MatchId} ended to Services", matchId);
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
