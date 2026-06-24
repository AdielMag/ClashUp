using System;
using ClashUp.Shared.MessagePackObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer.Unity;

namespace ClashUp.Client.Gameplay
{
    public sealed class RespawnScreenController : IStartable, ITickable, IDisposable
    {
        private const float TickRate = 30f;

        private readonly AetherClientSimulation _sim;
        private readonly ClientPredictionWorld _world;
        private readonly MatchModeHolder _mode;
        private readonly JoystickInputProvider _joystick;
        private readonly AbilityInputProvider _abilityInput;

        private GameObject _overlay;
        private TMP_Text _titleLabel;
        private TMP_Text _countdownLabel;
        private bool _wasDead;
        private bool _localEliminated;

        public RespawnScreenController(
            AetherClientSimulation sim,
            ClientPredictionWorld world,
            MatchModeHolder mode,
            JoystickInputProvider joystick,
            AbilityInputProvider abilityInput)
        {
            _sim = sim;
            _world = world;
            _mode = mode;
            _joystick = joystick;
            _abilityInput = abilityInput;
        }

        public void Start()
        {
            _overlay = BuildOverlay();
            _overlay.SetActive(false);
            _world.SnapshotDecoded += OnSnapshot;
        }

        private void OnSnapshot(int tick, WorldStatePacket packet)
        {
            var localId = _sim.LocalId;
            foreach (var dto in packet.Players)
                if (dto.Id.Equals(localId)) { _localEliminated = dto.IsEliminated; return; }
        }

        public void Tick()
        {
            // No-respawn mode: a permanent ELIMINATED overlay once we're out.
            if (_mode.IsElimination)
            {
                UpdateOverlay(_localEliminated, "ELIMINATED", "Spectating…");
                return;
            }

            // Survival mode: respawn countdown driven by RespawnInTicks.
            var localId = _sim.LocalId.Value;
            if (localId == null || !_sim.Players.TryGetValue(localId, out var state))
            {
                UpdateOverlay(false, null, null);
                return;
            }

            bool isDead = state.RespawnInTicks > 0;
            string countdown = isDead ? $"Respawning in {state.RespawnInTicks / TickRate:F1}s" : null;
            UpdateOverlay(isDead, "YOU DIED", countdown);
        }

        private void UpdateOverlay(bool isDown, string title, string countdown)
        {
            _overlay.SetActive(isDown);

            if (isDown != _wasDead)
            {
                _joystick.SetVisible(!isDown);
                _abilityInput.SetVisible(!isDown);
                _wasDead = isDown;
            }

            if (isDown)
            {
                if (title != null) _titleLabel.text = title;
                if (countdown != null) _countdownLabel.text = countdown;
            }
        }

        public void Dispose()
        {
            _world.SnapshotDecoded -= OnSnapshot;
            if (_overlay != null)
                UnityEngine.Object.Destroy(_overlay);
        }

        private GameObject BuildOverlay()
        {
            var root = new GameObject("RespawnScreen");

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();

            var bg = new GameObject("Background");
            bg.transform.SetParent(root.transform, false);
            var bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.5f);

            var deadObj = new GameObject("YouDiedLabel");
            deadObj.transform.SetParent(root.transform, false);
            var deadRect = deadObj.AddComponent<RectTransform>();
            deadRect.anchorMin = new Vector2(0.5f, 0.5f);
            deadRect.anchorMax = new Vector2(0.5f, 0.5f);
            deadRect.pivot = new Vector2(0.5f, 0.5f);
            deadRect.anchoredPosition = new Vector2(0f, 60f);
            deadRect.sizeDelta = new Vector2(700f, 110f);
            var deadLabel = deadObj.AddComponent<TextMeshProUGUI>();
            deadLabel.text = "YOU DIED";
            deadLabel.fontSize = 80;
            deadLabel.fontStyle = FontStyles.Bold;
            deadLabel.alignment = TextAlignmentOptions.Center;
            deadLabel.color = new Color(0.9f, 0.1f, 0.1f, 1f);
            _titleLabel = deadLabel;

            var cntObj = new GameObject("CountdownLabel");
            cntObj.transform.SetParent(root.transform, false);
            var cntRect = cntObj.AddComponent<RectTransform>();
            cntRect.anchorMin = new Vector2(0.5f, 0.5f);
            cntRect.anchorMax = new Vector2(0.5f, 0.5f);
            cntRect.pivot = new Vector2(0.5f, 0.5f);
            cntRect.anchoredPosition = new Vector2(0f, -20f);
            cntRect.sizeDelta = new Vector2(600f, 60f);
            _countdownLabel = cntObj.AddComponent<TextMeshProUGUI>();
            _countdownLabel.text = "Respawning in 5.0s";
            _countdownLabel.fontSize = 36;
            _countdownLabel.alignment = TextAlignmentOptions.Center;
            _countdownLabel.color = Color.white;

            return root;
        }
    }
}
