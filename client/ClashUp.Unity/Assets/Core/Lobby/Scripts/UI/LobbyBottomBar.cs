using UnityEngine;
using UnityEngine.UI;

namespace ClashUp.Client.Lobby.UI
{
    /// <summary>
    /// Bottom navigation bar — 5 dot indicators, one per lobby page.
    /// Active dot is larger and fully opaque; others are smaller and dimmed.
    /// Clicking a dot navigates to that page.
    /// </summary>
    public class LobbyBottomBar : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LobbyHorizontalScroll _mainScroll;
        [SerializeField] private RectTransform _indicatorContainer;

        [Header("Style")]
        [SerializeField] private Color _activeColor   = Color.white;
        [SerializeField] private Color _inactiveColor = new Color(1f, 1f, 1f, 0.30f);
        [SerializeField] private float _activeSize    = 16f;
        [SerializeField] private float _inactiveSize  = 10f;

        private Image[] _dots;
        private int _lastPage = -1;

        private void Awake()
        {
            if (_indicatorContainer == null) return;

            int count = _indicatorContainer.childCount;
            _dots = new Image[count];

            for (int i = 0; i < count; i++)
            {
                var child = _indicatorContainer.GetChild(i);
                _dots[i] = child.GetComponent<Image>();

                int idx = i; // capture for closure
                var btn = child.GetComponent<Button>();
                if (btn != null)
                    btn.onClick.AddListener(() => _mainScroll?.GoToPage(idx));
            }
        }

        public void SetCurrentPage(int pageIndex)
        {
            if (_dots == null || pageIndex == _lastPage) return;
            _lastPage = pageIndex;

            for (int i = 0; i < _dots.Length; i++)
            {
                if (_dots[i] == null) continue;
                bool active = i == pageIndex;
                _dots[i].color = active ? _activeColor : _inactiveColor;
                var rt = (RectTransform)_dots[i].transform;
                float sz = active ? _activeSize : _inactiveSize;
                rt.sizeDelta = new Vector2(sz, sz);
            }
        }
    }
}
