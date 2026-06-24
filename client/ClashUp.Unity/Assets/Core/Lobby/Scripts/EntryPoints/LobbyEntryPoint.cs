using System.Threading;

using ClashUp.Client.Core;
using ClashUp.Client.CoreStarter;
using ClashUp.Client.Networking;
using ClashUp.Shared.Characters;
using ClashUp.Shared.MessagePackObjects;

using Cysharp.Threading.Tasks;

using VContainer.Unity;

namespace ClashUp.Client.Lobby
{
    public sealed class LobbyEntryPoint : IAsyncStartable
    {
        private static int _reconnectFailures;

        public static void ResetReconnectFailures() => _reconnectFailures = 0;

        private readonly GameFlowController _flow;
        private readonly MatchmakingClient _matchmaking;
        private readonly SelectedCharacterStore _selectedCharacter;
        private readonly SelectedGameModeStore _selectedGameMode;
        private readonly IDebugLogger _log;

        public LobbyEntryPoint(GameFlowController flow, MatchmakingClient matchmaking, SelectedCharacterStore selectedCharacter, SelectedGameModeStore selectedGameMode, IDebugLogger log)
        {
            _flow = flow;
            _matchmaking = matchmaking;
            _selectedCharacter = selectedCharacter;
            _selectedGameMode = selectedGameMode;
            _log = log;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            const int maxReconnectAttempts = 3;

            // Check if player has an active match to reconnect to
            if (_reconnectFailures < maxReconnectAttempts)
            {
                try
                {
                    var activeHandoff = await _matchmaking.CheckActiveMatchAsync(cancellation);
                    if (activeHandoff != null)
                    {
                        _log.Log($"[Lobby] Active match found: {activeHandoff.MatchId}. Reconnecting... (attempt {_reconnectFailures + 1}/{maxReconnectAttempts})");
                        _reconnectFailures++;
                        _flow.EnterMatchFromLobby(activeHandoff);
                        return;
                    }
                }
                catch (System.Exception ex)
                {
                    _log.LogWarning($"[Lobby] Active match check failed: {ex.Message}");
                }
            }
            else
            {
                _log.LogWarning($"[Lobby] Skipping reconnect — failed {_reconnectFailures} times. Staying in lobby.");
            }

            _reconnectFailures = 0;

            while (!cancellation.IsCancellationRequested)
            {
                var ui = LobbyUI.Create();

                // Player picks a game mode entry; the chosen modeId rides matchmaking.
                var modeSelected = new UniTaskCompletionSource<string>();
                ui.OnModeSelected += mode => modeSelected.TrySetResult(mode);

                var chosenMode = await modeSelected.Task.AttachExternalCancellation(cancellation);

                ui.Destroy();

                _selectedGameMode.Selected = string.IsNullOrEmpty(chosenMode) ? "default" : chosenMode;
                _log.Log($"[Lobby] Game mode selected: {_selectedGameMode.Selected}");

                // Generic character selection before matchmaking (used across all game modes for now).
                var selectUI = CharacterSelectUI.Create(CharactersConfig.Default, _selectedCharacter.Selected);
                var confirmed = new UniTaskCompletionSource<CharacterId>();
                bool goBack = false;
                selectUI.OnConfirmed += id => confirmed.TrySetResult(id);
                selectUI.OnBackClicked += () => { goBack = true; confirmed.TrySetResult(default); };

                var chosen = await confirmed.Task.AttachExternalCancellation(cancellation);
                selectUI.Destroy();

                if (goBack)
                    continue;

                _selectedCharacter.Selected = chosen;
                _log.Log($"[Lobby] Character selected: {chosen.Value}");

                _flow.EnterMatchmaking();
                return;
            }
        }
    }
}
