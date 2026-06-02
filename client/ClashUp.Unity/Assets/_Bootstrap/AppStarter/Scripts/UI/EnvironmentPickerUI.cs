#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using ClashUp.Client.Networking;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ClashUp.Client.AppStarter
{
    public static class EnvironmentPickerUI
    {
        public static async UniTask<ServerEnvironment> ShowAndWaitAsync(EnvironmentConfig config)
        {
            var tcs = new UniTaskCompletionSource<ServerEnvironment>();
            var environments = config.GetAllEnvironments();

            // Ensure EventSystem exists
            if (EventSystem.current == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<StandaloneInputModule>();
                Object.DontDestroyOnLoad(esGo);
            }

            var go = new GameObject("EnvironmentPicker");
            Object.DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            go.AddComponent<GraphicRaycaster>();

            // Background panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(go.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

            // Title
            var title = CreateText(panel.transform, "Select Environment", 28, TextAnchor.MiddleCenter);
            var titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.sizeDelta = new Vector2(400f, 50f);
            titleRect.anchoredPosition = new Vector2(0f, 80f);

            // Dropdown
            var dropdownGo = CreateDropdown(panel.transform, environments, config.Current);
            var dropdownRect = dropdownGo.GetComponent<RectTransform>();
            dropdownRect.anchorMin = new Vector2(0.5f, 0.5f);
            dropdownRect.anchorMax = new Vector2(0.5f, 0.5f);
            dropdownRect.sizeDelta = new Vector2(250f, 40f);
            dropdownRect.anchoredPosition = new Vector2(0f, 20f);

            var dropdown = dropdownGo.GetComponent<Dropdown>();
            var selectedIndex = System.Array.IndexOf(environments, config.Current);

            dropdown.onValueChanged.AddListener(i => selectedIndex = i);

            // Confirm button
            var btnGo = new GameObject("ConfirmBtn");
            btnGo.transform.SetParent(panel.transform, false);
            var btnRect = btnGo.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.5f);
            btnRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnRect.sizeDelta = new Vector2(160f, 40f);
            btnRect.anchoredPosition = new Vector2(0f, -40f);

            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.13f, 0.47f, 0.84f, 1f);

            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(() => tcs.TrySetResult(environments[selectedIndex]));

            CreateText(btnGo.transform, "Confirm", 20, TextAnchor.MiddleCenter);

            var result = await tcs.Task;
            Object.Destroy(go);
            return result;
        }

        private static GameObject CreateDropdown(Transform parent, ServerEnvironment[] environments, ServerEnvironment current)
        {
            var ddGo = new GameObject("Dropdown");
            ddGo.transform.SetParent(parent, false);
            ddGo.AddComponent<RectTransform>();

            var ddImg = ddGo.AddComponent<Image>();
            ddImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            var dropdown = ddGo.AddComponent<Dropdown>();

            // Label (shows selected option)
            var labelGo = CreateText(ddGo.transform, "", 18, TextAnchor.MiddleLeft);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10f, 0f);
            labelRect.offsetMax = new Vector2(-30f, 0f);
            dropdown.captionText = labelGo.GetComponent<Text>();

            // Template (dropdown list)
            var templateGo = new GameObject("Template");
            templateGo.transform.SetParent(ddGo.transform, false);
            var templateRect = templateGo.AddComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.sizeDelta = new Vector2(0f, 150f);
            templateRect.anchoredPosition = Vector2.zero;

            var templateImg = templateGo.AddComponent<Image>();
            templateImg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            templateGo.AddComponent<ScrollRect>();
            templateGo.SetActive(false);

            // Viewport
            var viewportGo = new GameObject("Viewport");
            viewportGo.transform.SetParent(templateGo.transform, false);
            var viewportRect = viewportGo.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewportGo.AddComponent<Image>().color = Color.clear;
            var mask = viewportGo.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var scrollRect = templateGo.GetComponent<ScrollRect>();
            scrollRect.viewport = viewportRect;

            // Content
            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRect = contentGo.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 40f);

            scrollRect.content = contentRect;

            // Item template
            var itemGo = new GameObject("Item");
            itemGo.transform.SetParent(contentGo.transform, false);
            var itemRect = itemGo.AddComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 0.5f);
            itemRect.anchorMax = new Vector2(1f, 0.5f);
            itemRect.sizeDelta = new Vector2(0f, 40f);

            var itemImg = itemGo.AddComponent<Image>();
            itemImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            var toggle = itemGo.AddComponent<Toggle>();
            toggle.targetGraphic = itemImg;
            var toggleColors = toggle.colors;
            toggleColors.normalColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            toggleColors.highlightedColor = new Color(0.35f, 0.35f, 0.35f, 1f);
            toggleColors.selectedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            toggle.colors = toggleColors;

            var itemLabelGo = CreateText(itemGo.transform, "", 18, TextAnchor.MiddleLeft);
            var itemLabelRect = itemLabelGo.GetComponent<RectTransform>();
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.offsetMin = new Vector2(10f, 0f);
            itemLabelRect.offsetMax = Vector2.zero;

            dropdown.itemText = itemLabelGo.GetComponent<Text>();
            dropdown.template = templateRect;

            // Populate options
            dropdown.options = environments.Select(e => new Dropdown.OptionData(e.ToString())).ToList();
            dropdown.value = System.Array.IndexOf(environments, current);
            dropdown.RefreshShownValue();

            return ddGo;
        }

        private static GameObject CreateText(Transform parent, string text, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var txt = go.AddComponent<Text>();
            txt.text = text;
            txt.fontSize = fontSize;
            txt.alignment = alignment;
            txt.color = Color.white;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                       ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            return go;
        }
    }
}
#endif
