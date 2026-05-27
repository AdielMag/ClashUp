using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Shared.Hubs;

public interface IMatchHubReceiver
{
    void OnSnapshot(SnapshotPacket snapshot);
    void OnPlayerJoined(PlayerSummary player);
    void OnPlayerLeft(PlayerId player, LeaveReason reason);
    void OnMatchEvent(MatchEvent matchEvent);
    void OnMatchEnded(MatchResult result);
}
