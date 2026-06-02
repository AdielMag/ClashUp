using System;

using ClashUp.Shared.MessagePackObjects;

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClashUp.Client.Match
{
    public sealed class MatchUI
    {
        private readonly GameObject _root;
        private readonly TMP_Text _statusLabel;
        private readonly TMP_Text _timerLabel;
        private readonly TMP_Text _playerCountLabel;
        private readonly GameObject _backButtonObj;
        private readonly Button _backButton;

        public event Action OnBackToLobbyClicked;

        private MatchUI(GameObject root, TMP_Text statusLabel, TMP_Text timerLabel,
            TMP_Text playerCountLabel, GameObject backButtonObj, Button backButton)
        {
            _root = root;
            _statusLabel = statusLabel;
            _timerLabel = timerLabel;
            _playerCountLabel = playerCountLabel;
            _backButtonObj = backButtonObj;
            _backButton = backButton;
            _backButton.onClick.AddListener(() => OnBackToLobbyClicked?.Invoke());
            _backButtonObj.SetActive(false);
        }

        public static MatchUI Create()
        {
            var root = new GameObject("MatchUI");

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();

            // Status label (top center)
            var statusObj = new GameObject("StatusLabel");
            statusObj.transform.SetParent(root.transform, false);
            var statusRect = statusObj.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.5f, 1f);
            statusRect.anchorMax = new Vector2(0.5f, 1f);
            statusRect.pivot = new Vector2(0.5f, 1f);
            statusRect.anchoredPosition = new Vector2(0f, -40f);
            statusRect.sizeDelta = new Vector2(600f, 50f);
            var statusLabel = statusObj.AddComponent<TextMeshProUGUI>();
            statusLabel.text = "Waiting...";
            statusLabel.fontSize = 28;
            statusLabel.alignment = TextAlignmentOptions.Center;
            statusLabel.color = Color.white;

            // Timer label (center)
            var timerObj = new GameObject("TimerLabel");
            timerObj.transform.SetParent(root.transform, false);
            var timerRect = timerObj.AddComponent<RectTransform>();
            timerRect.anchoredPosition = new Vector2(0f, 60f);
            timerRect.sizeDelta = new Vector2(300f, 60f);
            var timerLabel = timerObj.AddComponent<TextMeshProUGUI>();
            timerLabel.text = "--:--";
            timerLabel.fontSize = 48;
            timerLabel.alignment = TextAlignmentOptions.Center;
            timerLabel.color = Color.white;

            // Player count label
            var playerObj = new GameObject("PlayerCountLabel");
            playerObj.transform.SetParent(root.transform, false);
            var playerRect = playerObj.AddComponent<RectTransform>();
            playerRect.anchoredPosition = new Vector2(0f, -20f);
            playerRect.sizeDelta = new Vector2(300f, 40f);
            var playerCountLabel = playerObj.AddComponent<TextMeshProUGUI>();
            playerCountLabel.text = "Players: 0";
            playerCountLabel.fontSize = 24;
            playerCountLabel.alignment = TextAlignmentOptions.Center;
            playerCountLabel.color = Color.white;

            // Back to Lobby button (hidden initially)
            var btnObj = new GameObject("BackToLobbyButton");
            btnObj.transform.SetParent(root.transform, false);
            var btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchoredPosition = new Vector2(0f, -120f);
            btnRect.sizeDelta = new Vector2(300f, 70f);
            var btnImage = btnObj.AddComponent<Image>();
            btnImage.color = new Color(0.6f, 0.2f, 0.2f, 1f);
            var backButton = btnObj.AddComponent<Button>();
            backButton.targetGraphic = btnImage;

            var btnTextObj = new GameObject("Text");
            btnTextObj.transform.SetParent(btnObj.transform, false);
            var btnTextRect = btnTextObj.AddComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.sizeDelta = Vector2.zero;
            var btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnText.text = "Back to Lobby";
            btnText.fontSize = 28;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.color = Color.white;

            return new MatchUI(root, statusLabel, timerLabel, playerCountLabel, btnObj, backButton);
        }

        public void SetTimeRemaining(float seconds)
        {
            var mins = Mathf.FloorToInt(seconds / 60f);
            var secs = Mathf.FloorToInt(seconds % 60f);
            _timerLabel.text = $"{mins:00}:{secs:00}";
        }

        public void SetPlayerCount(int count)
        {
            _playerCountLabel.text = $"Players: {count}";
        }

        public void SetStatus(string text)
        {
            _statusLabel.text = text;
        }

        public void ShowMatchEnded(MatchResult result)
        {
            _statusLabel.text = "Match Over";
            _timerLabel.text = "00:00";
            _backButtonObj.SetActive(true);
        }

        public void Destroy()
        {
            if (_root != null)
                UnityEngine.Object.Destroy(_root);
        }
    }
}
