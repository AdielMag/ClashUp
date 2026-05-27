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

    public void Dispose()
    {
        TickLoop?.Dispose();
        _scope.Dispose();
    }
}
