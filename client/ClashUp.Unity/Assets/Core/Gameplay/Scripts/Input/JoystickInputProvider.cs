using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer.Unity;

namespace ClashUp.Client.Gameplay
{
    public sealed class JoystickInputProvider : IMovementInput, IStartable, IDisposable
    {
        // Touch zone: bottom 45% of screen, full width
        private static readonly Vector2 ZoneAnchorMin = new Vector2(0f, 0f);
        private static readonly Vector2 ZoneAnchorMax = new Vector2(1f, 0.45f);

        private const float BackgroundRadius = 240f;
        private const float HandleRadius    = 160f;

        private readonly MatchInputGate _gate;

        private Joystick _joystick;
        private GameObject _canvasRoot;

        public JoystickInputProvider(MatchInputGate gate)
        {
            _gate = gate;
        }

        public Vector2 Value
        {
            get
            {
                if (!_gate.IsEnabled) return Vector2.zero;

                // Keyboard — works on PC and development builds
                var kb = Keyboard.current;
                if (kb != null)
                {
                    var kbInput = new Vector2(
                        (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f),
                        (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f));

                    if (kbInput.sqrMagnitude > 0.01f)
                        return kbInput.normalized;
                }

                return _joystick != null ? _joystick.Direction : Vector2.zero;
            }
        }

        public void Start()
        {
            EnsureEventSystem();

            _canvasRoot = new GameObject("JoystickCanvas");
            var canvas = _canvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = _canvasRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            _joystick = BuildJoystick(canvas);
            _joystick.InputEnabled = false;

            _gate.OnChanged += OnGateChanged;
        }

        public void Dispose()
        {
            _gate.OnChanged -= OnGateChanged;

            if (_canvasRoot != null)
                UnityEngine.Object.Destroy(_canvasRoot);
        }

        private void OnGateChanged(bool enabled)
        {
            if (_joystick != null)
                _joystick.InputEnabled = enabled;

            if (!enabled)
                _joystick?.ForceReset();
        }

        private static Joystick BuildJoystick(Canvas canvas)
        {
            // Background visual — shown at touch-down point, hidden by default
            var bgGo = new GameObject("JoystickBackground");
            bgGo.transform.SetParent(canvas.transform, false);
            var bgRect = bgGo.AddComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(BackgroundRadius * 2f, BackgroundRadius * 2f);
            bgRect.anchorMin = new Vector2(0.5f, 0.5f);
            bgRect.anchorMax = new Vector2(0.5f, 0.5f);
            bgRect.pivot    = new Vector2(0.5f, 0.5f);
            var bgImage = bgGo.AddComponent<Image>();
            bgImage.sprite = CreateCircleSprite(256);
            bgImage.color  = new Color(0.15f, 0.15f, 0.15f, 0.55f);
            bgImage.raycastTarget = false;

            // Knob
            var handleGo = new GameObject("Handle");
            handleGo.transform.SetParent(bgGo.transform, false);
            var handleRect = handleGo.AddComponent<RectTransform>();
            handleRect.sizeDelta        = new Vector2(HandleRadius * 2f, HandleRadius * 2f);
            handleRect.anchoredPosition = Vector2.zero;
            var handleImage = handleGo.AddComponent<Image>();
            handleImage.sprite = CreateCircleSprite(128);
            handleImage.color  = new Color(1f, 1f, 1f, 0.75f);
            handleImage.raycastTarget = false;

            // Zone — RectTransform only, defines the input-active area, no Image needed
            var zoneGo = new GameObject("JoystickZone");
            zoneGo.transform.SetParent(canvas.transform, false);
            var zoneRect = zoneGo.AddComponent<RectTransform>();
            zoneRect.anchorMin = ZoneAnchorMin;
            zoneRect.anchorMax = ZoneAnchorMax;
            zoneRect.offsetMin = Vector2.zero;
            zoneRect.offsetMax = Vector2.zero;

            var joystick = zoneGo.AddComponent<Joystick>();
            joystick.Initialize(zoneRect, bgRect, handleRect, BackgroundRadius);
            return joystick;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();
        }

        private static Sprite CreateCircleSprite(int diameter)
        {
            var tex = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear
            };

            float center = (diameter - 1) * 0.5f;
            var pixels = new Color32[diameter * diameter];

            for (int y = 0; y < diameter; y++)
                for (int x = 0; x < diameter; x++)
                {
                    float dist = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                    byte a = (byte)(Mathf.Clamp01(center - dist + 0.5f) * 255);
                    pixels[y * diameter + x] = new Color32(255, 255, 255, a);
                }

            tex.SetPixels32(pixels);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, diameter, diameter), new Vector2(0.5f, 0.5f), diameter);
        }
    }
}
