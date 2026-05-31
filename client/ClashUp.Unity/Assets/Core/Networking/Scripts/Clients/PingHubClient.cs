using System;
using System.Threading;

using ClashUp.Client.Core;
using ClashUp.Shared.Hubs;
using ClashUp.Shared.MessagePackObjects;

using Cysharp.Threading.Tasks;

using MagicOnion.Client;

namespace ClashUp.Client.Networking
{
    public sealed class PingHubClient : IPingHubReceiver, IDisposable
    {
        private readonly MagicOnionChannelProvider _channels;
        private readonly IDebugLogger _log;
        private IPingHub? _hub;

        public PingHubClient(MagicOnionChannelProvider channels, IDebugLogger log)
        {
            _channels = channels;
            _log = log;
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
            _log.Log($"[PingHub] heartbeat {serverStampMs}");
        }

        public void Dispose()
        {
            _hub?.DisposeAsync().AsUniTask().Forget();
            _hub = null;
        }
    }
}
