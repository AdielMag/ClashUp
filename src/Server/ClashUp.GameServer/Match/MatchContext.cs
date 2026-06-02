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

    public IServerSimulation Simulation =>
        _scope.ServiceProvider.GetRequiredService<IServerSimulation>();

    public InputBuffer Inputs =>
        _scope.ServiceProvider.GetRequiredService<InputBuffer>();

    public MatchClock Clock =>
        _scope.ServiceProvider.GetRequiredService<MatchClock>();

    public MatchTickLoop? TickLoop { get; set; }

    /// <summary>
    /// MagicOnion Group for this match. Captured by the first hub that
    /// joins; subsequent joins AddAsync to the same Group.
    /// </summary>
    public IGroup<IMatchHubReceiver>? Group { get; set; }

    private readonly ConcurrentDictionary<string, PlayerSummary> _players = new();
    private readonly ConcurrentDictionary<string, bool> _connected = new();

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

    /// <summary>Invoked when the match ends (timer expired). Registry wires this to Remove.</summary>
    public Action<MatchId>? OnMatchEnded { get; set; }

    public void Dispose()
    {
        TickLoop?.Dispose();
        _scope.Dispose();
    }
}
