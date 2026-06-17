using System;
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
        private readonly JoystickInputProvider _joystick;
        private readonly AbilityInputProvider _abilityInput;

        private GameObject _overlay;
        private TMP_Text _countdownLabel;
        private bool _wasDead;

        public RespawnScreenController(
            AetherClientSimulation sim,
            JoystickInputProvider joystick,
            AbilityInputProvider abilityInput)
        {
            _sim = sim;
            _joystick = joystick;
            _abilityInput = abilityInput;
        }

        public void Start()
        {
            _overlay = BuildOverlay();
            _overlay.SetActive(false);
        }

        public void Tick()
        {
            var localId = _sim.LocalId.Value;
            if (localId == null || !_sim.Players.TryGetValue(localId, out var state))
            {
                _overlay.SetActive(false);
                if (_wasDead)
                {
                    _joystick.SetVisible(true);
                    _abilityInput.SetVisible(true);
                    _wasDead = false;
                }
                return;
            }

            bool isDead = state.RespawnInTicks > 0;
            _overlay.SetActive(isDead);

            if (isDead != _wasDead)
            {
                _joystick.SetVisible(!isDead);
                _abilityInput.SetVisible(!isDead);
                _wasDead = isDead;
            }

            if (isDead)
            {
                float seconds = state.RespawnInTicks / TickRate;
                _countdownLabel.text = $"Respawning in {seconds:F1}s";
            }
        }

        public void Dispose()
        {
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
