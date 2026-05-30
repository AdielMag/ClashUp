#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Linq;
using ClashUp.Client.Networking;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClashUp.Client.AppStarter
{
    public static class EnvironmentPickerUI
    {
        public static async UniTask<ServerEnvironment> ShowAndWaitAsync(EnvironmentConfig config)
        {
            var tcs = new UniTaskCompletionSource<ServerEnvironment>();
            var environments = config.GetAllEnvironments();

            var go = new GameObject("EnvironmentPicker");
            Object.DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();

            // Background
            var panel = new GameObject("Panel");
            panel.transform.SetParent(go.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0f, 0f, 0f, 0.85f);

            // Title
            var title = new GameObject("Title");
            title.transform.SetParent(panel.transform, false);
            var titleRect = title.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.sizeDelta = new Vector2(400f, 50f);
            titleRect.anchoredPosition = new Vector2(0f, 60f);
            var titleTmp = title.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "Select Environment";
            titleTmp.fontSize = 28;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = Color.white;

            // Dropdown
            var dropdownGo = new GameObject("Dropdown");
            dropdownGo.transform.SetParent(panel.transform, false);
            var dropdownRect = dropdownGo.AddComponent<RectTransform>();
            dropdownRect.anchorMin = new Vector2(0.5f, 0.5f);
            dropdownRect.anchorMax = new Vector2(0.5f, 0.5f);
            dropdownRect.sizeDelta = new Vector2(250f, 40f);
            dropdownRect.anchoredPosition = new Vector2(0f, 0f);

            var dropdownImg = dropdownGo.AddComponent<Image>();
            dropdownImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            var dropdown = dropdownGo.AddComponent<TMP_Dropdown>();
            dropdown.targetGraphic = dropdownImg;

            // Caption
            var captionGo = new GameObject("Label");
            captionGo.transform.SetParent(dropdownGo.transform, false);
            var captionRect = captionGo.AddComponent<RectTransform>();
            captionRect.anchorMin = Vector2.zero;
            captionRect.anchorMax = Vector2.one;
            captionRect.offsetMin = new Vector2(10f, 0f);
            captionRect.offsetMax = new Vector2(-25f, 0f);
            var captionTmp = captionGo.AddComponent<TextMeshProUGUI>();
            captionTmp.fontSize = 18;
            captionTmp.alignment = TextAlignmentOptions.Left;
            captionTmp.color = Color.white;
            dropdown.captionText = captionTmp;

            // Template
            var templateGo = new GameObject("Template");
            templateGo.transform.SetParent(dropdownGo.transform, false);
            var templateRect = templateGo.AddComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.sizeDelta = new Vector2(0f, 150f);
            templateGo.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 1f);
            var scrollRect = templateGo.AddComponent<ScrollRect>();

            var viewportGo = new GameObject("Viewport");
            viewportGo.transform.SetParent(templateGo.transform, false);
            var viewportRect = viewportGo.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewportGo.AddComponent<Image>();
            viewportGo.AddComponent<Mask>().showMaskGraphic = false;

            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRect = contentGo.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 40f);

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;

            // Item template
            var itemGo = new GameObject("Item");
            itemGo.transform.SetParent(contentGo.transform, false);
            var itemRect = itemGo.AddComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 0.5f);
            itemRect.anchorMax = new Vector2(1f, 0.5f);
            itemRect.sizeDelta = new Vector2(0f, 40f);
            var itemToggle = itemGo.AddComponent<Toggle>();

            var itemBg = new GameObject("Item Background");
            itemBg.transform.SetParent(itemGo.transform, false);
            var itemBgRect = itemBg.AddComponent<RectTransform>();
            itemBgRect.anchorMin = Vector2.zero;
            itemBgRect.anchorMax = Vector2.one;
            itemBgRect.offsetMin = Vector2.zero;
            itemBgRect.offsetMax = Vector2.zero;
            itemBg.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.25f, 1f);
            itemToggle.targetGraphic = itemBg.GetComponent<Image>();

            var itemLabel = new GameObject("Item Label");
            itemLabel.transform.SetParent(itemGo.transform, false);
            var itemLabelRect = itemLabel.AddComponent<RectTransform>();
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.offsetMin = new Vector2(10f, 0f);
            itemLabelRect.offsetMax = Vector2.zero;
            var itemTmp = itemLabel.AddComponent<TextMeshProUGUI>();
            itemTmp.fontSize = 18;
            itemTmp.alignment = TextAlignmentOptions.Left;
            itemTmp.color = Color.white;

            dropdown.itemText = itemTmp;
            templateGo.SetActive(false);

            // Populate options
            dropdown.options = environments.Select(e => new TMP_Dropdown.OptionData(e.ToString())).ToList();
            var currentIndex = System.Array.IndexOf(environments, config.Current);
            dropdown.value = currentIndex >= 0 ? currentIndex : 0;
            dropdown.RefreshShownValue();

            // Confirm button
            var btnGo = new GameObject("ConfirmBtn");
            btnGo.transform.SetParent(panel.transform, false);
            var btnRect = btnGo.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.5f);
            btnRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnRect.sizeDelta = new Vector2(150f, 40f);
            btnRect.anchoredPosition = new Vector2(0f, -55f);

            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.13f, 0.47f, 0.84f, 1f);

            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(() => tcs.TrySetResult(environments[dropdown.value]));

            var btnLabel = new GameObject("Label");
            btnLabel.transform.SetParent(btnGo.transform, false);
            var btnLabelRect = btnLabel.AddComponent<RectTransform>();
            btnLabelRect.anchorMin = Vector2.zero;
            btnLabelRect.anchorMax = Vector2.one;
            btnLabelRect.offsetMin = Vector2.zero;
            btnLabelRect.offsetMax = Vector2.zero;
            var btnTmp = btnLabel.AddComponent<TextMeshProUGUI>();
            btnTmp.text = "Confirm";
            btnTmp.fontSize = 20;
            btnTmp.alignment = TextAlignmentOptions.Center;
            btnTmp.color = Color.white;

            var result = await tcs.Task;
            Object.Destroy(go);
            return result;
        }
    }
}
#endif
