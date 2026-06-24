using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClashUp.Client.Lobby.UI.Pages
{
    /// <summary>
    /// Tags a lobby button as the entry point for a specific game mode and gives it a distinct,
    /// mode-specific look (tint + label) at runtime so Classic and Elimination are easy to tell apart.
    /// <see cref="MathmakingPage"/> discovers these and queues for the button's <see cref="ModeId"/>.
    /// Adding a new mode = duplicate a button + set ModeId (label/color are derived, or override below).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class GameModeButton : MonoBehaviour
    {
        [SerializeField] private string _modeId = "default";

        [Tooltip("Optional label override. Empty = derived from the mode id.")]
        [SerializeField] private string _displayName = "";

        [Tooltip("Optional button tint override. Alpha 0 = use the built-in mode color.")]
        [SerializeField] private Color _accentColor = new Color(0f, 0f, 0f, 0f);

        public string ModeId => _modeId;

        private void Awake() => ApplyStyle();

        private void ApplyStyle()
        {
            Color tint = _accentColor.a > 0f ? _accentColor : DefaultColor(_modeId);
            string label = string.IsNullOrEmpty(_displayName) ? DefaultLabel(_modeId) : _displayName;

            // Recolor the button. The Button drives its graphic via the ColorBlock when using
            // ColorTint, so set both the image color and the color block to be safe across transitions.
            var image = GetComponent<Image>();
            if (image != null) image.color = tint;

            var button = GetComponent<Button>();
            if (button != null)
            {
                var cb = button.colors;
                cb.normalColor = tint;
                cb.highlightedColor = Scale(tint, 1.12f);
                cb.pressedColor = Scale(tint, 0.82f);
                cb.selectedColor = tint;
                cb.colorMultiplier = 1f;
                button.colors = cb;
            }

            // Clear, mode-named label. Reuse an existing label if present, else create one.
            var text = GetComponentInChildren<TMP_Text>(true);
            if (text == null) text = CreateLabel();
            text.text = label;
            text.color = Color.white;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = 16f;
            text.fontSizeMax = 46f;
        }

        private TMP_Text CreateLabel()
        {
            var go = new GameObject("ModeLabel");
            go.transform.SetParent(transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(12f, 6f);
            rect.offsetMax = new Vector2(-12f, -6f);
            return go.AddComponent<TextMeshProUGUI>();
        }

        private static Color Scale(Color c, float f) =>
            new Color(Mathf.Clamp01(c.r * f), Mathf.Clamp01(c.g * f), Mathf.Clamp01(c.b * f), c.a);

        private static Color DefaultColor(string modeId) => modeId switch
        {
            "elimination" => new Color(0.78f, 0.22f, 0.20f), // crimson
            "default" => new Color(0.20f, 0.55f, 0.32f),     // green
            _ => new Color(0.35f, 0.35f, 0.40f),
        };

        private static string DefaultLabel(string modeId) => modeId switch
        {
            "elimination" => "ELIMINATION",
            "default" => "CLASSIC",
            _ => modeId.ToUpperInvariant(),
        };
    }
}
