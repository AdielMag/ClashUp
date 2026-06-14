using Cysharp.Net.Http;

using Grpc.Net.Client;

namespace ClashUp.Client.Networking
{
    /// <summary>
    /// Builds a fresh GrpcChannel pointed at a per-match GS endpoint. Owned
    /// by the MatchLifetimeScope so the channel is disposed alongside the match.
    /// </summary>
    public sealed class GameServerChannelFactory
    {
        public GrpcChannel Create(string endpoint)
        {
            // On Android emulator, the host machine's localhost is reachable via 10.0.2.2.
            // The server advertises its public endpoint as localhost; rewrite it here.
            if (ClashUpEndpoints.ResolvedEnvironment == ServerEnvironment.Emulator)
                endpoint = endpoint.Replace("localhost", "10.0.2.2");

            return GrpcChannel.ForAddress(
                endpoint,
                new GrpcChannelOptions
                {
                    HttpHandler = new YetAnotherHttpHandler { Http2Only = true },
                    DisposeHttpClient = true,
                });
        }
    }
}
