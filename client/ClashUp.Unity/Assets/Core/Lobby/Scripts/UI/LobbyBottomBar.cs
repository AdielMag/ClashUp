using TMPro;
using UnityEngine;
using UnityEngine.UI;

using ClashUp.Client.Lobby.UI.Theme;

namespace ClashUp.Client.Lobby.UI
{
    /// <summary>
    /// Bottom navigation: 5 icon tabs (Store, Heroes, Play, Guild, Missions) with
    /// an elevated gold center Play tab. Tapping a tab navigates the page carousel. Selected tab sits
    /// on a gold tile with gold label; others are muted. Keeps the <see cref="SetCurrentPage"/> name so
    /// <see cref="LobbyHorizontalScroll"/> drives it unchanged.
    /// </summary>
    public class LobbyBottomBar : MonoBehaviour
    {
        [System.Serializable]
        public class Tab
        {
            public Button button;
            public Image icon;
            public Image tile;      // gold rounded tile behind a selected tab (hidden otherwise)
            public TMP_Text label;
            public bool elevated;   // center Play tab — always gold, scale-punches on select
        }

        [Header("References")]
        [SerializeField] private LobbyHorizontalScroll _mainScroll;
        [SerializeField] private Tab[] _tabs;

        [Header("Style")]
        [SerializeField] private Color _iconSelected = new Color(0.35f, 0.23f, 0.05f, 1f); // dark on gold tile
        [SerializeField] private Color _iconMuted    = new Color(0.66f, 0.70f, 0.89f, 1f);

        private int _lastPage = -1;

        private void Awake()
        {
            if (_tabs == null) return;
            for (int i = 0; i < _tabs.Length; i++)
            {
                int idx = i;
                var t = _tabs[i];
                if (t?.button != null)
                    t.button.onClick.AddListener(() => _mainScroll?.GoToPage(idx));
            }
        }

        public void SetCurrentPage(int pageIndex)
        {
            if (_tabs == null || pageIndex == _lastPage) return;
            _lastPage = pageIndex;

            for (int i = 0; i < _tabs.Length; i++)
            {
                var t = _tabs[i];
                if (t == null) continue;
                bool active = i == pageIndex;

                if (t.elevated)
                {
                    // center Play stays gold; just a subtle scale punch when selected
                    if (t.button != null)
                        ((RectTransform)t.button.transform).localScale = active ? Vector3.one * 1.06f : Vector3.one;
                    if (t.label != null) t.label.color = active ? LobbyTheme.TextGoldHi : LobbyTheme.TextMutedNav;
                    continue;
                }

                if (t.tile != null) t.tile.enabled = active;
                if (t.icon != null) t.icon.color = active ? _iconSelected : _iconMuted;
                if (t.label != null) t.label.color = active ? LobbyTheme.TextGoldHi : LobbyTheme.TextMutedNav;
            }
        }
    }
}
