using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace ClashUp.Client.Lobby.UI.Pages
{
    public class SettingsPage : LobbyPage
    {
        [Header("Settings Page UI")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private Transform _settingsContainer;

        protected override void Awake()
        {
            base.Awake();
            _pageId          = "settings";
            _pageDisplayName = "SETTINGS";
        }

        public override void Initialize()
        {
            base.Initialize();
            BuildPlaceholderContent();
        }

        private void BuildPlaceholderContent()
        {
            if (_titleText != null)
                _titleText.text = "SETTINGS";

            if (_settingsContainer == null) return;

            string[] items = { "Audio", "Graphics", "Controls", "Account", "About" };
            for (int i = 0; i < items.Length; i++)
            {
                var row = new GameObject($"Setting_{items[i]}");
                row.transform.SetParent(_settingsContainer, false);

                var rect = row.AddComponent<RectTransform>();
                rect.sizeDelta = new Vector2(320f, 60f);

                var image = row.AddComponent<Image>();
                image.color = new Color(0.15f, 0.15f, 0.2f, 1f);

                var label = new GameObject("Label");
                label.transform.SetParent(row.transform, false);
                var labelRect = label.AddComponent<RectTransform>();
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(1f, 1f);
                labelRect.sizeDelta = Vector2.zero;

                var tmp = label.AddComponent<TextMeshProUGUI>();
                tmp.text      = items[i];
                tmp.fontSize  = 22;
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                tmp.color     = Color.white;
                tmp.margin    = new Vector4(20f, 0f, 0f, 0f);
            }
        }
    }
}
