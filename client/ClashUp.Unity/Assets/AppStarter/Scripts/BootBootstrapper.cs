using System;
using System.Threading;
using ClashUp.Client.Core;
using ClashUp.Client.Networking;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

namespace ClashUp.Client.AppStarter;

/// <summary>
/// Entry point run by VContainer when the boot scene loads. Phase-1
/// flow: ensure a device ID exists in PlayerPrefs, then prove the
/// network path works by calling IPingHub on Services. Auth/login
/// land in a later step.
/// </summary>
public sealed class BootBootstrapper : IAsyncStartable, IDisposable
{
    private readonly IDeviceIdStore _deviceIdStore;
    private readonly PingHubClient _pingHub;

    public BootBootstrapper(IDeviceIdStore deviceIdStore, PingHubClient pingHub)
    {
        _deviceIdStore = deviceIdStore;
        _pingHub = pingHub;
    }

    public async UniTask StartAsync(CancellationToken cancellation)
    {
        var deviceId = _deviceIdStore.GetOrCreate();
        Debug.Log($"[Boot] device id = {deviceId}");

        try
        {
            var pong = await _pingHub.PingAsync(cancellation);
            var rttMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - pong.ClientStampMs;
            Debug.Log($"[Boot] Pong from Services v{pong.ServerVersion} rtt={rttMs}ms");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Boot] Ping failed: {ex.Message}");
        }
    }

    public void Dispose() => _pingHub.Dispose();
}
