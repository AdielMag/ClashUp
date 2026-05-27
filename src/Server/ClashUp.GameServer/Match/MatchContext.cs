using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Server.GameServer.Match;

/// <summary>
/// Stub for the per-match state container. Will own the IServiceScope,
/// MatchClock, AetherWorld, InputBuffer, SnapshotEncoder once the
/// AetherNet wiring step (plan step 10) lands.
/// </summary>
public sealed class MatchContext
{
    public MatchContext(MatchProvision provision)
    {
        Provision = provision;
    }

    public MatchProvision Provision { get; }
    public MatchId MatchId => Provision.MatchId;
}
