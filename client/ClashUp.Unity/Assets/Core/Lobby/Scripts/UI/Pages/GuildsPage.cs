using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace ClashUp.Client.Lobby.UI.Pages
{
    public class GuildsPage : LobbyPage
    {
        [Header("Guilds Page UI")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private Transform _guildContainer;

        protected override void Awake()
        {
            base.Awake();
            _pageId          = "guilds";
            _pageDisplayName = "GUILDS";
        }

        public override void Initialize()
        {
            base.Initialize();
            BuildPlaceholderContent();
        }

        private void BuildPlaceholderContent()
        {
            if (_titleText != null)
                _titleText.text = "GUILDS";

            if (_guildContainer == null) return;

            string[] names = { "Alpha Wolves", "Storm Riders", "Shadow Legion", "Iron Fist", "Nova Strike" };
            for (int i = 0; i < names.Length; i++)
            {
                var card = new GameObject($"Guild_{i}");
                card.transform.SetParent(_guildContainer, false);

                var rect = card.AddComponent<RectTransform>();
                rect.sizeDelta = new Vector2(220f, 140f);

                var image = card.AddComponent<Image>();
                image.color = new Color(0.2f, 0.15f, 0.35f, 1f);

                var label = new GameObject("Label");
                label.transform.SetParent(card.transform, false);
                var labelRect = label.AddComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.sizeDelta = Vector2.zero;

                var tmp = label.AddComponent<TextMeshProUGUI>();
                tmp.text      = names[i];
                tmp.fontSize  = 20;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color     = Color.white;
            }
        }
    }
}
