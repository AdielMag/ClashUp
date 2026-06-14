using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace ClashUp.Client.Lobby.UI.Pages
{
    public class StorePage : LobbyPage
    {
        [Header("Store Page UI")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private Transform _itemContainer;
        [SerializeField] private Button _backButton;

        protected override void Awake()
        {
            base.Awake();
            _pageId = "store";
            _pageDisplayName = "STORE";
        }

        public override void Initialize()
        {
            base.Initialize();
            BuildPlaceholderContent();
        }

        public override void OnPageShown()
        {
            base.OnPageShown();
        }

        public override void OnPageHidden()
        {
            base.OnPageHidden();
        }

        private void BuildPlaceholderContent()
        {
            if (_titleText != null)
                _titleText.text = "STORE";

            // Create placeholder store items if container exists
            if (_itemContainer != null)
            {
                for (int i = 0; i < 5; i++)
                {
                    var item = new GameObject($"StoreItem_{i}");
                    item.transform.SetParent(_itemContainer, false);

                    var rect = item.AddComponent<RectTransform>();
                    rect.sizeDelta = new Vector2(200f, 250f);

                    var image = item.AddComponent<Image>();
                    image.color = new Color(0.2f, 0.2f, 0.3f, 1f);

                    var label = new GameObject("Label");
                    label.transform.SetParent(item.transform, false);
                    var labelRect = label.AddComponent<RectTransform>();
                    labelRect.anchorMin = Vector2.zero;
                    labelRect.anchorMax = Vector2.one;
                    labelRect.sizeDelta = Vector2.zero;

                    var tmp = label.AddComponent<TextMeshProUGUI>();
                    tmp.text = $"Item {i + 1}";
                    tmp.fontSize = 24;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.color = Color.white;
                }
            }
        }
    }
}
