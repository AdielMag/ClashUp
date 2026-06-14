using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace ClashUp.Client.Lobby.UI.Pages
{
    public class CharacterPage : LobbyPage
    {
        [Header("Character Page UI")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private Transform _characterContainer;
        [SerializeField] private Image _characterPreview;

        protected override void Awake()
        {
            base.Awake();
            _pageId = "character";
            _pageDisplayName = "CHARACTER";
        }

        public override void Initialize()
        {
            base.Initialize();
            BuildPlaceholderContent();
        }

        private void BuildPlaceholderContent()
        {
            if (_titleText != null)
                _titleText.text = "CHARACTER";

            if (_characterContainer != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    var slot = new GameObject($"CharacterSlot_{i}");
                    slot.transform.SetParent(_characterContainer, false);

                    var rect = slot.AddComponent<RectTransform>();
                    rect.sizeDelta = new Vector2(150f, 200f);

                    var image = slot.AddComponent<Image>();
                    image.color = new Color(0.3f, 0.2f, 0.2f, 1f);

                    var label = new GameObject("Label");
                    label.transform.SetParent(slot.transform, false);
                    var labelRect = label.AddComponent<RectTransform>();
                    labelRect.anchorMin = new Vector2(0f, 0f);
                    labelRect.anchorMax = new Vector2(1f, 0f);
                    labelRect.sizeDelta = new Vector2(0f, 30f);
                    labelRect.anchoredPosition = new Vector2(0f, 15f);

                    var tmp = label.AddComponent<TextMeshProUGUI>();
                    tmp.text = $"Slot {i + 1}";
                    tmp.fontSize = 18;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.color = Color.white;
                }
            }

            if (_characterPreview != null)
            {
                _characterPreview.color = new Color(0.4f, 0.4f, 0.5f, 1f);
            }
        }
    }
}
