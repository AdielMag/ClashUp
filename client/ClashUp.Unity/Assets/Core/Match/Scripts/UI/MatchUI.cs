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
        private readonly GameObject _leaveButtonObj;
        private readonly Button _leaveButton;
        private readonly GameObject _confirmOverlay;

        public event Action OnBackToLobbyClicked;

        private MatchUI(GameObject root, TMP_Text statusLabel, TMP_Text timerLabel,
            TMP_Text playerCountLabel, GameObject backButtonObj, Button backButton,
            GameObject leaveButtonObj, Button leaveButton, GameObject confirmOverlay,
            Button confirmButton, Button cancelButton)
        {
            _root = root;
            _statusLabel = statusLabel;
            _timerLabel = timerLabel;
            _playerCountLabel = playerCountLabel;
            _backButtonObj = backButtonObj;
            _backButton = backButton;
            _leaveButtonObj = leaveButtonObj;
            _leaveButton = leaveButton;
            _confirmOverlay = confirmOverlay;

            _backButton.onClick.AddListener(() => OnBackToLobbyClicked?.Invoke());
            _backButtonObj.SetActive(false);

            // Leave button opens the confirmation; confirming reuses the back-to-lobby path
            // (which forfeits via MatchSessionRunner.Dispose → MatchSession.LeaveAsync).
            _leaveButton.onClick.AddListener(() => _confirmOverlay.SetActive(true));
            cancelButton.onClick.AddListener(() => _confirmOverlay.SetActive(false));
            confirmButton.onClick.AddListener(() =>
            {
                _confirmOverlay.SetActive(false);
                OnBackToLobbyClicked?.Invoke();
            });
            _confirmOverlay.SetActive(false);
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

            // Timer label (below status)
            var timerObj = new GameObject("TimerLabel");
            timerObj.transform.SetParent(root.transform, false);
            var timerRect = timerObj.AddComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0.5f, 1f);
            timerRect.anchorMax = new Vector2(0.5f, 1f);
            timerRect.pivot = new Vector2(0.5f, 1f);
            timerRect.anchoredPosition = new Vector2(0f, -100f);
            timerRect.sizeDelta = new Vector2(300f, 65f);
            var timerLabel = timerObj.AddComponent<TextMeshProUGUI>();
            timerLabel.text = "--:--";
            timerLabel.fontSize = 48;
            timerLabel.alignment = TextAlignmentOptions.Center;
            timerLabel.color = Color.white;

            // Player count label (below timer)
            var playerObj = new GameObject("PlayerCountLabel");
            playerObj.transform.SetParent(root.transform, false);
            var playerRect = playerObj.AddComponent<RectTransform>();
            playerRect.anchorMin = new Vector2(0.5f, 1f);
            playerRect.anchorMax = new Vector2(0.5f, 1f);
            playerRect.pivot = new Vector2(0.5f, 1f);
            playerRect.anchoredPosition = new Vector2(0f, -175f);
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

            // Leave Match button (top-right, visible during active play)
            var (leaveObj, leaveButton) = CreateButton(root.transform, "LeaveMatchButton",
                "Leave Match", new Color(0.6f, 0.2f, 0.2f, 1f), new Vector2(220f, 60f));
            var leaveRect = (RectTransform)leaveObj.transform;
            leaveRect.anchorMin = new Vector2(1f, 1f);
            leaveRect.anchorMax = new Vector2(1f, 1f);
            leaveRect.pivot = new Vector2(1f, 1f);
            leaveRect.anchoredPosition = new Vector2(-30f, -30f);

            // Confirmation overlay (hidden until the Leave button is pressed)
            var overlay = new GameObject("LeaveConfirmOverlay");
            overlay.transform.SetParent(root.transform, false);
            var overlayRect = overlay.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.sizeDelta = Vector2.zero;
            overlayRect.anchoredPosition = Vector2.zero;
            var dim = overlay.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.85f);

            var panel = new GameObject("Panel");
            panel.transform.SetParent(overlay.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(720f, 360f);
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.12f, 0.12f, 0.14f, 1f);

            var promptObj = new GameObject("Prompt");
            promptObj.transform.SetParent(panel.transform, false);
            var promptRect = promptObj.AddComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0.5f, 1f);
            promptRect.anchorMax = new Vector2(0.5f, 1f);
            promptRect.pivot = new Vector2(0.5f, 1f);
            promptRect.anchoredPosition = new Vector2(0f, -50f);
            promptRect.sizeDelta = new Vector2(640f, 160f);
            var promptText = promptObj.AddComponent<TextMeshProUGUI>();
            promptText.text = "Leave the match?\nThis will forfeit and count as a loss.";
            promptText.fontSize = 32;
            promptText.alignment = TextAlignmentOptions.Center;
            promptText.color = Color.white;

            var (cancelObj, cancelButton) = CreateButton(panel.transform, "CancelButton",
                "Cancel", new Color(0.25f, 0.25f, 0.3f, 1f), new Vector2(260f, 80f));
            var cancelRect = (RectTransform)cancelObj.transform;
            cancelRect.anchorMin = new Vector2(0.5f, 0f);
            cancelRect.anchorMax = new Vector2(0.5f, 0f);
            cancelRect.pivot = new Vector2(0.5f, 0f);
            cancelRect.anchoredPosition = new Vector2(-150f, 40f);

            var (confirmObj, confirmButton) = CreateButton(panel.transform, "ConfirmLeaveButton",
                "Leave", new Color(0.7f, 0.18f, 0.18f, 1f), new Vector2(260f, 80f));
            var confirmRect = (RectTransform)confirmObj.transform;
            confirmRect.anchorMin = new Vector2(0.5f, 0f);
            confirmRect.anchorMax = new Vector2(0.5f, 0f);
            confirmRect.pivot = new Vector2(0.5f, 0f);
            confirmRect.anchoredPosition = new Vector2(150f, 40f);

            return new MatchUI(root, statusLabel, timerLabel, playerCountLabel, btnObj, backButton,
                leaveObj, leaveButton, overlay, confirmButton, cancelButton);
        }

        private static (GameObject, Button) CreateButton(Transform parent, string name,
            string label, Color color, Vector2 size)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            var img = obj.AddComponent<Image>();
            img.color = color;
            var button = obj.AddComponent<Button>();
            button.targetGraphic = img;

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(obj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 28;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            return (obj, button);
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
            // Match is already over — no forfeit needed; the centered back button takes over.
            _confirmOverlay.SetActive(false);
            _leaveButtonObj.SetActive(false);
            _backButtonObj.SetActive(true);
        }

        public void Destroy()
        {
            if (_root != null)
                UnityEngine.Object.Destroy(_root);
        }
    }
}
