using System;

using UnityEngine;

namespace ClashUp.Client.Lobby.Data
{
    /// <summary>
    /// Runtime-loadable sprite library for the lobby UI. Lives in a Resources folder so overlays built
    /// at runtime (e.g. Champion Select) can fetch sprites without AssetDatabase. Populated from the
    /// imported sprites by the editor builder (ClashUp ▸ Build Lobby UI).
    /// </summary>
    [CreateAssetMenu(menuName = "ClashUp/Lobby UI Assets", fileName = "LobbyUiAssets")]
    public sealed class LobbyUiAssets : ScriptableObject
    {
        [Serializable]
        public struct NamedSprite
        {
            public string name;
            public Sprite sprite;
        }

        [Header("Panels / buttons (9-slice)")]
        public Sprite panelBlue;
        public Sprite panelGold;
        public Sprite panelPurple;
        public Sprite pillDark;
        public Sprite buttonGold;
        public Sprite buttonGreen;
        public Sprite buttonBlue;
        public Sprite buttonDark;

        [Header("Icons")]
        public Sprite back;
        public Sprite bolt;
        public Sprite trophy;
        public Sprite swords;
        public Sprite star;
        public Sprite background;

        [Header("Heroes")]
        public NamedSprite[] heroes;

        public Sprite GetHero(string name)
        {
            if (heroes == null || heroes.Length == 0) return null;
            if (!string.IsNullOrEmpty(name))
            {
                var n = name.ToLowerInvariant();
                foreach (var c in heroes)
                    if (!string.IsNullOrEmpty(c.name) && c.name.ToLowerInvariant() == n)
                        return c.sprite;
            }
            return heroes[0].sprite;
        }

        public Sprite GetHeroByIndex(int i)
        {
            if (heroes == null || heroes.Length == 0) return null;
            return heroes[((i % heroes.Length) + heroes.Length) % heroes.Length].sprite;
        }

        public static LobbyUiAssets Load() => Resources.Load<LobbyUiAssets>("LobbyUiAssets");
    }
}
