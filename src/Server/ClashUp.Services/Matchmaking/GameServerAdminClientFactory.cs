using System.Collections.Concurrent;
using ClashUp.Shared.Services;
using Grpc.Net.Client;
using MagicOnion.Client;

namespace ClashUp.Server.Services.Matchmaking;

/// <summary>
/// Lazily-cached MagicOnion clients for IMatchAdminService, keyed by GS
/// endpoint. One channel per GS to keep connection pooling sane.
/// </summary>
public sealed class GameServerAdminClientFactory : IDisposable
{
    private readonly ConcurrentDictionary<string, (GrpcChannel Channel, IMatchAdminService Client)> _clients = new();

    public IMatchAdminService GetOrCreate(string endpoint) =>
        _clients.GetOrAdd(endpoint, e =>
        {
            var channel = GrpcChannel.ForAddress(e);
            return (channel, MagicOnionClient.Create<IMatchAdminService>(channel));
        }).Client;

    public void Dispose()
    {
        foreach (var (_, value) in _clients)
        {
            value.Channel.Dispose();
        }
        _clients.Clear();
    }
}
