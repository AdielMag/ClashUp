using System;
using System.Threading;

using ClashUp.Client.Core;
using ClashUp.Client.CoreStarter;
using ClashUp.Client.Gameplay;
using ClashUp.Client.Networking;
using ClashUp.Shared.MessagePackObjects;

using Cysharp.Threading.Tasks;

using UnityEngine;
using VContainer.Unity;

namespace ClashUp.Client.Match
{
    public sealed class MatchSessionRunner : IAsyncStartable, IDisposable
    {
        private readonly IDebugLogger _log;
        private readonly MatchSession _session;
        private readonly MatchHandoffHolder _handoff;
        private readonly ClientPredictionWorld _prediction;
        private readonly GameFlowController _flow;

        private MatchUI _matchUI;
        private int _durationSeconds;
        private double _serverElapsedAtJoin;
        private DateTimeOffset _joinWallClock;
        private int _playerCount;
        private bool _matchEnded;
        private CancellationTokenSource _timerCts;

        public MatchSessionRunner(
            IDebugLogger log,
            MatchSession session,
            MatchHandoffHolder handoff,
            ClientPredictionWorld prediction,
            GameFlowController flow)
        {
            _log = log;
            _session = session;
            _handoff = handoff;
            _prediction = prediction;
            _flow = flow;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            if (string.IsNullOrEmpty(_handoff.Value.MatchToken))
            {
                _log.LogError("[Match] No handoff present in scope; cannot start session.");
                return;
            }

            _matchUI = MatchUI.Create();
            _matchUI.SetStatus("Connecting...");
            _matchUI.OnBackToLobbyClicked += OnBackToLobby;

            _session.Receiver.SnapshotReceived += OnSnapshot;
            _session.Receiver.PlayerJoined += OnPlayerJoined;
            _session.Receiver.PlayerLeft += OnPlayerLeft;
            _session.Receiver.MatchEnded += OnMatchEnded;

            try
            {
                var join = await _session.ConnectAndJoinAsync(_handoff.Value, cancellation);
                _durationSeconds = join.DurationSeconds;
                _serverElapsedAtJoin = join.ElapsedSeconds;
                _joinWallClock = DateTimeOffset.UtcNow;
                _playerCount = join.Players.Count;
                _matchEnded = false;

                _matchUI.SetStatus("Match in progress");
                _matchUI.SetPlayerCount(_playerCount);

                _log.Log($"[Match] Joined match {_handoff.Value.MatchId}. tickRate={join.TickRateHz}Hz duration={_durationSeconds}s");

                _timerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                RunCountdownAsync(_timerCts.Token).Forget();
            }
            catch (Exception ex)
            {
                _log.LogError($"[Match] Connect/Join failed: {ex.Message}");
                _flow.ReturnToLobbyAsync().Forget();
            }
        }

        private async UniTaskVoid RunCountdownAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && !_matchEnded)
            {
                var localElapsed = (DateTimeOffset.UtcNow - _joinWallClock).TotalSeconds;
                var totalElapsed = _serverElapsedAtJoin + localElapsed;
                var remaining = Math.Max(0.0, _durationSeconds - totalElapsed);
                _matchUI.SetTimeRemaining((float)remaining);

                if (remaining <= 0.0)
                    break;

                await UniTask.Yield(ct);
            }
        }

        private void OnSnapshot(SnapshotPacket snapshot) => _prediction.ApplyServerSnapshot(snapshot);

        private void OnPlayerJoined(PlayerSummary player)
        {
            _playerCount++;
            _matchUI?.SetPlayerCount(_playerCount);
        }

        private void OnPlayerLeft(PlayerId player, LeaveReason reason)
        {
            _playerCount = Math.Max(0, _playerCount - 1);
            _matchUI?.SetPlayerCount(_playerCount);
        }

        private void OnMatchEnded(MatchResult result)
        {
            if (_matchEnded) return;
            _matchEnded = true;
            _timerCts?.Cancel();
            _matchUI?.ShowMatchEnded(result);
            _log.Log($"[Match] Match ended. Winner team={result.WinningTeamId}");
        }

        private void OnBackToLobby()
        {
            _flow.ReturnToLobbyAsync().Forget();
        }

        public void Dispose()
        {
            _timerCts?.Cancel();
            _timerCts?.Dispose();

            _session.Receiver.SnapshotReceived -= OnSnapshot;
            _session.Receiver.PlayerJoined -= OnPlayerJoined;
            _session.Receiver.PlayerLeft -= OnPlayerLeft;
            _session.Receiver.MatchEnded -= OnMatchEnded;

            _matchUI?.Destroy();
            _session.LeaveAsync().Forget();
            _session.Dispose();
        }
    }

    public sealed class MatchHandoffHolder
    {
        public MatchHandoff Value { get; set; } = new();
    }
}
