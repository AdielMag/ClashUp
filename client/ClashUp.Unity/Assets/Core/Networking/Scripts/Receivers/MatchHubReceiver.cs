using System;

using ClashUp.Shared.Hubs;
using ClashUp.Shared.MessagePackObjects;

using UnityEngine;

namespace ClashUp.Client.Networking
{
    /// <summary>
    /// Default IMatchHubReceiver. Logs everything; gameplay code subscribes
    /// to the events for UI/prediction wiring.
    /// </summary>
    public sealed class MatchHubReceiver : IMatchHubReceiver
    {
        public event Action<SnapshotPacket>? SnapshotReceived;
        public event Action<PlayerSummary>? PlayerJoined;
        public event Action<PlayerId, LeaveReason>? PlayerLeft;
        public event Action<MatchEvent>? MatchEventOccurred;
        public event Action<MatchResult>? MatchEnded;

        public void OnSnapshot(SnapshotPacket snapshot) => SnapshotReceived?.Invoke(snapshot);

        public void OnPlayerJoined(PlayerSummary player)
        {
            Debug.Log($"[Match] Player joined: {player.Id} ({player.DisplayName}) team={player.TeamId}");
            PlayerJoined?.Invoke(player);
        }

        public void OnPlayerLeft(PlayerId player, LeaveReason reason)
        {
            Debug.Log($"[Match] Player left: {player} ({reason})");
            PlayerLeft?.Invoke(player, reason);
        }

        public void OnMatchEvent(MatchEvent matchEvent) => MatchEventOccurred?.Invoke(matchEvent);

        public void OnMatchEnded(MatchResult result)
        {
            Debug.Log($"[Match] Ended. Winner team {result.WinningTeamId}");
            MatchEnded?.Invoke(result);
        }
    }
}
