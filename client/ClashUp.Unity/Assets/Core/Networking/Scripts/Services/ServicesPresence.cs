using System;
using System.Threading;

using ClashUp.Client.Core;
using ClashUp.Shared.Hubs;
using ClashUp.Shared.MessagePackObjects;

using Cysharp.Threading.Tasks;

using MagicOnion.Client;

namespace ClashUp.Client.Networking
{
    /// <summary>
    /// Holds a live streaming-hub connection to the Services tier while the player
    /// is in the lobby/matchmaking (i.e. NOT in a match). This connection IS the
    /// Services-tier CCU signal: one live session == one concurrent user in the
    /// menus. A periodic keepalive ping prevents the L4 load balancer from dropping
    /// the idle connection (the bug that made Services CCU read 0).
    ///
    /// The model is mutually exclusive with the GameServer tier: entering a match
    /// calls <see cref="Stop"/> (the player is now counted on the GameServer);
    /// returning to the lobby calls <see cref="Start"/>. Both are idempotent.
    /// </summary>
    public sealed class ServicesPresence : IPingHubReceiver, IDisposable
    {
        private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(3);

        private readonly MagicOnionChannelProvider _channels;
        private readonly IDebugLogger _log;

        private IPingHub? _hub;
        private CancellationTokenSource? _cts;

        public ServicesPresence(MagicOnionChannelProvider channels, IDebugLogger log)
        {
            _channels = channels;
            _log = log;
        }

        /// <summary>Begin (or resume) counting this player on the Services tier. Idempotent.</summary>
        public void Start()
        {
            if (_cts != null)
            {
                return; // already running
            }

            _cts = new CancellationTokenSource();
            RunAsync(_cts.Token).Forget();
            _log.Log("[Presence] started (Services CCU +1)");
        }

        /// <summary>Stop counting this player on the Services tier (entering a match). Idempotent.</summary>
        public void Stop()
        {
            if (_cts == null)
            {
                return;
            }

            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
            DropHub();
            _log.Log("[Presence] stopped (Services CCU -1)");
        }

        private async UniTaskVoid RunAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    _hub ??= await StreamingHubClient.ConnectAsync<IPingHub, IPingHubReceiver>(
                        _channels.Services, this, cancellationToken: ct);

                    await _hub.PingAsync(new PingRequest
                    {
                        ClientStampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Note = "presence",
                    });

                    await UniTask.Delay(KeepAliveInterval, cancellationToken: ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _log.LogWarning($"[Presence] keepalive failed, reconnecting: {ex.Message}");
                    DropHub();
                    try
                    {
                        await UniTask.Delay(ReconnectDelay, cancellationToken: ct);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            }
        }

        private void DropHub()
        {
            _hub?.DisposeAsync().AsUniTask().Forget();
            _hub = null;
        }

        public void OnHeartbeat(long serverStampMs)
        {
        }

        public void Dispose() => Stop();
    }
}
