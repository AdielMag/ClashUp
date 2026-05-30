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
        public GrpcChannel Create(string endpoint) =>
            GrpcChannel.ForAddress(
                endpoint,
                new GrpcChannelOptions
                {
                    HttpHandler = new YetAnotherHttpHandler { Http2Only = true },
                    DisposeHttpClient = true,
                });
    }
}
