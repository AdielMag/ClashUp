using UnityEngine;

namespace ClashUp.Client.Lobby.UI
{
    /// <summary>
    /// Per-page vertical content scroller.
    /// NOT an EventSystem handler — driven by LobbyHorizontalScroll via public API.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class LobbyVerticalScroll : MonoBehaviour
    {
        [Header("Feel")]
        [SerializeField] private float _snapSpeed = 12f;
        // Deceleration multiplier applied to scroll momentum each frame
        [SerializeField] private float _deceleration = 8f;

        [Header("References")]
        [SerializeField] private RectTransform _content;

        // ── state ─────────────────────────────────────────────────────────────
        private RectTransform _rt;
        private float _viewportH;
        private float _contentH;
        private float _targetY;
        private float _velocity;
        private bool  _coasting; // momentum after EndDrag

        // drag
        private bool    _dragging;
        private Vector2 _dragStartPointer;
        private float   _dragStartContentY;
        private float   _prevDragY;
        private float   _prevDragTime;

        // ── lifecycle ─────────────────────────────────────────────────────────

        private void Awake() => _rt = GetComponent<RectTransform>();

        private void Start()
        {
            Canvas.ForceUpdateCanvases();
            RecalculateLayout();
        }

        private void Update()
        {
            if (_dragging) return;

            if (_coasting)
            {
                _velocity  = Mathf.Lerp(_velocity, 0f, Time.unscaledDeltaTime * _deceleration);
                float newY = Mathf.Clamp(_content.anchoredPosition.y + _velocity * Time.unscaledDeltaTime,
                                          0f, MaxScrollY());
                SetContentY(newY);
                if (Mathf.Abs(_velocity) < 1f) _coasting = false;
            }
        }

        // ── public API — called by LobbyHorizontalScroll ───────────────────────

        public void BeginDrag(Vector2 pointerPos)
        {
            _dragging          = true;
            _coasting          = false;
            _velocity          = 0f;
            _dragStartPointer  = pointerPos;
            _dragStartContentY = _content.anchoredPosition.y;
            _prevDragY         = pointerPos.y;
            _prevDragTime      = Time.unscaledTime;
            RecalculateLayout();
        }

        public void Drag(Vector2 pointerPos)
        {
            if (!_dragging) return;

            // Track velocity for momentum
            float dt = Time.unscaledTime - _prevDragTime;
            if (dt > 0.001f)
                _velocity = (pointerPos.y - _prevDragY) / dt;
            _prevDragY   = pointerPos.y;
            _prevDragTime = Time.unscaledTime;

            float dy   = pointerPos.y - _dragStartPointer.y;
            // Finger UP (dy > 0) → content up → see content below ✓
            float newY = Mathf.Clamp(_dragStartContentY + dy, 0f, MaxScrollY());
            SetContentY(newY);
        }

        public void EndDrag(Vector2 pointerPos)
        {
            _dragging = false;
            // Let momentum coast
            _coasting = Mathf.Abs(_velocity) > 50f;
        }

        /// <summary>Reset scroll to top (called when navigating back to this page).</summary>
        public void ResetToTop()
        {
            _coasting = false;
            _velocity = 0f;
            SetContentY(0f);
        }

        // ── private ───────────────────────────────────────────────────────────

        private void RecalculateLayout()
        {
            Canvas.ForceUpdateCanvases();
            _viewportH = _rt.rect.height;
            _contentH  = _content != null ? _content.rect.height : _viewportH;
        }

        private float MaxScrollY() => Mathf.Max(0f, _contentH - _viewportH);

        private void SetContentY(float y)
        {
            var p = _content.anchoredPosition;
            _content.anchoredPosition = new Vector2(p.x, y);
        }
    }
}
