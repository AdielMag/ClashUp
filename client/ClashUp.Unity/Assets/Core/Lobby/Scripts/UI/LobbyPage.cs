using UnityEngine;

namespace ClashUp.Client.Lobby.UI
{
    /// <summary>
    /// Base class for all lobby pages.
    /// Each page lives in its own subdomain and has its own Canvas.
    /// The Canvas is enabled/disabled by LobbyCanvasOptimizer for performance.
    /// </summary>
    public abstract class LobbyPage : MonoBehaviour
    {
        [Header("Page Settings")]
        [SerializeField] protected string _pageId;
        [SerializeField] protected string _pageDisplayName;

        [Header("References")]
        [SerializeField] protected Canvas _pageCanvas;
        [SerializeField] protected RectTransform _rootRect;
        [SerializeField] protected LobbyCanvasOptimizer _optimizer;

        public string PageId => _pageId;
        public string PageDisplayName => _pageDisplayName;
        public Canvas PageCanvas => _pageCanvas;
        public bool IsActive => gameObject.activeInHierarchy && _pageCanvas != null && _pageCanvas.enabled;

        protected virtual void Awake()
        {
            // Get components if not assigned
            if (_pageCanvas == null)
                _pageCanvas = GetComponent<Canvas>();
            if (_rootRect == null)
                _rootRect = GetComponent<RectTransform>();
            if (_optimizer == null)
                _optimizer = GetComponent<LobbyCanvasOptimizer>();
        }

        /// <summary>
        /// Called when the page becomes visible (navigated to via vertical scroll).
        /// Override to initialize data, load resources, etc.
        /// </summary>
        public virtual void OnPageShown()
        {
            if (_optimizer != null)
                _optimizer.ForceEnable();
            else if (_pageCanvas != null)
                _pageCanvas.enabled = true;

            Debug.Log($"[LobbyPage] Shown: {_pageId}");
        }

        /// <summary>
        /// Called when the page is no longer visible (user scrolled away).
        /// Override to save state, release temporary resources, etc.
        /// </summary>
        public virtual void OnPageHidden()
        {
            if (_optimizer != null)
                _optimizer.ForceDisable();
            else if (_pageCanvas != null)
                _pageCanvas.enabled = false;

            Debug.Log($"[LobbyPage] Hidden: {_pageId}");
        }

        /// <summary>
        /// Initialize the page with any required data.
        /// </summary>
        public virtual void Initialize()
        {
            Debug.Log($"[LobbyPage] Initialized: {_pageId}");
        }

        /// <summary>
        /// Clean up when the page is destroyed.
        /// </summary>
        protected virtual void OnDestroy()
        {
            Debug.Log($"[LobbyPage] Destroyed: {_pageId}");
        }
    }
}
