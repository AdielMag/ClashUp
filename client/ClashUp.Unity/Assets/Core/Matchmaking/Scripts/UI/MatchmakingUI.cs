using System;

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClashUp.Client.Matchmaking
{
    public sealed class MatchmakingUI
    {
        private readonly GameObject _root;
        private readonly TMP_Text _statusLabel;
        private readonly TMP_Text _timerLabel;
        private readonly Button _cancelButton;

        public event Action OnCancelClicked;

        private MatchmakingUI(GameObject root, TMP_Text statusLabel, TMP_Text timerLabel, Button cancelButton)
        {
            _root = root;
            _statusLabel = statusLabel;
            _timerLabel = timerLabel;
            _cancelButton = cancelButton;
            _cancelButton.onClick.AddListener(() => OnCancelClicked?.Invoke());
        }

        public static MatchmakingUI Create()
        {
            var root = new GameObject("MatchmakingUI");

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();

            // Title
            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(root.transform, false);
            var titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -80f);
            titleRect.sizeDelta = new Vector2(600f, 60f);
            var titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "FINDING MATCH";
            titleText.fontSize = 48;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;
            titleText.fontStyle = FontStyles.Bold;

            // Status label
            var statusObj = new GameObject("StatusLabel");
            statusObj.transform.SetParent(root.transform, false);
            var statusRect = statusObj.AddComponent<RectTransform>();
            statusRect.anchoredPosition = new Vector2(0f, 40f);
            statusRect.sizeDelta = new Vector2(600f, 50f);
            var statusLabel = statusObj.AddComponent<TextMeshProUGUI>();
            statusLabel.text = "Searching for opponents...";
            statusLabel.fontSize = 28;
            statusLabel.alignment = TextAlignmentOptions.Center;
            statusLabel.color = new Color(0.8f, 0.8f, 0.8f, 1f);

            // Timer label
            var timerObj = new GameObject("TimerLabel");
            timerObj.transform.SetParent(root.transform, false);
            var timerRect = timerObj.AddComponent<RectTransform>();
            timerRect.anchoredPosition = new Vector2(0f, -20f);
            timerRect.sizeDelta = new Vector2(300f, 40f);
            var timerLabel = timerObj.AddComponent<TextMeshProUGUI>();
            timerLabel.text = "0:00";
            timerLabel.fontSize = 32;
            timerLabel.alignment = TextAlignmentOptions.Center;
            timerLabel.color = new Color(0.6f, 0.6f, 0.6f, 1f);

            // Cancel button
            var btnObj = new GameObject("CancelButton");
            btnObj.transform.SetParent(root.transform, false);
            var btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchoredPosition = new Vector2(0f, -120f);
            btnRect.sizeDelta = new Vector2(240f, 60f);
            var btnImage = btnObj.AddComponent<Image>();
            btnImage.color = new Color(0.6f, 0.2f, 0.2f, 1f);
            var cancelButton = btnObj.AddComponent<Button>();
            cancelButton.targetGraphic = btnImage;

            var btnTextObj = new GameObject("Text");
            btnTextObj.transform.SetParent(btnObj.transform, false);
            var btnTextRect = btnTextObj.AddComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.sizeDelta = Vector2.zero;
            var btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnText.text = "Cancel";
            btnText.fontSize = 28;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.color = Color.white;

            return new MatchmakingUI(root, statusLabel, timerLabel, cancelButton);
        }

        public void SetStatus(string text) => _statusLabel.text = text;

        public void SetTimer(float elapsedSeconds)
        {
            var minutes = (int)(elapsedSeconds / 60f);
            var seconds = (int)(elapsedSeconds % 60f);
            _timerLabel.text = $"{minutes}:{seconds:D2}";
        }

        public void SetInteractable(bool interactable) => _cancelButton.interactable = interactable;

        public void Destroy()
        {
            if (_root != null)
                UnityEngine.Object.Destroy(_root);
        }
    }
}
