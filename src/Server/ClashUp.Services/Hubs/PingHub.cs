using ClashUp.Server.Services.Ccu;
using ClashUp.Shared.Hubs;
using ClashUp.Shared.MessagePackObjects;
using MagicOnion.Server.Hubs;

namespace ClashUp.Server.Services.Hubs;

/// <summary>
/// Connection-keepalive / latency hub for clients attached to the Services tier.
/// Its connect/disconnect lifecycle is the CCU signal for the Services tier — a
/// live session counts as one concurrent connected user (see <see cref="ServicesCcuTracker"/>).
/// </summary>
public sealed class PingHub : StreamingHubBase<IPingHub, IPingHubReceiver>, IPingHub
{
    private readonly ServicesCcuTracker _ccu;

    public PingHub(ServicesCcuTracker ccu)
    {
        _ccu = ccu;
    }

    public Task<PongResult> PingAsync(PingRequest request)
    {
        var result = new PongResult
        {
            ClientStampMs = request.ClientStampMs,
            ServerStampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ServerVersion = ClashUp.Server.Common.ServerVersion.Current,
        };
        return Task.FromResult(result);
    }

    protected override ValueTask OnConnecting()
    {
        _ccu.Connected(Context.ContextId.ToString());
        return default;
    }

    protected override ValueTask OnDisconnected()
    {
        _ccu.Disconnected(Context.ContextId.ToString());
        return default;
    }
}
