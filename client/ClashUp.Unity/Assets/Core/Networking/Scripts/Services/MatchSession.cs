using System;
using System.Threading;

using ClashUp.Client.Core;
using ClashUp.Shared.Hubs;
using ClashUp.Shared.MessagePackObjects;

using Cysharp.Threading.Tasks;

using Grpc.Net.Client;

using MagicOnion.Client;

namespace ClashUp.Client.Networking
{
    public sealed class MatchSession : IDisposable
    {
        private readonly GameServerChannelFactory _channelFactory;
        private readonly MatchHubReceiver _receiver;
        private readonly ResolveMatchClient _resolve;
        private readonly IDebugLogger _log;

        private GrpcChannel? _channel;
        private IMatchHub? _hub;
        private MatchHandoff _handoff;
        private CancellationTokenSource? _reconnectCts;

        public MatchSession(GameServerChannelFactory channelFactory, MatchHubReceiver receiver, ResolveMatchClient resolve, IDebugLogger log)
        {
            _channelFactory = channelFactory;
            _receiver = receiver;
            _resolve = resolve;
            _log = log;
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

            _log.LogWarning("[Match] Hub disconnected; attempting sticky reconnect.");
            try
            {
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
                _log.Log("[Match] Sticky reconnect succeeded.");
                WatchForDisconnectAsync(ct).Forget();
            }
            catch (Exception ex)
            {
                _log.LogError($"[Match] Sticky reconnect failed: {ex.Message}");
            }
        }

        public UniTask SubmitInputAsync(InputCommand command) => Hub.SubmitInputAsync(command).AsUniTask();

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

            _hub?.DisposeAsync().AsUniTask().Forget();
            _hub = null;
            _channel?.Dispose();
            _channel = null;
        }
    }
}
