using UnityEngine;
using UnityEngine.InputSystem;

namespace ClashUp.Client.Gameplay
{
    /// <summary>
    /// Polls Touchscreen and Mouse directly — no EventSystem dependency.
    /// The zone RectTransform defines the screen area that activates the joystick.
    /// </summary>
    public sealed class Joystick : MonoBehaviour
    {
        private RectTransform _zoneRect;
        private RectTransform _backgroundRect;
        private RectTransform _handleRect;
        private float _radius;
        private bool _dragging;

        public Vector2 Direction { get; private set; }
        public bool InputEnabled { get; set; }

        internal void Initialize(RectTransform zone, RectTransform background, RectTransform handle, float radius)
        {
            _zoneRect = zone;
            _backgroundRect = background;
            _handleRect = handle;
            _radius = radius;
            _backgroundRect.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!InputEnabled)
            {
                if (_dragging) EndDrag();
                return;
            }

            ReadPointer(out bool justPressed, out bool isHeld, out Vector2 screenPos);

            if (!_dragging)
            {
                if (justPressed && IsInZone(screenPos))
                    BeginDrag(screenPos);
            }
            else
            {
                if (isHeld)
                    UpdateDrag(screenPos);
                else
                    EndDrag();
            }
        }

        internal void ForceReset()
        {
            if (_dragging) EndDrag();
        }

        private static void ReadPointer(out bool justPressed, out bool isHeld, out Vector2 screenPos)
        {
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
            {
                justPressed = touch.primaryTouch.press.wasPressedThisFrame;
                isHeld = true;
                screenPos = touch.primaryTouch.position.ReadValue();
                return;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                justPressed = mouse.leftButton.wasPressedThisFrame;
                isHeld = true;
                screenPos = mouse.position.ReadValue();
                return;
            }

            justPressed = false;
            isHeld = false;
            screenPos = default;
        }

        private bool IsInZone(Vector2 screenPos) =>
            RectTransformUtility.RectangleContainsScreenPoint(_zoneRect, screenPos, null);

        private void BeginDrag(Vector2 screenPos)
        {
            _dragging = true;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_backgroundRect.parent, screenPos, null, out var pos);
            _backgroundRect.anchoredPosition = pos;
            _backgroundRect.gameObject.SetActive(true);
            _handleRect.anchoredPosition = Vector2.zero;
            Direction = Vector2.zero;
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
            _backgroundRect.gameObject.SetActive(false);
        }
    }
}
