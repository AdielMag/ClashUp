using UnityEngine;
using UnityEngine.EventSystems;

namespace ClashUp.Client.Lobby.UI
{
    /// <summary>
    /// Main lobby scroller — owns all drag input.
    /// Horizontal axis: snaps between pages.
    /// Vertical axis: delegated to the active page's LobbyVerticalScroll.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class LobbyHorizontalScroll : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Pages")]
        [SerializeField] private int _pageCount = 5;
        [SerializeField] private int _defaultPage = 2; // 0-based

        [Header("Feel")]
        [SerializeField] private float _snapSpeed = 14f;
        [SerializeField, Range(0.05f, 0.5f)] private float _snapThreshold = 0.2f;
        // Ratio required on dominant axis before locking direction
        [SerializeField, Range(1f, 6f)] private float _lockRatio = 2f;
        // Min pixels of movement before we decide the axis
        [SerializeField] private float _lockMinPixels = 8f;

        [Header("References")]
        [SerializeField] private RectTransform _content;
        // One per page — vertical content scrollers
        [SerializeField] private LobbyVerticalScroll[] _pageScrollers;
        [SerializeField] private LobbyBottomBar _bottomBar;

        // ── state ─────────────────────────────────────────────────────────────
        private RectTransform _rt;
        private int   _currentPage;
        private float _pageWidth;
        private float _targetX;
        private bool  _snapping;

        // drag
        private bool    _dragging;
        private bool    _axisLocked;
        private bool    _lockedHorizontal;
        private bool    _vScrollStarted;
        private Vector2 _dragStartPointer;
        private float   _dragStartContentX;

        public int CurrentPage => _currentPage;

        // ── lifecycle ─────────────────────────────────────────────────────────

        private void Awake() => _rt = GetComponent<RectTransform>();

        private void Start()
        {
            Canvas.ForceUpdateCanvases();
            Rebuild(immediate: true);
        }

        private void Update()
        {
            float w = _rt.rect.width;
            if (!_dragging && Mathf.Abs(w - _pageWidth) > 1f)
                Rebuild(immediate: false);

            if (!_dragging && _snapping)
            {
                float cur  = _content.anchoredPosition.x;
                float next = Mathf.Lerp(cur, _targetX, Time.unscaledDeltaTime * _snapSpeed);
                if (Mathf.Abs(next - _targetX) < 0.5f) { next = _targetX; _snapping = false; }
                SetContentX(next);
            }
        }

        // ── drag ──────────────────────────────────────────────────────────────

        public void OnBeginDrag(PointerEventData e)
        {
            _dragging         = true;
            _axisLocked       = false;
            _lockedHorizontal = false;
            _vScrollStarted   = false;
            _snapping         = false;
            _dragStartPointer  = e.position;
            _dragStartContentX = _content.anchoredPosition.x;
        }

        public void OnDrag(PointerEventData e)
        {
            Vector2 delta = e.position - _dragStartPointer;

            if (!_axisLocked)
            {
                float ax = Mathf.Abs(delta.x), ay = Mathf.Abs(delta.y);
                if (ax + ay >= _lockMinPixels)
                {
                    _axisLocked = true;
                    if      (ax > ay * _lockRatio) _lockedHorizontal = true;
                    else if (ay > ax * _lockRatio) _lockedHorizontal = false;
                    else                           _lockedHorizontal = ax >= ay;
                }
                else return;
            }

            if (_lockedHorizontal)
            {
                // Move horizontal content (clamp to valid page range)
                float newX = Mathf.Clamp(
                    _dragStartContentX + delta.x,
                    -(_pageCount - 1) * _pageWidth,
                    0f);
                SetContentX(newX);
            }
            else
            {
                // First vertical event: tell the page scroller to start
                if (!_vScrollStarted)
                {
                    _vScrollStarted = true;
                    GetPageScroll(_currentPage)?.BeginDrag(_dragStartPointer);
                }
                GetPageScroll(_currentPage)?.Drag(e.position);
            }
        }

        public void OnEndDrag(PointerEventData e)
        {
            _dragging = false;

            if (_lockedHorizontal)
            {
                float moved = _content.anchoredPosition.x - _dragStartContentX;
                int target  = _currentPage;
                // moved < 0 = dragged left = next page (higher index)
                if      (moved < -_snapThreshold * _pageWidth) target = Mathf.Min(_currentPage + 1, _pageCount - 1);
                else if (moved >  _snapThreshold * _pageWidth) target = Mathf.Max(_currentPage - 1, 0);
                GoToPage(target);
            }
            else if (_vScrollStarted)
            {
                GetPageScroll(_currentPage)?.EndDrag(e.position);
            }
        }

        // ── public API ────────────────────────────────────────────────────────

        public void GoToPage(int index)
        {
            _currentPage = Mathf.Clamp(index, 0, _pageCount - 1);
            _targetX     = -_currentPage * _pageWidth;
            _snapping    = true;
            _bottomBar?.SetCurrentPage(_currentPage);
        }

        // ── private ───────────────────────────────────────────────────────────

        private void Rebuild(bool immediate)
        {
            _pageWidth = _rt.rect.width;
            if (_pageWidth <= 0f) return;

            // Size the horizontal content strip
            _content.sizeDelta = new Vector2(_pageWidth * _pageCount, 0f);

            // Lay out page children side-by-side
            int count = _content.childCount;
            for (int i = 0; i < count; i++)
            {
                var child = (RectTransform)_content.GetChild(i);
                child.anchorMin        = new Vector2(0f, 0f);
                child.anchorMax        = new Vector2(0f, 1f);
                child.pivot            = new Vector2(0f, 0.5f);
                child.sizeDelta        = new Vector2(_pageWidth, 0f);
                child.anchoredPosition = new Vector2(i * _pageWidth, 0f);
            }

            if (immediate)
            {
                _currentPage = Mathf.Clamp(_defaultPage, 0, _pageCount - 1);
                _targetX     = -_currentPage * _pageWidth;
                SetContentX(_targetX);
                _snapping = false;
                _bottomBar?.SetCurrentPage(_currentPage);
            }
            else
            {
                _targetX  = -_currentPage * _pageWidth;
                _snapping = true;
            }
        }

        private void SetContentX(float x)
        {
            var p = _content.anchoredPosition;
            _content.anchoredPosition = new Vector2(x, p.y);
        }

        private LobbyVerticalScroll GetPageScroll(int idx)
        {
            if (_pageScrollers == null || (uint)idx >= (uint)_pageScrollers.Length) return null;
            return _pageScrollers[idx];
        }
    }
}
