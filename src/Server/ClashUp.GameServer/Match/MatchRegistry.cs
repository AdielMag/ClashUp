using System;
using System.Collections.Concurrent;
using ClashUp.Server.GameServer.Maps;
using ClashUp.Server.GameServer.Registration;
using ClashUp.Server.GameServer.Simulation;
using ClashUp.Shared.Characters;
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
        scope.ServiceProvider.GetRequiredService<MatchCharactersHolder>()
            .Initialize(provision.Characters);
        if (!_matches.TryAdd(provision.MatchId, context))
        {
            scope.Dispose();
            throw new InvalidOperationException(
                $"Match {provision.MatchId} is already registered on this instance.");
        }

        var mapData = _mapStore.GetMap(provision.MapId);
        if (mapData != null)
            context.Simulation.LoadMap(mapData);

        // Apply game-mode rules after the map is loaded (boxes are seeded from map data).
        context.Simulation.Configure(provision.ObjectiveType);

        // Materialize AI bots from the provision BEFORE the tick loop starts, so they're in the
        // roster (spawn, appear in JoinResult.Players) and get driven by the BotDirector each tick.
        var charactersConfig = provision.Characters ?? CharactersConfig.Default;
        var roster = charactersConfig.Characters;
        var botRng = new Random(unchecked((int)context.Simulation.RandomSeed));
        foreach (var assignment in provision.PlayerAssignments)
        {
            if (!assignment.IsBot) continue;
            string botId = assignment.PlayerId.Value;
            CharacterId character = roster.Count > 0
                ? roster[botRng.Next(roster.Count)].Id
                : new CharacterId(charactersConfig.DefaultCharacterId);
            string shortId = botId.StartsWith("bot:", StringComparison.Ordinal) ? botId.Substring(4) : botId;
            context.AddPlayer(new PlayerSummary
            {
                Id = assignment.PlayerId,
                DisplayName = "Bot " + shortId.Substring(0, Math.Min(4, shortId.Length)),
                TeamId = assignment.TeamId,
                ColorSlot = context.GetPlayers().Count,
                CharacterId = character,
                IsBot = true,
            });
            context.Bots.RegisterBot(botId, context.Simulation.RandomSeed);
        }

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
