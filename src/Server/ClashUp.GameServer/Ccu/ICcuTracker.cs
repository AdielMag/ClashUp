namespace ClashUp.Server.GameServer.Ccu;

/// <summary>
/// Tracks Concurrent Connected Users (CCU) for this GameServer instance across
/// all matches it hosts. A disconnect starts a grace timer rather than removing
/// the player immediately; reconnecting within the grace window keeps them
/// counted. This is the per-instance scaling signal consumed by the GCP MIG
/// autoscaler (via <c>custom.googleapis.com/gameserver/ccu</c>).
/// </summary>
public interface ICcuTracker
{
    /// <summary>Current number of counted players (live + within disconnect grace).</summary>
    int CurrentCcu { get; }

    /// <summary>
    /// Record that a player connected/joined. Cancels any pending grace-period
    /// removal (reconnect); counts a brand-new player exactly once.
    /// </summary>
    void PlayerConnected(string playerId);

    /// <summary>
    /// Record that a player disconnected. Starts the grace timer; the player is
    /// removed from the count only if they do not reconnect before it elapses.
    /// </summary>
    void PlayerDisconnected(string playerId);
}
