using ClashUp.Server.GameServer.Match;
using ClashUp.Shared.MessagePackObjects;
using ClashUp.Shared.Services;
using Grpc.Net.Client;
using MagicOnion.Client;
using Microsoft.Extensions.Options;

namespace ClashUp.Server.GameServer.Registration;

public sealed class ServicesRegistryClient : IServicesRegistryClient, IDisposable
{
    private readonly GrpcChannel _channel;
    private readonly IGameServerRegistry _client;

    public ServicesRegistryClient(IOptions<GameServerOptions> options)
    {
        _channel = GrpcChannel.ForAddress(options.Value.ServicesEndpoint);
        _client = MagicOnionClient.Create<IGameServerRegistry>(_channel);
    }

    public async Task<GsToken> RegisterAsync(GsRegistration registration, CancellationToken cancellationToken) =>
        await _client.RegisterAsync(registration);

    public async Task HeartbeatAsync(GsHeartbeat heartbeat, CancellationToken cancellationToken) =>
        await _client.HeartbeatAsync(heartbeat);

    public async Task MarkDrainingAsync(string instanceId, CancellationToken cancellationToken) =>
        await _client.MarkDrainingAsync(instanceId);

    public async Task ReportMatchEndedAsync(GsMatchEnded notice, CancellationToken cancellationToken) =>
        await _client.ReportMatchEndedAsync(notice);

    public void Dispose() => _channel.Dispose();
}
