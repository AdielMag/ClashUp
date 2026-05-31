using System;
using System.Threading;

using ClashUp.Client.Core;
using ClashUp.Client.Gameplay;
using ClashUp.Client.Networking;
using ClashUp.Shared.MessagePackObjects;

using Cysharp.Threading.Tasks;

using VContainer.Unity;

namespace ClashUp.Client.Match
{
    public sealed class MatchSessionRunner : IAsyncStartable, IDisposable
    {
        private readonly IDebugLogger _log;
        private readonly MatchSession _session;
        private readonly MatchHandoffHolder _handoff;
        private readonly ClientPredictionWorld _prediction;

        public MatchSessionRunner(IDebugLogger log, MatchSession session, MatchHandoffHolder handoff, ClientPredictionWorld prediction)
        {
            _log = log;
            _session = session;
            _handoff = handoff;
            _prediction = prediction;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            if (string.IsNullOrEmpty(_handoff.Value.MatchToken))
            {
                _log.LogError("[Match] No handoff present in scope; cannot start session.");
                return;
            }

            _session.Receiver.SnapshotReceived += OnSnapshot;

            try
            {
                var join = await _session.ConnectAndJoinAsync(_handoff.Value, cancellation);
                _log.Log($"[Match] Joined match {_handoff.Value.MatchId}. tickRate={join.TickRateHz}Hz");
            }
            catch (Exception ex)
            {
                _log.LogError($"[Match] Connect/Join failed: {ex.Message}");
            }
        }

        private void OnSnapshot(SnapshotPacket snapshot) => _prediction.ApplyServerSnapshot(snapshot);

        public void Dispose()
        {
            _session.Receiver.SnapshotReceived -= OnSnapshot;
            _session.LeaveAsync().Forget();
            _session.Dispose();
        }
    }

    public sealed class MatchHandoffHolder
    {
        public MatchHandoff Value { get; set; } = new();
    }
}
