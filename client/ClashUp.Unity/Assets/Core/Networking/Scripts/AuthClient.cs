using System.Threading;

using ClashUp.Shared.MessagePackObjects;
using ClashUp.Shared.Services;

using Cysharp.Threading.Tasks;

using MagicOnion.Client;

namespace ClashUp.Client.Networking
{
    public sealed class AuthClient
    {
        private readonly MagicOnionChannelProvider _channels;
        private readonly SessionTokenStore _tokens;

        public AuthClient(MagicOnionChannelProvider channels, SessionTokenStore tokens)
        {
            _channels = channels;
            _tokens = tokens;
        }

        public async UniTask<LoginResult> LoginWithDeviceIdAsync(string deviceId, CancellationToken ct)
        {
            var client = MagicOnionClient.Create<IAuthService>(_channels.Services);
            var result = await client.LoginWithDeviceIdAsync(new LoginRequest { DeviceId = deviceId });
            _tokens.UpdateEndUser(result.Jwt, result.ExpiresAtMs);
            return result;
        }
    }
}
