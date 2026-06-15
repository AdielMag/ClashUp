using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ClashUp.Client.Gameplay
{
    public sealed class AbilityButton : MonoBehaviour
    {
        private const int MouseSentinelId = -2;
        private const int NoTouch = -1;
        private const float AimThreshold = 0.25f;

        private RectTransform _zoneRect;
        private RectTransform _backgroundRect;
        private RectTransform _handleRect;
        private RectTransform _arrowRect;
        private Image _arrowImage;
        private Image _cooldownImage;
        private float _radius;
        private Vector2 _defaultAnchorPos;
        private bool _dragging;
        private int _activeTouchId = NoTouch;

        private float _cooldownTotal;
        private float _cooldownRemaining;

        public event Action<bool> OnActiveChanged;

        public bool WasFired { get; private set; }
        public Vector2 AimDirection { get; private set; }
        public bool InputEnabled { get; set; }
        public bool IsOnCooldown => _cooldownRemaining > 0f;

        internal void Initialize(RectTransform zone, RectTransform background, RectTransform handle,
                                  RectTransform arrow, Image arrowImage, Image cooldownImage,
                                  float radius, Vector2 defaultAnchorPos)
        {
            _zoneRect = zone;
            _backgroundRect = background;
            _handleRect = handle;
            _arrowRect = arrow;
            _arrowImage = arrowImage;
            _cooldownImage = cooldownImage;
            _radius = radius;
            _defaultAnchorPos = defaultAnchorPos;
            _backgroundRect.anchoredPosition = defaultAnchorPos;
            _backgroundRect.gameObject.SetActive(true);
            if (_cooldownImage != null) _cooldownImage.fillAmount = 0f;
        }

        public void StartCooldown(float seconds)
        {
            _cooldownTotal = seconds;
            _cooldownRemaining = seconds;
        }

        public void Consume()
        {
            WasFired = false;
            AimDirection = Vector2.zero;
        }

        private void Update()
        {
            if (_cooldownRemaining > 0f)
            {
                _cooldownRemaining -= Time.deltaTime;
                if (_cooldownImage != null)
                    _cooldownImage.fillAmount = Mathf.Clamp01(_cooldownRemaining / _cooldownTotal);
            }

            if (!InputEnabled)
            {
                if (_dragging) CancelDrag();
                return;
            }

            if (_dragging)
            {
                if (TryGetTrackedPointer(out var pos))
                    UpdateDrag(pos);
                else
                    ReleaseDrag();
            }
            else
            {
                if (TryFindNewPointer(out var pos))
                    BeginDrag(pos);
            }
        }

        internal void ForceReset()
        {
            if (_dragging) CancelDrag();
            Consume();
        }

        private bool TryFindNewPointer(out Vector2 screenPos)
        {
            if (IsOnCooldown) { screenPos = default; return false; }

            var touch = Touchscreen.current;
            if (touch != null)
            {
                foreach (var tc in touch.touches)
                {
                    if (!tc.press.wasPressedThisFrame) continue;
                    int id = tc.touchId.ReadValue();
                    if (Joystick.ClaimedTouchIds.Contains(id)) continue;
                    var pos = tc.position.ReadValue();
                    if (IsInZone(pos))
                    {
                        _activeTouchId = id;
                        Joystick.ClaimedTouchIds.Add(id);
                        screenPos = pos;
                        return true;
                    }
                }
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame
                && !Joystick.ClaimedTouchIds.Contains(MouseSentinelId))
            {
                var pos = mouse.position.ReadValue();
                if (IsInZone(pos))
                {
                    _activeTouchId = MouseSentinelId;
                    Joystick.ClaimedTouchIds.Add(MouseSentinelId);
                    screenPos = pos;
                    return true;
                }
            }

            screenPos = default;
            return false;
        }

        private bool TryGetTrackedPointer(out Vector2 screenPos)
        {
            if (_activeTouchId == MouseSentinelId)
            {
                var mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.isPressed)
                {
                    screenPos = mouse.position.ReadValue();
                    return true;
                }
                screenPos = default;
                return false;
            }

            var touch = Touchscreen.current;
            if (touch != null)
            {
                foreach (var tc in touch.touches)
                {
                    if (tc.touchId.ReadValue() != _activeTouchId) continue;
                    if (tc.press.isPressed)
                    {
                        screenPos = tc.position.ReadValue();
                        return true;
                    }
                    screenPos = default;
                    return false;
                }
            }

            screenPos = default;
            return false;
        }

        private bool IsInZone(Vector2 screenPos) =>
            RectTransformUtility.RectangleContainsScreenPoint(_zoneRect, screenPos, null);

        private void BeginDrag(Vector2 screenPos)
        {
            _dragging = true;
            bool onBackground = RectTransformUtility.RectangleContainsScreenPoint(_backgroundRect, screenPos, null);
            if (!onBackground)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)_backgroundRect.parent, screenPos, null, out var pos);
                _backgroundRect.anchoredPosition = pos;
            }
            _handleRect.anchoredPosition = Vector2.zero;
            SetArrowVisible(false);
            OnActiveChanged?.Invoke(true);
        }

        private void UpdateDrag(Vector2 screenPos)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _backgroundRect, screenPos, null, out var local);
            var clamped = Vector2.ClampMagnitude(local, _radius);
            _handleRect.anchoredPosition = clamped;

            float norm = clamped.magnitude / _radius;
            if (norm >= AimThreshold)
            {
                SetArrowVisible(true);
                float angle = Mathf.Atan2(-clamped.x, clamped.y) * Mathf.Rad2Deg;
                _arrowRect.localRotation = Quaternion.Euler(0f, 0f, angle);
                _arrowRect.sizeDelta = new Vector2(6f, clamped.magnitude);
            }
            else
            {
                SetArrowVisible(false);
            }
        }

        private void ReleaseDrag()
        {
            var handleNorm = _handleRect.anchoredPosition / _radius;
            float magnitude = handleNorm.magnitude;

            WasFired = true;
            AimDirection = magnitude >= AimThreshold ? handleNorm.normalized : Vector2.zero;

            _handleRect.anchoredPosition = Vector2.zero;
            _backgroundRect.anchoredPosition = _defaultAnchorPos;
            SetArrowVisible(false);
            _dragging = false;

            if (_activeTouchId != NoTouch)
            {
                Joystick.ClaimedTouchIds.Remove(_activeTouchId);
                _activeTouchId = NoTouch;
            }
            OnActiveChanged?.Invoke(false);
        }

        private void CancelDrag()
        {
            _handleRect.anchoredPosition = Vector2.zero;
            _backgroundRect.anchoredPosition = _defaultAnchorPos;
            SetArrowVisible(false);
            _dragging = false;

            if (_activeTouchId != NoTouch)
            {
                Joystick.ClaimedTouchIds.Remove(_activeTouchId);
                _activeTouchId = NoTouch;
            }
            OnActiveChanged?.Invoke(false);
        }

        private void SetArrowVisible(bool visible)
        {
            if (_arrowImage == null) return;
            var c = _arrowImage.color;
            c.a = visible ? 0.8f : 0f;
            _arrowImage.color = c;
        }
    }
}
