using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer.Unity;

namespace ClashUp.Client.Gameplay
{
    public sealed class AbilityInputProvider : IAbilityInput, IStartable, IDisposable
    {
        // Rightmost 40% of screen — 10% gap from movement joystick zone (which ends at 0.5)
        private static readonly Vector2 ZoneAnchorMin = new Vector2(0.6f, 0f);
        private static readonly Vector2 ZoneAnchorMax = new Vector2(1.0f, 0.45f);

        // 70% of movement joystick (movement: 240/160)
        private const float BackgroundRadius = 168f;
        private const float HandleRadius    = 112f;
        private const int ActiveSlotIndex   = 1;
        private const float CooldownSeconds = 3f;

        private readonly MatchInputGate _gate;

        private AbilityButton _button;
        private GameObject _canvasRoot;
        private bool _pendingFire;
        private float _pendingAimYaw;
        private bool _isTouching;

        public event Action<bool> OnTouching;

        public void SetVisible(bool visible)
        {
            if (_canvasRoot != null) _canvasRoot.SetActive(visible);
        }

        public AbilityInputProvider(MatchInputGate gate) => _gate = gate;

        public uint ButtonMask  => _pendingFire ? (1u << ActiveSlotIndex) : 0u;
        public float AimYaw     => _pendingAimYaw;
        public float LiveAimYaw => ComputeLiveAimYaw();

        public void Poll()
        {
            if (_pendingFire) return;

            if (_button != null && _button.WasFired)
            {
                _pendingFire = true;
                var dir = _button.AimDirection;
                _pendingAimYaw = dir.sqrMagnitude > 0.01f
                    ? Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg
                    : 0f;
                _button.Consume();
                _button.StartCooldown(CooldownSeconds);
                return;
            }

            var kb = Keyboard.current;
            if (kb != null && kb.qKey.wasPressedThisFrame && _gate.IsEnabled
                && _button != null && !_button.IsOnCooldown)
            {
                _pendingFire = true;
                var mouse = Mouse.current;
                if (mouse != null)
                {
                    var screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                    var dir = mouse.position.ReadValue() - screenCenter;
                    _pendingAimYaw = dir.sqrMagnitude > 400f
                        ? Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg
                        : 0f;
                }
                else
                {
                    _pendingAimYaw = 0f;
                }
                _button.StartCooldown(CooldownSeconds);
            }
        }

        public void ConsumeInput()
        {
            _pendingFire   = false;
            _pendingAimYaw = 0f;
        }

        private float ComputeLiveAimYaw()
        {
            if (_button != null && _isTouching)
            {
                var dir = _button.CurrentAimDirection;
                if (dir.sqrMagnitude > 0.01f)
                    return Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            }

            // Desktop fallback: mouse offset from screen centre while LMB held
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                var screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                var dir = mouse.position.ReadValue() - screenCenter;
                if (dir.sqrMagnitude > 400f)
                    return Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            }

            return 0f;
        }

        public void Start()
        {
            _canvasRoot = new GameObject("AbilityCanvas");
            var canvas = _canvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 101;

            var scaler = _canvasRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            _button = BuildAbilityButton(canvas);
            _button.InputEnabled = false;
            _button.OnActiveChanged += active =>
            {
                _isTouching = active;
                OnTouching?.Invoke(active);
            };

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
            if (_button != null) _button.InputEnabled = enabled;
            if (!enabled) _button?.ForceReset();
        }

        private static AbilityButton BuildAbilityButton(Canvas canvas)
        {
            var canvasRect = (RectTransform)canvas.transform;
            float w = canvasRect.rect.width;
            float h = canvasRect.rect.height;

            // Background — anchored at canvas center, offset to right side
            var bgGo = new GameObject("AbilityBackground");
            bgGo.transform.SetParent(canvas.transform, false);
            var bgRect = bgGo.AddComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(BackgroundRadius * 2f, BackgroundRadius * 2f);
            bgRect.anchorMin = new Vector2(0.5f, 0.5f);
            bgRect.anchorMax = new Vector2(0.5f, 0.5f);
            bgRect.pivot     = new Vector2(0.5f, 0.5f);
            var bgImage = bgGo.AddComponent<Image>();
            bgImage.sprite        = CreateCircleSprite(256);
            bgImage.color         = new Color(0.9f, 0.5f, 0.1f, 0.45f);
            bgImage.raycastTarget = false;

            // Cooldown overlay (filled radial, on top of background)
            var cdGo = new GameObject("CooldownOverlay");
            cdGo.transform.SetParent(bgGo.transform, false);
            var cdRect = cdGo.AddComponent<RectTransform>();
            cdRect.anchorMin = Vector2.zero;
            cdRect.anchorMax = Vector2.one;
            cdRect.offsetMin = Vector2.zero;
            cdRect.offsetMax = Vector2.zero;
            var cdImage = cdGo.AddComponent<Image>();
            cdImage.sprite        = CreateCircleSprite(256);
            cdImage.color         = new Color(0.1f, 0.1f, 0.1f, 0.7f);
            cdImage.type          = Image.Type.Filled;
            cdImage.fillMethod    = Image.FillMethod.Radial360;
            cdImage.fillOrigin    = (int)Image.Origin360.Top;
            cdImage.fillClockwise = true;
            cdImage.fillAmount    = 0f;
            cdImage.raycastTarget = false;

            // Handle (moves with drag)
            var handleGo = new GameObject("AbilityHandle");
            handleGo.transform.SetParent(bgGo.transform, false);
            var handleRect = handleGo.AddComponent<RectTransform>();
            handleRect.sizeDelta        = new Vector2(HandleRadius * 2f, HandleRadius * 2f);
            handleRect.anchoredPosition = Vector2.zero;
            var handleImage = handleGo.AddComponent<Image>();
            handleImage.sprite        = CreateCircleSprite(128);
            handleImage.color         = new Color(1f, 0.65f, 0.1f, 0.9f);
            handleImage.raycastTarget = false;

            // Aim arrow (hidden at rest)
            var arrowGo = new GameObject("AimArrow");
            arrowGo.transform.SetParent(bgGo.transform, false);
            var arrowRect = arrowGo.AddComponent<RectTransform>();
            arrowRect.sizeDelta        = new Vector2(6f, BackgroundRadius);
            arrowRect.pivot            = new Vector2(0.5f, 0f);
            arrowRect.anchoredPosition = Vector2.zero;
            var arrowImage = arrowGo.AddComponent<Image>();
            arrowImage.color         = new Color(1f, 0.8f, 0.2f, 0f);
            arrowImage.raycastTarget = false;

            // Zone — right half of screen
            var zoneGo = new GameObject("AbilityZone");
            zoneGo.transform.SetParent(canvas.transform, false);
            var zoneRect = zoneGo.AddComponent<RectTransform>();
            zoneRect.anchorMin = ZoneAnchorMin;
            zoneRect.anchorMax = ZoneAnchorMax;
            zoneRect.offsetMin = Vector2.zero;
            zoneRect.offsetMax = Vector2.zero;

            // Default position: center of right zone (60–100%)
            var defaultPos = new Vector2(w * 0.30f, -h * 0.30f);

            var button = zoneGo.AddComponent<AbilityButton>();
            button.Initialize(zoneRect, bgRect, handleRect, arrowRect, arrowImage, cdImage,
                              BackgroundRadius, defaultPos);
            return button;
        }

        private static Sprite CreateCircleSprite(int diameter)
        {
            var tex = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear
            };

            float center = (diameter - 1) * 0.5f;
            var pixels   = new Color32[diameter * diameter];

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
