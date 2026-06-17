using System;

using Cysharp.Net.Http;

using Grpc.Net.Client;

using UnityEngine;

namespace ClashUp.Client.Networking
{
    /// <summary>
    /// Owns the lazily-created GrpcChannel to the Services tier. Built on
    /// YetAnotherHttpHandler because gRPC C-core is discontinued — see
    /// docs/rules/il2cpp-aot.md and the Unity install notes for MagicOnion.
    /// </summary>
    public sealed class MagicOnionChannelProvider : IDisposable
    {
        private readonly ClashUpEndpoints _endpoints;
        private readonly string _clientVersion;
        private GrpcChannel? _servicesChannel;

        public MagicOnionChannelProvider(ClashUpEndpoints endpoints)
        {
            _endpoints = endpoints;
            // Captured on the main thread (DI construction) for use on the gRPC handler's thread.
            _clientVersion = Application.version;
        }

        public GrpcChannel Services => _servicesChannel ??= GrpcChannel.ForAddress(
            _endpoints.ServicesAddress,
            new GrpcChannelOptions
            {
                HttpHandler = new ClientVersionHttpHandler(
                    new YetAnotherHttpHandler { Http2Only = true }, _clientVersion),
                DisposeHttpClient = true,
            });

        public void Dispose()
        {
            _servicesChannel?.Dispose();
            _servicesChannel = null;
        }
    }
}
