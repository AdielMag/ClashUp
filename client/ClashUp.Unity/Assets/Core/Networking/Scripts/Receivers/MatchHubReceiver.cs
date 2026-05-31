using System;

using ClashUp.Client.Core;
using ClashUp.Shared.Hubs;
using ClashUp.Shared.MessagePackObjects;

namespace ClashUp.Client.Networking
{
    public sealed class MatchHubReceiver : IMatchHubReceiver
    {
        private readonly IDebugLogger _log;

        public event Action<SnapshotPacket>? SnapshotReceived;
        public event Action<PlayerSummary>? PlayerJoined;
        public event Action<PlayerId, LeaveReason>? PlayerLeft;
        public event Action<MatchEvent>? MatchEventOccurred;
        public event Action<MatchResult>? MatchEnded;

        public MatchHubReceiver(IDebugLogger log)
        {
            _log = log;
        }

        public void OnSnapshot(SnapshotPacket snapshot) => SnapshotReceived?.Invoke(snapshot);

        public void OnPlayerJoined(PlayerSummary player)
        {
            _log.Log($"[Match] Player joined: {player.Id} ({player.DisplayName}) team={player.TeamId}");
            PlayerJoined?.Invoke(player);
        }

        public void OnPlayerLeft(PlayerId player, LeaveReason reason)
        {
            _log.Log($"[Match] Player left: {player} ({reason})");
            PlayerLeft?.Invoke(player, reason);
        }

        public void OnMatchEvent(MatchEvent matchEvent) => MatchEventOccurred?.Invoke(matchEvent);

        public void OnMatchEnded(MatchResult result)
        {
            _log.Log($"[Match] Ended. Winner team {result.WinningTeamId}");
            MatchEnded?.Invoke(result);
        }
    }
}
