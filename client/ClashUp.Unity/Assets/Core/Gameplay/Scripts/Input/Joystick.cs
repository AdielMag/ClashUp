using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ClashUp.Client.Gameplay
{
    public sealed class Joystick : MonoBehaviour
    {
        internal static readonly HashSet<int> ClaimedTouchIds = new();
        private const int MouseSentinelId = -2;
        private const int NoTouch = -1;

        private RectTransform _zoneRect;
        private RectTransform _backgroundRect;
        private RectTransform _handleRect;
        private float _radius;
        private bool _dragging;
        private Vector2 _defaultAnchorPos;
        private int _activeTouchId = NoTouch;

        public event Action<bool> OnActiveChanged;

        public Vector2 Direction { get; private set; }
        public bool InputEnabled { get; set; }

        internal void Initialize(RectTransform zone, RectTransform background, RectTransform handle, float radius, Vector2 defaultAnchorPos)
        {
            _zoneRect = zone;
            _backgroundRect = background;
            _handleRect = handle;
            _radius = radius;
            _defaultAnchorPos = defaultAnchorPos;
            _backgroundRect.anchoredPosition = defaultAnchorPos;
            _backgroundRect.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!InputEnabled)
            {
                if (_dragging) EndDrag();
                return;
            }

            if (_dragging)
            {
                if (TryGetTrackedPointer(out var pos))
                    UpdateDrag(pos);
                else
                    EndDrag();
            }
            else
            {
                if (TryFindNewPointer(out var pos))
                    BeginDrag(pos);
            }
        }

        internal void ForceReset()
        {
            if (_dragging) EndDrag();
        }

        private bool TryFindNewPointer(out Vector2 screenPos)
        {
            var touch = Touchscreen.current;
            if (touch != null)
            {
                foreach (var tc in touch.touches)
                {
                    if (!tc.press.wasPressedThisFrame) continue;
                    int id = tc.touchId.ReadValue();
                    if (ClaimedTouchIds.Contains(id)) continue;
                    var pos = tc.position.ReadValue();
                    if (IsInZone(pos))
                    {
                        _activeTouchId = id;
                        ClaimedTouchIds.Add(id);
                        screenPos = pos;
                        return true;
                    }
                }
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !ClaimedTouchIds.Contains(MouseSentinelId))
            {
                var pos = mouse.position.ReadValue();
                if (IsInZone(pos))
                {
                    _activeTouchId = MouseSentinelId;
                    ClaimedTouchIds.Add(MouseSentinelId);
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
            Direction = Vector2.zero;
            OnActiveChanged?.Invoke(true);
        }

        private void UpdateDrag(Vector2 screenPos)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _backgroundRect, screenPos, null, out var local);
            var clamped = Vector2.ClampMagnitude(local, _radius);
            _handleRect.anchoredPosition = clamped;
            Direction = clamped / _radius;
        }

        private void EndDrag()
        {
            _dragging = false;
            Direction = Vector2.zero;
            _handleRect.anchoredPosition = Vector2.zero;
            _backgroundRect.anchoredPosition = _defaultAnchorPos;
            if (_activeTouchId != NoTouch)
            {
                ClaimedTouchIds.Remove(_activeTouchId);
                _activeTouchId = NoTouch;
            }
            OnActiveChanged?.Invoke(false);
        }
    }
}
