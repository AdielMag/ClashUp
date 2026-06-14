using UnityEngine;

namespace ClashUp.Client.Lobby.UI
{
    /// <summary>
    /// Enables/disables the Canvas on a lobby page based on whether its world-space
    /// rect overlaps the screen. Checked once per LateUpdate. Completely custom —
    /// uses GetWorldCorners so it works regardless of scroll nesting depth.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class LobbyCanvasOptimizer : MonoBehaviour
    {
        // Pixels of padding outside screen edge still considered "visible"
        // (gives a buffer so canvas is enabled slightly before it snaps in)
        [SerializeField] private float _edgePadding = 60f;

        private Canvas       _canvas;
        private RectTransform _rect;
        private readonly Vector3[] _corners = new Vector3[4];
        private bool _lastVisible = true;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _rect   = GetComponent<RectTransform>();
        }

        private void LateUpdate()
        {
            bool visible = ComputeVisible();
            if (visible == _lastVisible) return;
            _lastVisible  = visible;
            _canvas.enabled = visible;
        }

        // ── public ────────────────────────────────────────────────────────────

        public void ForceEnable()
        {
            _canvas.enabled = true;
            _lastVisible    = true;
        }

        public void ForceDisable()
        {
            _canvas.enabled = false;
            _lastVisible    = false;
        }

        // ── private ───────────────────────────────────────────────────────────

        private bool ComputeVisible()
        {
            // GetWorldCorners returns screen-pixel positions for ScreenSpaceOverlay canvases
            _rect.GetWorldCorners(_corners);

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (var c in _corners)
            {
                if (c.x < minX) minX = c.x;
                if (c.y < minY) minY = c.y;
                if (c.x > maxX) maxX = c.x;
                if (c.y > maxY) maxY = c.y;
            }

            float sw = Screen.width  + _edgePadding;
            float sh = Screen.height + _edgePadding;
            float p  = _edgePadding;

            // AABB overlap test: page rect ∩ [−p, sw] × [−p, sh]
            return maxX > -p && minX < sw
                && maxY > -p && minY < sh;
        }
    }
}
