using UnityEngine;

namespace ClashUp.Client.Lobby.UI.Theme
{
    /// <summary>
    /// Color + sizing tokens for the lobby UI. Single source of truth for every screen. Values are at
    /// the 390-ref logical space; the lobby Canvas runs at 1080×1920, so multiply layout pixels by
    /// <see cref="Scale"/>. Palette sampled directly from the design mockup.
    /// </summary>
    public static class LobbyTheme
    {
        // Mock frame is 390×844; canvas is 1080×1920 → scale by height so a full screen fits.
        public const float Scale = 1920f / 844f; // ≈ 2.275
        public static float S(float mockPx) => mockPx * Scale;

        // ── Backgrounds (bright royal blue + radial glow baked by the builder) ─────
        public static readonly Color BgTop      = Hex("#2A47A6");
        public static readonly Color BgBottom   = Hex("#1A2B72");
        public static readonly Color BgTopHome  = Hex("#3354BE");
        public static readonly Color BgGlow     = Hex("#5072DC"); // upper-center radial highlight

        // ── Panels ────────────────────────────────────────────────────────────
        public static readonly Color PanelBlue        = Hex("#34499A");
        public static readonly Color PanelBlueBottom  = Hex("#293C82");
        public static readonly Color PanelBlueBorder  = Hex("#42579E");
        public static readonly Color PanelPurple      = Hex("#7A45C0");
        public static readonly Color PanelPurpleHi    = Hex("#A45ED0");
        public static readonly Color PanelPurpleBorder= Hex("#C98AE8");
        public static readonly Color PanelGold        = Hex("#2A4391");
        public static readonly Color PanelGoldBorder  = Hex("#FFD86B");

        // ── Insets / chips ────────────────────────────────────────────────────
        public static readonly Color InsetDark        = Hex("#0F1A45");
        public static readonly Color InsetDarkBorder  = Hex("#34499A");

        // ── Buttons ───────────────────────────────────────────────────────────
        public static readonly Color Gold        = Hex("#FFC227");
        public static readonly Color GoldBevel   = Hex("#C8861A");
        public static readonly Color Green       = Hex("#5BC23A");
        public static readonly Color GreenBevel  = Hex("#2E7D17");
        public static readonly Color Blue        = Hex("#3E63C8");
        public static readonly Color BlueBevel   = Hex("#243C86");
        public static readonly Color Dark        = Hex("#1E2E78");
        public static readonly Color DarkBevel   = Hex("#101B4A");
        public static readonly Color Red         = Hex("#E8484A");
        public static readonly Color RedBevel    = Hex("#8E2628");

        // ── Text ──────────────────────────────────────────────────────────────
        public static readonly Color TextWhite   = Hex("#FFFFFF");
        public static readonly Color TextLight    = Hex("#EAF0FF");
        public static readonly Color TextMuted    = Hex("#9CA8DC");
        public static readonly Color TextMutedNav  = Hex("#7E8AC4");
        public static readonly Color TextOnGold   = Hex("#5A3A0C");
        public static readonly Color TextGold     = Hex("#FFE9A8");
        public static readonly Color TextGoldHi   = Hex("#FFD66B");

        // ── Currency / misc ─────────────────────────────────────────────────────
        public static readonly Color Coin = Hex("#FFD24A");
        public static readonly Color Gem  = Hex("#46C8F0");
        public static readonly Color Online = Hex("#5FD06B");
        public static readonly Color Orange = Hex("#FF8A3D");

        // ── Rarity 1–5 ────────────────────────────────────────────────────────
        public static readonly Color[] Rarity =
        {
            Hex("#8FA0C8"), Hex("#6BD06B"), Hex("#4FA3E8"), Hex("#B26BE8"), Hex("#FFC53D"),
        };
        public static Color RarityColor(int rarity) => Rarity[Mathf.Clamp(rarity - 1, 0, Rarity.Length - 1)];

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }
}
