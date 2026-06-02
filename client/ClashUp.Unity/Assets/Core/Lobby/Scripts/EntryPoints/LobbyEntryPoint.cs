using System.Threading;

using ClashUp.Client.Core;
using ClashUp.Client.CoreStarter;
using ClashUp.Client.Networking;

using Cysharp.Threading.Tasks;

using VContainer.Unity;

namespace ClashUp.Client.Lobby
{
    public sealed class LobbyEntryPoint : IAsyncStartable
    {
        private readonly GameFlowController _flow;
        private readonly MatchmakingClient _matchmaking;
        private readonly IDebugLogger _log;

        public LobbyEntryPoint(GameFlowController flow, MatchmakingClient matchmaking, IDebugLogger log)
        {
            _flow = flow;
            _matchmaking = matchmaking;
            _log = log;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            // Check if player has an active match to reconnect to
            try
            {
                var activeHandoff = await _matchmaking.CheckActiveMatchAsync(cancellation);
                if (activeHandoff != null)
                {
                    _log.Log($"[Lobby] Active match found: {activeHandoff.MatchId}. Reconnecting...");
                    _flow.EnterMatchFromLobby(activeHandoff);
                    return;
                }
            }
            catch (System.Exception ex)
            {
                _log.LogWarning($"[Lobby] Active match check failed: {ex.Message}");
            }

            var ui = LobbyUI.Create();

            var playClicked = new UniTaskCompletionSource();
            ui.OnPlayClicked += () => playClicked.TrySetResult();

            await playClicked.Task.AttachExternalCancellation(cancellation);

            ui.Destroy();
            _flow.EnterMatchmaking();
        }
    }
}
