using System;
using System.Threading;
using ClashUp.Client.Core;
using ClashUp.Client.Networking;
using ClashUp.Client.UI;

using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

using Object = UnityEngine.Object;

namespace ClashUp.Client.AppStarter
{
    public sealed class BootBootstrapper : IAsyncStartable, IDisposable
    {
        private readonly IDebugLogger _log;
        private readonly IDeviceIdStore _deviceIdStore;
        private readonly PingHubClient _pingHub;
        private readonly ISceneLoader _sceneLoader;
        private readonly ClashUpEndpoints _endpoints;
        private readonly EnvironmentConfig _environmentConfig;

        public BootBootstrapper(
            IDebugLogger log,
            IDeviceIdStore deviceIdStore,
            PingHubClient pingHub,
            ISceneLoader sceneLoader,
            ClashUpEndpoints endpoints,
            EnvironmentConfig environmentConfig)
        {
            _log = log;
            _deviceIdStore = deviceIdStore;
            _pingHub = pingHub;
            _sceneLoader = sceneLoader;
            _endpoints = endpoints;
            _environmentConfig = environmentConfig;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            // 1. Load persistent UI scene and get loading screen
            await _sceneLoader.LoadAdditiveAsync("PersistentUI", ct: cancellation);
            var loadingScreen = Object.FindAnyObjectByType<LoadingScreenPresenter>();

            await loadingScreen.ShowAsync(cancellation);
            loadingScreen.SetProgress(0.1f);

            // 2. Environment picker (dev only)
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            var selectedEnv = await EnvironmentPickerUI.ShowAndWaitAsync(_environmentConfig);
            _environmentConfig.SetCurrent(selectedEnv);
            _endpoints.ServicesAddress = _environmentConfig.GetServicesUrl();
            _log.Log($"[Boot] Environment: {selectedEnv} → {_endpoints.ServicesAddress}");
#endif
            ClashUpEndpoints.ResolvedServicesAddress = _endpoints.ServicesAddress;
            loadingScreen.SetProgress(0.2f);

            // 3. Identity
            loadingScreen.SetStepText("Preparing identity...");
            var deviceId = _deviceIdStore.GetOrCreate();
            loadingScreen.SetUserId(deviceId);
            _log.Log($"[Boot] device id = {deviceId}");
            loadingScreen.SetProgress(0.4f);

            // 4. Ping server (retry until connected)
            loadingScreen.SetStepText("Connecting to server...");
            while (!cancellation.IsCancellationRequested)
            {
                try
                {
                    var pong = await _pingHub.PingAsync(cancellation);
                    var rttMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - pong.ClientStampMs;
                    _log.Log($"[Boot] Pong from Services v{pong.ServerVersion} rtt={rttMs}ms");
                    break;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _log.LogWarning($"[Boot] Ping failed, retrying in 3s: {ex.Message}");
                    loadingScreen.SetStepText("Connection failed. Retrying...");
                    await UniTask.Delay(3000, cancellationToken: cancellation);
                }
            }
            loadingScreen.SetProgress(0.7f);

            // 5. Load CoreStarter scene (standalone — not a child of AppStarter)
            loadingScreen.SetStepText("Loading session...");
            await _sceneLoader.LoadAdditiveAsync("CoreStarter", ct: cancellation);
            loadingScreen.SetProgress(0.8f);

            // GameFlowController in CoreStarter takes over from here (auth, lobby load, hide loading).
        }

        public void Dispose() => _pingHub.Dispose();
    }
}
