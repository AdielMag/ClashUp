using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ClashUp.Server.GameServer.Simulation;
using ClashUp.Shared.Hubs;
using ClashUp.Shared.MessagePackObjects;
using MagicOnion.Server.Hubs;

namespace ClashUp.Server.GameServer.Match;

/// <summary>
/// Per-match state container. Owns its own IServiceScope (MS DI), so
/// scoped services (simulation, clock, input buffer, tick loop) are
/// disposed cleanly on match end. See docs/rules/server-authority.md.
/// </summary>
public sealed class MatchContext : IDisposable
{
    private readonly IServiceScope _scope;

    public MatchContext(MatchProvision provision, IServiceScope scope)
    {
        Provision = provision;
        _scope = scope;
    }

    public MatchProvision Provision { get; }
    public MatchId MatchId => Provision.MatchId;
    public string ObjectiveType => Provision.ObjectiveType;

    public IServerSimulation Simulation =>
        _scope.ServiceProvider.GetRequiredService<IServerSimulation>();

    public InputBuffer Inputs =>
        _scope.ServiceProvider.GetRequiredService<InputBuffer>();

    public MatchClock Clock =>
        _scope.ServiceProvider.GetRequiredService<MatchClock>();

    public BotDirector Bots =>
        _scope.ServiceProvider.GetRequiredService<BotDirector>();

    public MatchTickLoop? TickLoop { get; set; }

    /// <summary>
    /// MagicOnion Group for this match. Captured by the first hub that
    /// joins; subsequent joins AddAsync to the same Group.
    /// </summary>
    public IGroup<IMatchHubReceiver>? Group { get; set; }

    private readonly ConcurrentDictionary<string, PlayerSummary> _players = new();
    private readonly ConcurrentDictionary<string, bool> _connected = new();

    // Players who explicitly forfeited. They are barred from rejoining this match
    // even if a reconnect token is somehow issued (vs. a disconnect, which is
    // reconnectable). Distinct from _players: the player is removed from _players
    // but remembered here for the lifetime of the (still-running) match.
    private readonly ConcurrentDictionary<string, byte> _forfeited = new();

    public void AddPlayer(PlayerSummary player)
    {
        _players[player.Id.Value] = player;
        _connected[player.Id.Value] = true;
    }

    public void MarkDisconnected(string playerId) => _connected[playerId] = false;
    public void MarkConnected(string playerId) => _connected[playerId] = true;
    public bool IsPlayerInMatch(string playerId) => _players.ContainsKey(playerId);
    public bool IsConnected(string playerId) => _connected.TryGetValue(playerId, out var c) && c;
    public void RemovePlayer(string playerId)
    {
        _players.TryRemove(playerId, out _);
        _connected.TryRemove(playerId, out _);
    }
    public List<PlayerSummary> GetPlayers() => _players.Values.ToList();

    /// <summary>Count of real (non-bot) players still in the roster. The tick loop closes the match
    /// once this hits 0 after a real player has been present, so a bot-only match never lingers.</summary>
    public int RealPlayerCount() => _players.Values.Count(p => !p.IsBot);

    /// <summary>
    /// Distinct team ids that still have at least one player in the match. A forfeit
    /// (RemovePlayer) drops a player from this set; a disconnect only flips _connected
    /// and keeps the player, so a reconnectable drop does NOT reduce alive teams.
    /// </summary>
    public IReadOnlyCollection<int> GetAliveTeamIds() =>
        _players.Values.Select(p => p.TeamId).Distinct().ToArray();

    /// <summary>Permanently bar a player from rejoining (forfeit) and drop them from the roster.</summary>
    public void Forfeit(string playerId)
    {
        _forfeited[playerId] = 0;
        RemovePlayer(playerId);
    }

    public bool IsForfeited(string playerId) => _forfeited.ContainsKey(playerId);

    /// <summary>
    /// Invoked immediately when the match ends — before the 2-second client broadcast window.
    /// Registry uses this to fire the Services notification early so the DB is updated
    /// before any client can loop back through CheckActiveMatchAsync.
    /// </summary>
    public Action<MatchId>? OnMatchEndedEarly { get; set; }

    /// <summary>Invoked after the 2-second broadcast window to remove the match from the registry.</summary>
    public Action<MatchId>? OnMatchEnded { get; set; }

    /// <summary>
    /// Set to true (and EndResult populated) the moment the tick loop broadcasts OnMatchEnded.
    /// Used by JoinAsync to replay the result to clients that reconnect during the 2-second grace window.
    /// </summary>
    public bool IsEnded { get; set; }
    public MatchResult? EndResult { get; set; }

    public void Dispose()
    {
        TickLoop?.Dispose();
        _scope.Dispose();
    }
}
