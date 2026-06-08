using System.Threading;

using ClashUp.Client.Core;
using ClashUp.Client.Networking;
using ClashUp.Client.UI;
using ClashUp.Shared.MessagePackObjects;

using Cysharp.Threading.Tasks;

using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace ClashUp.Client.CoreStarter
{
    public sealed class GameFlowController : IAsyncStartable
    {
        private readonly ISceneLoader _sceneLoader;
        private readonly IDebugLogger _log;
        private readonly AuthClient _auth;
        private readonly IDeviceIdStore _deviceIdStore;
        private readonly LifetimeScope _scope;

        private SceneHandle _lobbyHandle;
        private SceneHandle _matchmakingHandle;
        private SceneHandle _matchHandle;

        public MatchHandoff PendingHandoff { get; set; }

        public GameFlowController(
            ISceneLoader sceneLoader,
            IDebugLogger log,
            AuthClient auth,
            IDeviceIdStore deviceIdStore,
            LifetimeScope scope)
        {
            _sceneLoader = sceneLoader;
            _log = log;
            _auth = auth;
            _deviceIdStore = deviceIdStore;
            _scope = scope;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            var loadingScreen = Object.FindAnyObjectByType<LoadingScreenPresenter>();

            // Auth
            loadingScreen.SetStepText("Authenticating...");
            var deviceId = _deviceIdStore.GetOrCreate();
            await _auth.LoginWithDeviceIdAsync(deviceId, cancellation);
            _log.Log("[GameFlow] Authenticated");
            loadingScreen.SetProgress(0.85f);

            // Load Lobby
            loadingScreen.SetStepText("Loading lobby...");
            using (LifetimeScope.EnqueueParent(_scope))
            {
                _lobbyHandle = await _sceneLoader.LoadAdditiveAsync("Lobby", ct: cancellation);
            }
            SceneManager.SetActiveScene(_lobbyHandle.Scene);
            loadingScreen.SetProgress(1f);

            await loadingScreen.WaitForProgressComplete(cancellation);
            await loadingScreen.HideAsync(cancellation);
        }

        public void EnterMatchmaking()
        {
            EnterMatchmakingAsync().Forget();
        }

        private async UniTaskVoid EnterMatchmakingAsync()
        {
            var loadingScreen = Object.FindAnyObjectByType<LoadingScreenPresenter>();
            await loadingScreen.ShowAsync();

            loadingScreen.SetStepText("Loading matchmaking...");
            await _sceneLoader.UnloadAsync(_lobbyHandle);

            using (LifetimeScope.EnqueueParent(_scope))
            {
                _matchmakingHandle = await _sceneLoader.LoadAdditiveAsync("Matchmaking");
            }
            SceneManager.SetActiveScene(_matchmakingHandle.Scene);

            await loadingScreen.HideAsync();
        }

        public void ReturnToLobbyFromMatchmaking()
        {
            ReturnToLobbyFromMatchmakingAsync().Forget();
        }

        private async UniTaskVoid ReturnToLobbyFromMatchmakingAsync()
        {
            var loadingScreen = Object.FindAnyObjectByType<LoadingScreenPresenter>();
            await loadingScreen.ShowAsync();

            loadingScreen.SetStepText("Returning to lobby...");
            await _sceneLoader.UnloadAsync(_matchmakingHandle);

            using (LifetimeScope.EnqueueParent(_scope))
            {
                _lobbyHandle = await _sceneLoader.LoadAdditiveAsync("Lobby");
            }
            SceneManager.SetActiveScene(_lobbyHandle.Scene);

            await loadingScreen.HideAsync();
        }

        public void EnterMatchFromLobby(MatchHandoff handoff)
        {
            EnterMatchFromLobbyAsync(handoff).Forget();
        }

        private async UniTaskVoid EnterMatchFromLobbyAsync(MatchHandoff handoff)
        {
            var loadingScreen = Object.FindAnyObjectByType<LoadingScreenPresenter>();
            await loadingScreen.ShowAsync();

            loadingScreen.SetStepText("Reconnecting to match...");
            await _sceneLoader.UnloadAsync(_lobbyHandle);

            PendingHandoff = handoff;
            using (LifetimeScope.EnqueueParent(_scope))
            {
                _matchHandle = await _sceneLoader.LoadAdditiveAsync("Match");
            }
            SceneManager.SetActiveScene(_matchHandle.Scene);

            await loadingScreen.HideAsync();
        }

        public void EnterMatchFromMatchmaking(MatchHandoff handoff)
        {
            EnterMatchFromMatchmakingAsync(handoff).Forget();
        }

        private async UniTaskVoid EnterMatchFromMatchmakingAsync(MatchHandoff handoff)
        {
            var loadingScreen = Object.FindAnyObjectByType<LoadingScreenPresenter>();
            await loadingScreen.ShowAsync();

            loadingScreen.SetStepText("Entering match...");
            await _sceneLoader.UnloadAsync(_matchmakingHandle);

            PendingHandoff = handoff;
            using (LifetimeScope.EnqueueParent(_scope))
            {
                _matchHandle = await _sceneLoader.LoadAdditiveAsync("Match");
            }
            SceneManager.SetActiveScene(_matchHandle.Scene);

            await loadingScreen.HideAsync();
        }

        public async UniTaskVoid ReturnToLobbyAsync()
        {
            var loadingScreen = Object.FindAnyObjectByType<LoadingScreenPresenter>();
            await loadingScreen.ShowAsync();

            loadingScreen.SetStepText("Returning to lobby...");
            await _sceneLoader.UnloadAsync(_matchHandle);

            using (LifetimeScope.EnqueueParent(_scope))
            {
                _lobbyHandle = await _sceneLoader.LoadAdditiveAsync("Lobby");
            }
            SceneManager.SetActiveScene(_lobbyHandle.Scene);

            await loadingScreen.HideAsync();
        }
    }
}
