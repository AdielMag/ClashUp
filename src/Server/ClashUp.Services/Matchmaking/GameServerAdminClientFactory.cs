using System.Collections.Concurrent;
using ClashUp.Server.Common;
using ClashUp.Shared.Services;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using MagicOnion.Client;

namespace ClashUp.Server.Services.Matchmaking;

/// <summary>
/// Lazily-cached MagicOnion clients for IMatchAdminService, keyed by GS
/// endpoint. One channel per GS to keep connection pooling sane.
///
/// Every call carries an <c>x-client-version</c> header equal to this Services
/// backend's <see cref="ServerVersion.Current"/>. The gateway routes by that
/// header, so the match is provisioned on the GameServer backend of the SAME
/// version the players' clients will join — they were routed to this version's
/// Services backend by the gateway, so our version is their version. Without the
/// header the gateway would fall back to its default tag and the match could land
/// on a different backend than the one clients connect to.
/// </summary>
public sealed class GameServerAdminClientFactory : IDisposable
{
    private const string VersionHeader = "x-client-version";

    private readonly ConcurrentDictionary<string, (GrpcChannel Channel, IMatchAdminService Client)> _clients = new();

    public IMatchAdminService GetOrCreate(string endpoint) =>
        _clients.GetOrAdd(endpoint, e =>
        {
            var channel = GrpcChannel.ForAddress(e);
            var invoker = channel.CreateCallInvoker().Intercept(metadata =>
            {
                metadata.Add(VersionHeader, ServerVersion.Current);
                return metadata;
            });
            return (channel, MagicOnionClient.Create<IMatchAdminService>(invoker));
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
