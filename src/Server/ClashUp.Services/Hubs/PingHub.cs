using ClashUp.Shared.Hubs;
using ClashUp.Shared.MessagePackObjects;
using MagicOnion.Server.Hubs;

namespace ClashUp.Server.Services.Hubs;

/// <summary>
/// Smoke-test hub. Lives on Services during bring-up; will move to
/// (or be duplicated on) GameServer once the GS host comes online.
/// </summary>
public sealed class PingHub : StreamingHubBase<IPingHub, IPingHubReceiver>, IPingHub
{
    public Task<PongResult> PingAsync(PingRequest request)
    {
        var result = new PongResult
        {
            ClientStampMs = request.ClientStampMs,
            ServerStampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ServerVersion = ThisAssembly.InformationalVersion,
        };
        return Task.FromResult(result);
    }
}

internal static class ThisAssembly
{
    public const string InformationalVersion = "0.0.1";
}
