using System;
using System.Threading;

using ClashUp.Client.Core;
using ClashUp.Client.CoreStarter;
using ClashUp.Client.Gameplay;
using ClashUp.Client.Lobby;
using ClashUp.Client.Networking;
using ClashUp.Shared.Maps;
using ClashUp.Shared.MessagePackObjects;

using Cysharp.Threading.Tasks;

using UnityEngine;
using VContainer.Unity;

namespace ClashUp.Client.Match
{
    public sealed class MatchSessionRunner : IAsyncStartable, IDisposable
    {
        private readonly IDebugLogger _log;
        private readonly MatchSession _session;
        private readonly MatchHandoffHolder _handoff;
        private readonly ClientPredictionWorld _prediction;
        private readonly GameFlowController _flow;
        private readonly MatchInputGate _inputGate;
        private readonly IClientSimulation _sim;
        private readonly PlayerViewSystem _viewSystem;
        private readonly LocalInputPublisher _inputPublisher;
        private readonly MapRegistry _mapRegistry;
        private readonly MatchCharactersHolder _characters;
        private readonly MatchAbilitiesHolder _abilities;
        private readonly MatchModeHolder _mode;
        private readonly JoystickInputProvider _joystickProvider;
        private readonly AbilityInputProvider _abilityProvider;

        private MatchUI _matchUI;
        private GameObject _mapVisualInstance;
        private int _localTeamId;
        private int _durationSeconds;
        private double _serverElapsedAtJoin;
        private DateTimeOffset _joinWallClock;
        private int _playerCount;
        private bool _matchEnded;
        private bool _joystickTouching;
        private bool _abilityTouching;
        private CancellationTokenSource _timerCts;

        public MatchSessionRunner(
            IDebugLogger log,
            MatchSession session,
            MatchHandoffHolder handoff,
            ClientPredictionWorld prediction,
            GameFlowController flow,
            MatchInputGate inputGate,
            IClientSimulation sim,
            PlayerViewSystem viewSystem,
            LocalInputPublisher inputPublisher,
            MapRegistry mapRegistry,
            MatchCharactersHolder characters,
            MatchAbilitiesHolder abilities,
            MatchModeHolder mode,
            JoystickInputProvider joystickProvider,
            AbilityInputProvider abilityProvider)
        {
            _log = log;
            _session = session;
            _handoff = handoff;
            _prediction = prediction;
            _flow = flow;
            _inputGate = inputGate;
            _sim = sim;
            _viewSystem = viewSystem;
            _inputPublisher = inputPublisher;
            _mapRegistry = mapRegistry;
            _characters = characters;
            _abilities = abilities;
            _mode = mode;
            _joystickProvider = joystickProvider;
            _abilityProvider = abilityProvider;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            if (string.IsNullOrEmpty(_handoff.Value.MatchToken))
            {
                _log.LogError("[Match] No handoff present in scope; cannot start session.");
                return;
            }

            _matchUI = MatchUI.Create();
            _matchUI.SetStatus("Connecting...");
            _matchUI.OnBackToLobbyClicked += OnBackToLobby;

            _joystickProvider.OnTouching += OnJoystickTouching;
            _abilityProvider.OnTouching  += OnAbilityTouching;

            _session.Receiver.SnapshotReceived += OnSnapshot;
            _session.Receiver.PlayerJoined += OnPlayerJoined;
            _session.Receiver.PlayerLeft += OnPlayerLeft;
            _session.Receiver.MatchEnded += OnMatchEnded;

            try
            {
                var join = await _session.ConnectAndJoinAsync(_handoff.Value, cancellation);
                _characters.Initialize(join.Characters);
                _abilities.Initialize(join.Abilities);
                _mode.Initialize(join.ObjectiveType);
                _localTeamId = 0;
                foreach (var p in join.Players)
                    if (p.Id.Equals(join.You)) { _localTeamId = p.TeamId; break; }
                _durationSeconds = join.DurationSeconds;
                _serverElapsedAtJoin = join.ElapsedSeconds;
                _joinWallClock = DateTimeOffset.UtcNow;
                _playerCount = join.Players.Count;
                _matchEnded = false;

                _sim.SetLocalPlayer(join.You);
                _prediction.Configure(join.TickRateHz);
                _prediction.SetRandomSeed(join.RandomSeed);

                LoadMap(join.MapId);

                foreach (var player in join.Players)
                    _viewSystem.RegisterPlayer(player);

                _inputPublisher.Configure(join.CurrentTick, join.TickRateHz);

                LobbyEntryPoint.ResetReconnectFailures();
                _matchUI.SetStatus("Match in progress");
                _matchUI.SetPlayerCount(_playerCount);
                _inputGate.Enable();

                _log.Log($"[Match] Joined match {_handoff.Value.MatchId}. tickRate={join.TickRateHz}Hz duration={_durationSeconds}s");

                _timerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                RunCountdownAsync(_timerCts.Token).Forget();
            }
            catch (Exception ex)
            {
                _log.LogError($"[Match] Connect/Join failed: {ex.Message}");
                _flow.ReturnToLobbyAsync().Forget();
            }
        }

        private async UniTaskVoid RunCountdownAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && !_matchEnded)
            {
                var localElapsed = (DateTimeOffset.UtcNow - _joinWallClock).TotalSeconds;
                var totalElapsed = _serverElapsedAtJoin + localElapsed;
                var remaining = Math.Max(0.0, _durationSeconds - totalElapsed);
                _matchUI.SetTimeRemaining((float)remaining);

                if (remaining <= 0.0)
                    break;

                await UniTask.Yield(ct);
            }
        }

        private void OnSnapshot(SnapshotPacket snapshot) => _prediction.EnqueueSnapshot(snapshot);

        private void OnPlayerJoined(PlayerSummary player)
        {
            _playerCount++;
            _matchUI?.SetPlayerCount(_playerCount);
            _viewSystem.RegisterPlayer(player);
        }

        private void OnPlayerLeft(PlayerId player, LeaveReason reason)
        {
            _playerCount = Math.Max(0, _playerCount - 1);
            _matchUI?.SetPlayerCount(_playerCount);
            _viewSystem.UnregisterPlayer(player);
        }

        private void OnMatchEnded(MatchResult result)
        {
            if (_matchEnded) return;
            _matchEnded = true;
            _inputGate.Disable();
            _timerCts?.Cancel();
            _matchUI?.ShowMatchEnded(result, _localTeamId);
            _log.Log($"[Match] Match ended. Winner team={result.WinningTeamId}");
        }

        private void LoadMap(string mapId)
        {
            var mapDef = _mapRegistry.Get(mapId);
            if (mapDef == null)
            {
                _log.LogError($"[Match] Map '{mapId}' not found in registry");
                return;
            }

            MapData mapData = null;
            if (mapDef.BakedMapJson != null)
            {
                mapData = MapDataDeserializer.Deserialize(mapDef.BakedMapJson.text);
                if (mapData != null)
                    _sim.LoadMap(mapData);
            }

            // Authored prefab wins; otherwise build a readable grid-floor arena straight from the map data.
            if (mapDef.VisualPrefab != null)
                _mapVisualInstance = UnityEngine.Object.Instantiate(mapDef.VisualPrefab);
            else if (mapData != null)
                _mapVisualInstance = MapVisualBuilder.Build(mapData);
        }

        private void OnJoystickTouching(bool active)
        {
            _joystickTouching = active;
            UpdateInputVisibility();
        }

        private void OnAbilityTouching(bool active)
        {
            _abilityTouching = active;
            UpdateInputVisibility();
        }

        private void UpdateInputVisibility()
        {
            _abilityProvider.SetVisible(!_joystickTouching);
            _joystickProvider.SetVisible(!_abilityTouching);
        }

        private void OnBackToLobby()
        {
            LeaveAndReturnAsync().Forget();
        }

        private async UniTaskVoid LeaveAndReturnAsync()
        {
            // Send the forfeit and WAIT for it before tearing down. Dispose's fire-and-forget
            // LeaveAsync races with the channel teardown and never reaches the server, so the
            // server would only see a (reconnectable) disconnect and the lobby would reconnect.
            await _session.LeaveAsync();
            _flow.ReturnToLobbyAsync().Forget();
        }

        public void Dispose()
        {
            _timerCts?.Cancel();
            _timerCts?.Dispose();

            _joystickProvider.OnTouching -= OnJoystickTouching;
            _abilityProvider.OnTouching  -= OnAbilityTouching;

            _session.Receiver.SnapshotReceived -= OnSnapshot;
            _session.Receiver.PlayerJoined -= OnPlayerJoined;
            _session.Receiver.PlayerLeft -= OnPlayerLeft;
            _session.Receiver.MatchEnded -= OnMatchEnded;

            if (_mapVisualInstance != null)
                UnityEngine.Object.Destroy(_mapVisualInstance);

            _matchUI?.Destroy();
            _session.LeaveAsync().Forget();
            _session.Dispose();
        }
    }

    public sealed class MatchHandoffHolder
    {
        public MatchHandoff Value { get; set; } = new();
    }
}
