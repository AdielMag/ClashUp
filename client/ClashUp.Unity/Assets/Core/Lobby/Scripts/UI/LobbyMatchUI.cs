using System;

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClashUp.Client.Lobby
{
    public sealed class LobbyUI
    {
        private readonly GameObject _root;
        private readonly Button _playButton;

        public event Action OnPlayClicked;

        private LobbyUI(GameObject root, Button playButton)
        {
            _root = root;
            _playButton = playButton;
            _playButton.onClick.AddListener(() => OnPlayClicked?.Invoke());
        }

        public static LobbyUI Create()
        {
            var root = new GameObject("LobbyUI");

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();

            // Title at top
            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(root.transform, false);
            var titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -60f);
            titleRect.sizeDelta = new Vector2(600f, 80f);
            var titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "CLASH UP";
            titleText.fontSize = 64;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;
            titleText.fontStyle = FontStyles.Bold;

            // Play button
            var btnObj = new GameObject("PlayButton");
            btnObj.transform.SetParent(root.transform, false);

            var btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchoredPosition = Vector2.zero;
            btnRect.sizeDelta = new Vector2(300f, 80f);

            var btnImage = btnObj.AddComponent<Image>();
            btnImage.color = new Color(0.2f, 0.6f, 0.2f, 1f);

            var button = btnObj.AddComponent<Button>();
            button.targetGraphic = btnImage;

            var btnTextObj = new GameObject("Text");
            btnTextObj.transform.SetParent(btnObj.transform, false);
            var btnTextRect = btnTextObj.AddComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.sizeDelta = Vector2.zero;

            var btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnText.text = "Play";
            btnText.fontSize = 36;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.color = Color.white;

            return new LobbyUI(root, button);
        }

        public void Destroy()
        {
            if (_root != null)
                UnityEngine.Object.Destroy(_root);
        }
    }
}
