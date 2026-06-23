using System;
using System.Collections.Generic;

using ClashUp.Client.Lobby.Data;
using ClashUp.Client.Lobby.UI.Theme;
using ClashUp.Shared.Characters;
using ClashUp.Shared.MessagePackObjects;

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ClashUp.Client.Lobby
{
    /// <summary>
    /// "Champion Select" — pre-matchmaking champion picker, restyled to the mockup
    /// (spotlight stage + stat chips + ability card + champion carousel + Random/BATTLE footer).
    /// Still driven by the wire <see cref="CharactersConfig"/> and reports the choice via
    /// <see cref="OnConfirmed"/>; visuals only — no gameplay logic. Built as a full-screen overlay.
    /// </summary>
    public sealed class CharacterSelectUI
    {
        public event Action<CharacterId> OnConfirmed;
        public event Action OnBackClicked;

        private readonly GameObject _root;
        private readonly LobbyUiAssets _assets;
        private readonly List<CharacterDefinition> _chars = new();
        private readonly List<Image> _tileFrames = new();

        private CharacterId _selected;
        private int _selectedIndex;

        // spotlight elements refreshed on selection
        private Image _spotImg;
        private readonly Image[] _starImgs = new Image[5];
        private TMP_Text _nameText, _hpVal, _atkVal, _spdVal, _abilityName, _abilityDesc;

        private static float S(float p) => LobbyTheme.S(p);

        private CharacterSelectUI(GameObject root, LobbyUiAssets assets)
        {
            _root = root;
            _assets = assets;
        }

        public void Destroy()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root);
        }

        public static CharacterSelectUI Create(CharactersConfig config, CharacterId current)
        {
            var root = new GameObject("ChampionSelectUI");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            var ui = new CharacterSelectUI(root, LobbyUiAssets.Load()) { _selected = current };

            // gradient backdrop
            var bg = NewUI("Background", root.transform);
            Stretch(bg.GetComponent<RectTransform>());
            var bgImg = bg.AddComponent<Image>();
            if (ui._assets != null && ui._assets.background != null) { bgImg.sprite = ui._assets.background; bgImg.color = Color.white; }
            else bgImg.color = LobbyTheme.Hex("#1A2B72");

            var chars = (config?.Characters != null && config.Characters.Count > 0)
                ? config.Characters : CharactersConfig.Default.Characters;
            ui._chars.AddRange(chars);

            ui.BuildHeader();
            ui.BuildSpotlight();
            ui.BuildCarousel();
            ui.BuildFooter();

            int idx = ui._chars.FindIndex(c => c.Id.Value == current.Value);
            ui.Select(idx < 0 ? 0 : idx);
            return ui;
        }

        // ── header ────────────────────────────────────────────────────────────
        private void BuildHeader()
        {
            var back = Button("Back", _root.transform, _assets ? _assets.buttonDark : null, LobbyTheme.Dark, LobbyTheme.DarkBevel);
            var brt = back.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(0, 1); brt.pivot = new Vector2(0, 1);
            brt.anchoredPosition = new Vector2(S(14), -S(44)); brt.sizeDelta = new Vector2(S(56), S(56));
            if (_assets && _assets.back) AddIcon(back.transform, _assets.back, Color.white, S(28));
            else AddLabel(back.transform, "<", S(24), Color.white, TextAlignmentOptions.Center, true);
            back.onClick.AddListener(() => OnBackClicked?.Invoke());

            var title = AddLabel(_root.transform, "CHOOSE YOUR CHAMPION", S(15), LobbyTheme.TextWhite, TextAlignmentOptions.Center, true);
            title.enableWordWrapping = false; title.enableAutoSizing = true; title.fontSizeMin = S(11); title.fontSizeMax = S(15);
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1); trt.pivot = new Vector2(0.5f, 1);
            trt.offsetMin = new Vector2(S(80), -S(96)); trt.offsetMax = new Vector2(-S(140), -S(48));

            var chip = Panel("Stake", _root.transform, _assets ? _assets.pillDark : null, LobbyTheme.InsetDark);
            var crt = chip.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(1, 1); crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(1, 1);
            crt.anchoredPosition = new Vector2(-S(14), -S(48)); crt.sizeDelta = new Vector2(S(110), S(48));
            if (_assets && _assets.trophy)
            {
                var ti = AddIcon(chip.transform, _assets.trophy, Color.white, S(22));
                ti.rectTransform.anchorMin = new Vector2(0, 0.5f); ti.rectTransform.anchorMax = new Vector2(0, 0.5f);
                ti.rectTransform.pivot = new Vector2(0, 0.5f); ti.rectTransform.anchoredPosition = new Vector2(S(10), 0);
            }
            var stakeTxt = AddLabel(chip.transform, "+30", S(15), LobbyTheme.TextGold, TextAlignmentOptions.Center, true);
            var srt2 = stakeTxt.rectTransform; srt2.anchorMin = new Vector2(0, 0); srt2.anchorMax = new Vector2(1, 1);
            srt2.offsetMin = new Vector2(S(30), 0); srt2.offsetMax = Vector2.zero;
        }

        // ── spotlight ───────────────────────────────────────────────────────────
        private void BuildSpotlight()
        {
            var stage = NewUI("Spotlight", _root.transform);
            var srt = stage.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0, 1); srt.anchorMax = new Vector2(1, 1); srt.pivot = new Vector2(0.5f, 1);
            srt.anchoredPosition = new Vector2(0, -S(120)); srt.sizeDelta = new Vector2(0, S(420));

            var glow = NewUI("Glow", stage.transform);
            var grt = glow.GetComponent<RectTransform>();
            grt.anchorMin = new Vector2(0.5f, 1); grt.anchorMax = new Vector2(0.5f, 1); grt.pivot = new Vector2(0.5f, 1);
            grt.sizeDelta = new Vector2(S(300), S(300)); grt.anchoredPosition = new Vector2(0, -S(10));
            var glowImg = glow.AddComponent<Image>();
            glowImg.sprite = LobbyUiAssets.Load() != null ? LobbyUiAssets.Load().pillDark : null;
            glowImg.type = glowImg.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            glowImg.color = new Color(LobbyTheme.Gold.r, LobbyTheme.Gold.g, LobbyTheme.Gold.b, 0.10f);

            _spotImg = NewUI("Champion", stage.transform).AddComponent<Image>();
            _spotImg.preserveAspect = true;
            var prt = _spotImg.rectTransform;
            prt.anchorMin = new Vector2(0.5f, 1); prt.anchorMax = new Vector2(0.5f, 1); prt.pivot = new Vector2(0.5f, 1);
            prt.sizeDelta = new Vector2(S(238), S(238)); prt.anchoredPosition = new Vector2(0, -S(20));

            _nameText = AddLabel(stage.transform, "", S(24), LobbyTheme.TextWhite, TextAlignmentOptions.Center, true);
            var nrt = _nameText.rectTransform;
            nrt.anchorMin = new Vector2(0.5f, 1); nrt.anchorMax = new Vector2(0.5f, 1); nrt.pivot = new Vector2(0.5f, 1);
            nrt.anchoredPosition = new Vector2(0, -S(264)); nrt.sizeDelta = new Vector2(S(360), S(34));

            var starRow = NewUI("Stars", stage.transform);
            var strt = starRow.GetComponent<RectTransform>();
            strt.anchorMin = new Vector2(0.5f, 1); strt.anchorMax = new Vector2(0.5f, 1); strt.pivot = new Vector2(0.5f, 1);
            strt.anchoredPosition = new Vector2(0, -S(298)); strt.sizeDelta = new Vector2(S(200), S(26));
            var hlg = starRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = S(4); hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false; hlg.childControlHeight = false; hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            for (int i = 0; i < 5; i++)
            {
                _starImgs[i] = AddIcon(starRow.transform, _assets ? _assets.star : null, LobbyTheme.Coin, S(22));
                _starImgs[i].rectTransform.anchorMin = _starImgs[i].rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            }

            // stat chips
            var chips = NewUI("Chips", stage.transform);
            var chrt = chips.GetComponent<RectTransform>();
            chrt.anchorMin = new Vector2(0.5f, 1); chrt.anchorMax = new Vector2(0.5f, 1); chrt.pivot = new Vector2(0.5f, 1);
            chrt.anchoredPosition = new Vector2(0, -S(330)); chrt.sizeDelta = new Vector2(S(330), S(64));
            _hpVal  = StatChip(chips.transform, "HP",  -1, LobbyTheme.Green);
            _atkVal = StatChip(chips.transform, "ATK",  0, LobbyTheme.Hex("#FF8A3D"));
            _spdVal = StatChip(chips.transform, "SPD",  1, LobbyTheme.Gem);

            // ability card
            var card = Panel("Ability", stage.transform, _assets ? _assets.panelBlue : null, LobbyTheme.PanelBlue);
            var cardRt = card.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 1); cardRt.anchorMax = new Vector2(0.5f, 1); cardRt.pivot = new Vector2(0.5f, 1);
            cardRt.anchoredPosition = new Vector2(0, -S(402)); cardRt.sizeDelta = new Vector2(S(360), S(74));
            if (_assets && _assets.bolt)
            {
                var icon = AddIcon(card.transform, _assets.bolt, LobbyTheme.TextGold, S(34));
                icon.rectTransform.anchorMin = new Vector2(0, 0.5f); icon.rectTransform.anchorMax = new Vector2(0, 0.5f);
                icon.rectTransform.pivot = new Vector2(0, 0.5f); icon.rectTransform.anchoredPosition = new Vector2(S(12), 0);
            }
            _abilityName = AddLabel(card.transform, "", S(14), LobbyTheme.TextGold, TextAlignmentOptions.TopLeft, true);
            Place(_abilityName.gameObject, card, S(10), S(22), S(54));
            _abilityDesc = AddLabel(card.transform, "", S(11), LobbyTheme.TextLight, TextAlignmentOptions.TopLeft, false);
            Place(_abilityDesc.gameObject, card, S(34), S(34), S(54));
        }

        private TMP_Text StatChip(Transform parent, string label, int slot, Color valueColor)
        {
            float w = S(104), gap = S(9);
            var chip = Panel("Chip_" + label, parent, _assets ? _assets.pillDark : null, LobbyTheme.InsetDark);
            var rt = chip.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, S(60)); rt.anchoredPosition = new Vector2(slot * (w + gap), 0);
            var lbl = AddLabel(chip.transform, label, S(11), LobbyTheme.TextMuted, TextAlignmentOptions.Center, true);
            var lrt = lbl.rectTransform; lrt.anchorMin = new Vector2(0, 1); lrt.anchorMax = new Vector2(1, 1); lrt.pivot = new Vector2(0.5f, 1);
            lrt.anchoredPosition = new Vector2(0, -S(8)); lrt.sizeDelta = new Vector2(0, S(18));
            var val = AddLabel(chip.transform, "", S(18), valueColor, TextAlignmentOptions.Center, true);
            var vrt = val.rectTransform; vrt.anchorMin = new Vector2(0, 0); vrt.anchorMax = new Vector2(1, 0); vrt.pivot = new Vector2(0.5f, 0);
            vrt.anchoredPosition = new Vector2(0, S(8)); vrt.sizeDelta = new Vector2(0, S(26));
            return val;
        }

        // ── carousel ────────────────────────────────────────────────────────────
        private void BuildCarousel()
        {
            var scrollGO = NewUI("Carousel", _root.transform);
            var srt = scrollGO.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(1, 0); srt.pivot = new Vector2(0.5f, 0);
            srt.anchoredPosition = new Vector2(0, S(150)); srt.sizeDelta = new Vector2(0, S(110));
            scrollGO.AddComponent<RectMask2D>();
            var scroll = scrollGO.AddComponent<ScrollRect>();
            scroll.horizontal = true; scroll.vertical = false; scroll.movementType = ScrollRect.MovementType.Elastic;

            var content = NewUI("Content", scrollGO.transform);
            var crt = content.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 0); crt.anchorMax = new Vector2(0, 1); crt.pivot = new Vector2(0, 0.5f);
            var hlg = content.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = S(12); hlg.childAlignment = TextAnchor.MiddleLeft; hlg.padding = new RectOffset((int)S(20), (int)S(20), 0, 0);
            hlg.childControlWidth = false; hlg.childControlHeight = false; hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = crt;

            for (int i = 0; i < _chars.Count; i++)
            {
                int idx = i;
                var tile = Panel("Tile_" + _chars[i].Id.Value, content.transform, _assets ? _assets.panelBlue : null, LobbyTheme.PanelBlue);
                var trt = tile.GetComponent<RectTransform>();
                trt.sizeDelta = new Vector2(S(82), S(82));
                var frame = tile.GetComponent<Image>();
                _tileFrames.Add(frame);
                var art = AddIcon(tile.transform, HeroSprite(i), Color.white, S(60));
                art.rectTransform.anchoredPosition = Vector2.zero;
                var btn = tile.AddComponent<Button>();
                btn.targetGraphic = frame;
                btn.onClick.AddListener(() => Select(idx));
            }
        }

        // ── footer ────────────────────────────────────────────────────────────
        private void BuildFooter()
        {
            var rnd = Button("Random", _root.transform, _assets ? _assets.buttonDark : null, LobbyTheme.Dark, LobbyTheme.DarkBevel);
            var rrt = rnd.GetComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0, 0); rrt.anchorMax = new Vector2(0, 0); rrt.pivot = new Vector2(0, 0);
            rrt.anchoredPosition = new Vector2(S(14), S(40)); rrt.sizeDelta = new Vector2(S(90), S(72));
            var rndLbl = AddLabel(rnd.transform, "Random", S(13), Color.white, TextAlignmentOptions.Center, true);
            Stretch(rndLbl.rectTransform);
            rnd.onClick.AddListener(() => Select(UnityEngine.Random.Range(0, Mathf.Max(1, _chars.Count))));

            var battle = Button("Battle", _root.transform, _assets ? _assets.buttonGold : null, LobbyTheme.Gold, LobbyTheme.GoldBevel);
            var btrt = battle.GetComponent<RectTransform>();
            btrt.anchorMin = new Vector2(0, 0); btrt.anchorMax = new Vector2(1, 0); btrt.pivot = new Vector2(0.5f, 0);
            btrt.offsetMin = new Vector2(S(118), S(40)); btrt.offsetMax = new Vector2(-S(14), S(40) + S(72));
            if (_assets && _assets.swords)
            {
                var sw = AddIcon(battle.transform, _assets.swords, LobbyTheme.TextOnGold, S(32));
                sw.rectTransform.anchorMin = sw.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                sw.rectTransform.anchoredPosition = new Vector2(-S(70), 0);
            }
            var lbl = AddLabel(battle.transform, "BATTLE", S(26), LobbyTheme.TextOnGold, TextAlignmentOptions.Center, true);
            var lblRt = lbl.rectTransform; lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one; lblRt.offsetMin = new Vector2(S(30), 0); lblRt.offsetMax = Vector2.zero;
            battle.onClick.AddListener(() => OnConfirmed?.Invoke(_selected));
        }

        // ── selection ───────────────────────────────────────────────────────────
        private void Select(int index)
        {
            if (_chars.Count == 0) return;
            _selectedIndex = Mathf.Clamp(index, 0, _chars.Count - 1);
            var ch = _chars[_selectedIndex];
            _selected = ch.Id;

            if (_spotImg != null) _spotImg.sprite = HeroSprite(_selectedIndex);
            if (_nameText != null) _nameText.text = ch.DisplayName;
            int rarity = (_selectedIndex % 5) + 1;
            for (int i = 0; i < _starImgs.Length; i++)
                if (_starImgs[i] != null)
                    _starImgs[i].color = i < rarity ? LobbyTheme.Coin : new Color(1, 1, 1, 0.22f);
            if (_hpVal != null)  _hpVal.text  = ch.BaseStats.MaxHealth.ToString("0");
            if (_atkVal != null) _atkVal.text = ch.BaseStats.Damage.ToString("0");
            if (_spdVal != null) _spdVal.text = ch.BaseStats.MoveSpeed.ToString("0.#");

            string abilityId = string.IsNullOrEmpty(ch.ActiveAbilityId.Value) ? ch.AutoAttackId.Value : ch.ActiveAbilityId.Value;
            if (_abilityName != null) _abilityName.text = Prettify(abilityId);
            if (_abilityDesc != null) _abilityDesc.text = "Signature ability";

            for (int i = 0; i < _tileFrames.Count; i++)
            {
                if (_tileFrames[i] == null) continue;
                bool sel = i == _selectedIndex;
                if (_assets != null)
                {
                    _tileFrames[i].sprite = sel ? _assets.buttonGold : _assets.panelBlue;
                    _tileFrames[i].color = Color.white;
                    Slice(_tileFrames[i], sel ? S(15) : S(14));
                }
                else _tileFrames[i].color = sel ? LobbyTheme.Gold : LobbyTheme.PanelBlue;
                _tileFrames[i].rectTransform.localScale = sel ? Vector3.one * 1.08f : Vector3.one;
            }
        }

        private Sprite HeroSprite(int index)
        {
            if (_assets == null) return null;
            var byName = _assets.GetHero(_chars.Count > index ? _chars[index].Id.Value : null);
            return byName != null ? byName : _assets.GetHeroByIndex(index);
        }

        // ── tiny UI helpers ─────────────────────────────────────────────────────
        private static GameObject NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }

        private static void Place(GameObject go, GameObject parent, float top, float height, float leftPad)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(leftPad / 2f, -top);
            rt.sizeDelta = new Vector2(-leftPad - S(10), height);
        }

        private static TMP_Text AddLabel(Transform parent, string text, float size, Color color, TextAlignmentOptions align, bool bold)
        {
            var go = NewUI("Label", parent);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
            t.enableWordWrapping = true; t.raycastTarget = false;
            if (bold) t.fontStyle = FontStyles.Bold;
            return t;
        }

        private static Image AddIcon(Transform parent, Sprite sprite, Color color, float size)
        {
            var go = NewUI("Icon", parent);
            var img = go.AddComponent<Image>();
            img.sprite = sprite; img.color = color; img.preserveAspect = true; img.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            return img;
        }

        // 9-slice with a fixed displayed corner radius so small elements don't smear
        private static void Slice(Image img, float cornerPx)
        {
            if (img.sprite == null) { img.type = Image.Type.Simple; return; }
            img.type = Image.Type.Sliced; img.fillCenter = true;
            var b = img.sprite.border; float avg = (b.x + b.y + b.z + b.w) / 4f;
            img.pixelsPerUnitMultiplier = avg > 1f ? Mathf.Max(0.05f, avg / Mathf.Max(2f, cornerPx)) : 1f;
        }

        private static GameObject Panel(string name, Transform parent, Sprite sprite, Color color, float corner = 0f)
        {
            var go = NewUI(name, parent);
            var img = go.AddComponent<Image>();
            img.sprite = sprite; img.color = sprite != null ? Color.white : color;
            Slice(img, corner > 0 ? corner : S(14));
            return go;
        }

        private static Button Button(string name, Transform parent, Sprite sprite, Color tint, Color bevel, float corner = 0f)
        {
            var go = NewUI(name, parent);
            var img = go.AddComponent<Image>();
            img.sprite = sprite; img.color = tint;
            Slice(img, corner > 0 ? corner : S(15));
            var sh = go.AddComponent<Shadow>(); sh.effectColor = bevel; sh.effectDistance = new Vector2(0, -S(4)); sh.useGraphicAlpha = false;
            var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
            return btn;
        }

        private static string Prettify(string id)
        {
            if (string.IsNullOrEmpty(id)) return "—";
            id = id.Replace('_', ' ');
            return char.ToUpper(id[0]) + id.Substring(1);
        }
    }
}
