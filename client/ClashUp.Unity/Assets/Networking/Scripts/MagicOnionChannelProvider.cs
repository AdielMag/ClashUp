using System;
using Cysharp.Net.Http;
using Grpc.Net.Client;

namespace ClashUp.Client.Networking;

/// <summary>
/// Owns the lazily-created GrpcChannel to the Services tier. Built on
/// YetAnotherHttpHandler because gRPC C-core is discontinued — see
/// docs/rules/il2cpp-aot.md and the Unity install notes for MagicOnion.
/// </summary>
public sealed class MagicOnionChannelProvider : IDisposable
{
    private readonly ClashUpEndpoints _endpoints;
    private GrpcChannel? _servicesChannel;

    public MagicOnionChannelProvider(ClashUpEndpoints endpoints)
    {
        _endpoints = endpoints;
    }

    public GrpcChannel Services => _servicesChannel ??= GrpcChannel.ForAddress(
        _endpoints.ServicesAddress,
        new GrpcChannelOptions
        {
            HttpHandler = new YetAnotherHttpHandler { Http2Only = true },
            DisposeHttpClient = true,
        });

    public void Dispose()
    {
        _servicesChannel?.Dispose();
        _servicesChannel = null;
    }
}
