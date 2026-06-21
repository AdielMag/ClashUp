using System;
using System.Collections.Generic;

using ClashUp.Shared.Characters;
using ClashUp.Shared.MessagePackObjects;

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClashUp.Client.Lobby
{
    /// <summary>
    /// Generic pre-matchmaking character picker. Lists the available characters and reports the
    /// player's choice via <see cref="OnConfirmed"/>. Built programmatically as a full-screen
    /// overlay so it works across all game modes with no scene-specific wiring.
    /// </summary>
    public sealed class CharacterSelectUI
    {
        private readonly GameObject _root;
        private readonly List<(CharacterId id, Image bg)> _cards = new();

        public event Action<CharacterId> OnConfirmed;

        private CharacterId _selected;

        private static readonly Color CardNormal = new Color(0.18f, 0.18f, 0.22f, 1f);
        private static readonly Color CardSelected = new Color(0.2f, 0.45f, 0.8f, 1f);

        private CharacterSelectUI(GameObject root) => _root = root;

        public void Destroy()
        {
            if (_root != null)
                UnityEngine.Object.Destroy(_root);
        }

        public static CharacterSelectUI Create(CharactersConfig config, CharacterId current)
        {
            var root = new GameObject("CharacterSelectUI");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            var bg = NewChild(root.transform, "Background");
            var bgRect = bg.AddComponent<RectTransform>();
            Stretch(bgRect);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.05f, 0.05f, 0.08f, 0.96f);

            var title = NewChild(root.transform, "Title");
            var titleRect = title.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -60f);
            titleRect.sizeDelta = new Vector2(900f, 90f);
            var titleText = title.AddComponent<TextMeshProUGUI>();
            titleText.text = "SELECT CHARACTER";
            titleText.fontSize = 60;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;
            titleText.fontStyle = FontStyles.Bold;

            var ui = new CharacterSelectUI(root) { _selected = current };

            var row = NewChild(root.transform, "CardRow");
            var rowRect = row.AddComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.5f, 0.5f);
            rowRect.anchorMax = new Vector2(0.5f, 0.5f);
            rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.anchoredPosition = new Vector2(0f, 40f);
            rowRect.sizeDelta = new Vector2(1400f, 460f);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 40f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var characters = config?.Characters ?? CharactersConfig.Default.Characters;
            foreach (var ch in characters)
                ui.BuildCard(row.transform, ch);

            ui.Highlight(ui._selected);

            var confirm = NewChild(root.transform, "ConfirmButton");
            var confirmRect = confirm.AddComponent<RectTransform>();
            confirmRect.anchorMin = new Vector2(0.5f, 0f);
            confirmRect.anchorMax = new Vector2(0.5f, 0f);
            confirmRect.pivot = new Vector2(0.5f, 0f);
            confirmRect.anchoredPosition = new Vector2(0f, 80f);
            confirmRect.sizeDelta = new Vector2(320f, 90f);
            var confirmImg = confirm.AddComponent<Image>();
            confirmImg.color = new Color(0.2f, 0.6f, 0.2f, 1f);
            var confirmBtn = confirm.AddComponent<Button>();
            confirmBtn.targetGraphic = confirmImg;

            var confirmTextObj = NewChild(confirm.transform, "Text");
            var confirmTextRect = confirmTextObj.AddComponent<RectTransform>();
            Stretch(confirmTextRect);
            var confirmText = confirmTextObj.AddComponent<TextMeshProUGUI>();
            confirmText.text = "CONFIRM";
            confirmText.fontSize = 38;
            confirmText.alignment = TextAlignmentOptions.Center;
            confirmText.color = Color.white;
            confirmText.fontStyle = FontStyles.Bold;
            confirmBtn.onClick.AddListener(() => ui.OnConfirmed?.Invoke(ui._selected));

            return ui;
        }

        private void BuildCard(Transform parent, CharacterDefinition ch)
        {
            var card = NewChild(parent, $"Card_{ch.Id.Value}");
            var rect = card.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(300f, 420f);
            var img = card.AddComponent<Image>();
            img.color = CardNormal;
            var btn = card.AddComponent<Button>();
            btn.targetGraphic = img;

            var id = ch.Id;
            btn.onClick.AddListener(() => Highlight(id));

            var info = NewChild(card.transform, "Info");
            var infoRect = info.AddComponent<RectTransform>();
            Stretch(infoRect);
            infoRect.offsetMin = new Vector2(14f, 14f);
            infoRect.offsetMax = new Vector2(-14f, -14f);
            var text = info.AddComponent<TextMeshProUGUI>();
            text.text =
                $"<b>{ch.DisplayName}</b>\n\n" +
                $"HP {ch.BaseStats.MaxHealth:0}\n" +
                $"Speed {ch.BaseStats.MoveSpeed:0.#}";
            text.fontSize = 30;
            text.alignment = TextAlignmentOptions.Top;
            text.color = Color.white;

            _cards.Add((ch.Id, img));
        }

        private void Highlight(CharacterId id)
        {
            _selected = id;
            foreach (var (cardId, bg) in _cards)
                bg.color = cardId.Value == id.Value ? CardSelected : CardNormal;
        }

        private static GameObject NewChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }
    }
}
