using System;
using System.Threading;

using ClashUp.Shared.Hubs;
using ClashUp.Shared.MessagePackObjects;

using Cysharp.Threading.Tasks;

using UnityEngine;

namespace ClashUp.Client.Networking.Networking.Scripts
{
    /// <summary>
    /// Owns the per-match GrpcChannel + IMatchHub connection. Watches for
    /// hub disconnects and re-runs Connect+Join with the same MatchToken
    /// (or a fresh handoff via ResolveMatchAsync) so the player snaps back
    /// to the same GS — see docs/rules/jwt-auth.md (sticky claim).
    /// </summary>
    public sealed class MatchSession : IDisposable
    {
        private readonly GameServerChannelFactory _channelFactory;
        private readonly MatchHubReceiver _receiver;
        private readonly ResolveMatchClient _resolve;

        private GrpcChannel? _channel;
        private IMatchHub? _hub;
        private MatchHandoff _handoff;
        private CancellationTokenSource? _reconnectCts;

        public MatchSession(GameServerChannelFactory channelFactory, MatchHubReceiver receiver, ResolveMatchClient resolve)
        {
            _channelFactory = channelFactory;
            _receiver = receiver;
            _resolve = resolve;
        }

        public IMatchHub Hub => _hub ?? throw new InvalidOperationException("MatchSession is not connected.");

        public MatchHubReceiver Receiver => _receiver;

        public async UniTask<JoinResult> ConnectAndJoinAsync(MatchHandoff handoff, CancellationToken ct)
        {
            _handoff = handoff;
            var result = await ConnectInternalAsync(ct);

            _reconnectCts = new CancellationTokenSource();
            WatchForDisconnectAsync(_reconnectCts.Token).Forget();

            return result;
        }

        private async UniTask<JoinResult> ConnectInternalAsync(CancellationToken ct)
        {
            _channel?.Dispose();
            _channel = _channelFactory.Create(_handoff.GsEndpoint);

            _hub = await StreamingHubClient.ConnectAsync<IMatchHub, IMatchHubReceiver>(
                _channel, _receiver, cancellationToken: ct);

            return await _hub.JoinAsync(new MatchJoinRequest
            {
                MatchId = _handoff.MatchId,
                MatchToken = _handoff.MatchToken,
            });
        }

        private async UniTaskVoid WatchForDisconnectAsync(CancellationToken ct)
        {
            if (_hub is null)
            {
                return;
            }

            try
            {
                await _hub.WaitForDisconnect();
            }
            catch (Exception)
            {
                // Normal teardown path.
            }

            if (ct.IsCancellationRequested)
            {
                return;
            }

            Debug.LogWarning("[Match] Hub disconnected; attempting sticky reconnect.");
            try
            {
                // First try the same handoff — the GS may have stayed up. If
                // the GS is gone or the match's location changed, fall back to
                // ResolveMatchAsync.
                try
                {
                    await ConnectInternalAsync(ct);
                }
                catch (Exception)
                {
                    var fresh = await _resolve.ResolveAsync(_handoff.MatchId, ct);
                    _handoff = new MatchHandoff
                    {
                        MatchId = fresh.MatchId,
                        GsEndpoint = fresh.GsEndpoint,
                        MatchToken = string.IsNullOrEmpty(fresh.MatchToken) ? _handoff.MatchToken : fresh.MatchToken,
                        MatchTokenExpiresAtMs = fresh.MatchTokenExpiresAtMs,
                    };
                    await ConnectInternalAsync(ct);
                }
                Debug.Log("[Match] Sticky reconnect succeeded.");
                WatchForDisconnectAsync(ct).Forget();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Match] Sticky reconnect failed: {ex.Message}");
            }
        }

        public async UniTask LeaveAsync()
        {
            if (_hub is null)
            {
                return;
            }
            try
            {
                await _hub.LeaveAsync();
            }
            catch (Exception)
            {
                // Best-effort.
            }
        }

        public void Dispose()
        {
            _reconnectCts?.Cancel();
            _reconnectCts?.Dispose();
            _reconnectCts = null;

            _hub?.DisposeAsync().Forget();
            _hub = null;
            _channel?.Dispose();
            _channel = null;
        }
    }
}
