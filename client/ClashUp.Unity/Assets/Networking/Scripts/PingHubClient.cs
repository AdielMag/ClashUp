using System;
using System.Threading;

using ClashUp.Shared.Hubs;
using ClashUp.Shared.MessagePackObjects;

using Cysharp.Threading.Tasks;

using MagicOnion.Client;

using UnityEngine;

namespace ClashUp.Client.Networking.Networking.Scripts
{
    /// <summary>
    /// Connects to IPingHub on the Services host. Used for the L3 bring-up
    /// smoke test (BootScene → "Pong" within 200 ms).
    /// </summary>
    public sealed class PingHubClient : IPingHubReceiver, IDisposable
    {
        private readonly MagicOnionChannelProvider _channels;
        private IPingHub? _hub;

        public PingHubClient(MagicOnionChannelProvider channels)
        {
            _channels = channels;
        }

        public async UniTask<PongResult> PingAsync(CancellationToken cancellationToken)
        {
            _hub ??= await StreamingHubClient.ConnectAsync<IPingHub, IPingHubReceiver>(
                _channels.Services, this, cancellationToken: cancellationToken);

            var request = new PingRequest
            {
                ClientStampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Note = "boot",
            };
            return await _hub.PingAsync(request);
        }

        public void OnHeartbeat(long serverStampMs)
        {
            Debug.Log($"[PingHub] heartbeat {serverStampMs}");
        }

        public void Dispose()
        {
            _hub?.DisposeAsync().AsUniTask().Forget();
            _hub = null;
        }
    }
}
