// Editor builder for the lobby UI. Re-runnable: clears and rebuilds the 5 nav pages + bottom navigation
// in Lobby.unity from the imported sprites. UI only — no gameplay logic.
// Run via menu  ClashUp ▸ Build Lobby UI  (or LobbyUiBuilder.Build()).
using System.Collections.Generic;
using System.IO;

using ClashUp.Client.Lobby.Data;
using ClashUp.Client.Lobby.UI;
using ClashUp.Client.Lobby.UI.Pages;
using ClashUp.Client.Lobby.UI.Theme;

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class LobbyUiBuilder
{
    const string SpritesRoot = "Assets/Core/Lobby/Content/Sprites";
    const string GenDir      = "Assets/Core/Lobby/Content/Sprites/gen";

    static float S(float p) => LobbyTheme.S(p);
    static readonly float NavH = 84f * LobbyTheme.Scale;
    const float RefW = 1080f;          // canvas reference width — use for full-width layout, NOT S(390)
    static float TopPad => S(50);      // top margin for screens without the currency bar

    static float PanelCorner  => S(18);
    static float ButtonCorner => S(15);
    static float PillCorner   => S(12);
    static float ChipCorner   => S(9);
    static float BarCorner    => S(5);

    static Dictionary<string, Sprite> _sprites;

    [MenuItem("ClashUp/Build Lobby UI")]
    public static void Build()
    {
        LoadSprites();
        PopulateUiAssets();

        var canvas = GameObject.Find("LobbyCanvas");
        if (canvas == null) { Debug.LogError("[LobbyUI] LobbyCanvas not found. Open Lobby.unity first."); return; }

        var scrollGO = canvas.transform.Find("ScrollViewport");
        var hscroll  = scrollGO != null ? scrollGO.GetComponent<LobbyHorizontalScroll>() : null;
        var hcontent = scrollGO != null ? scrollGO.Find("HContent") : null;
        var bottomBarGO = canvas.transform.Find("BottomBar");
        if (hscroll == null || hcontent == null || bottomBarGO == null)
        { Debug.LogError("[LobbyUI] ScrollViewport/HContent + BottomBar not found."); return; }

        ClearChildren(hcontent);
        ClearChildren(bottomBarGO);

        var storeScroll    = BuildPage(hcontent, "Page_STORE",    false, out _, BuildStore);
        var heroesScroll   = BuildPage(hcontent, "Page_HEROES",   false, out _, BuildHeroes);
        var homeGO = BuildPageGO(hcontent, "Page_PLAY", true, out var homeScroll, out var homeContent);
        Button battleBtn = null;
        BuildHome(homeContent, ref battleBtn);
        var guildScroll    = BuildPage(hcontent, "Page_GUILD",    false, out _, BuildGuild);
        var missionsScroll = BuildPage(hcontent, "Page_MISSIONS", false, out _, BuildMissions);

        var mm = homeGO.GetComponent<MathmakingPage>();
        if (mm == null) mm = homeGO.AddComponent<MathmakingPage>();
        var so = new SerializedObject(mm);
        so.FindProperty("_playButton").objectReferenceValue = battleBtn;
        so.ApplyModifiedPropertiesWithoutUndo();

        var nav = BuildBottomNav(bottomBarGO, hscroll);

        var hso = new SerializedObject(hscroll);
        hso.FindProperty("_pageCount").intValue   = 5;
        hso.FindProperty("_defaultPage").intValue = 2;
        hso.FindProperty("_content").objectReferenceValue = hcontent.GetComponent<RectTransform>();
        hso.FindProperty("_bottomBar").objectReferenceValue = nav;
        var arr = hso.FindProperty("_pageScrollers");
        var scrolls = new[] { storeScroll, heroesScroll, homeScroll, guildScroll, missionsScroll };
        arr.arraySize = scrolls.Length;
        for (int i = 0; i < scrolls.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = scrolls[i];
        hso.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(canvas.scene);
        EditorSceneManager.SaveScene(canvas.scene);
        Debug.Log("[LobbyUI] Lobby rebuilt: 5 screens + bottom nav.");
    }

    // ════════════════════════════ page scaffolding ═══════════════════════════

    delegate void ContentBuilder(RectTransform content);

    static LobbyVerticalScroll BuildPage(Transform parent, string name, bool home, out RectTransform content, ContentBuilder builder)
    {
        var go = BuildPageGO(parent, name, home, out var scroll, out content);
        builder(content);
        return scroll;
    }

    static GameObject BuildPageGO(Transform parent, string name, bool home, out LobbyVerticalScroll scroll, out RectTransform content)
    {
        var page = NewUI(name, parent);
        var pageRt = page.GetComponent<RectTransform>();
        pageRt.anchorMin = new Vector2(0, 0); pageRt.anchorMax = new Vector2(1, 1);
        pageRt.offsetMin = Vector2.zero; pageRt.offsetMax = Vector2.zero;

        var bgImg = page.AddComponent<Image>();
        bgImg.sprite = BackgroundSprite(name + "_bg", home ? LobbyTheme.BgTopHome : LobbyTheme.BgTop, LobbyTheme.BgBottom, home ? 0.65f : 0.5f);

        page.AddComponent<Canvas>();
        page.AddComponent<GraphicRaycaster>();
        page.AddComponent<LobbyCanvasOptimizer>();

        var vp = NewUI("VScrollViewport", page);
        var vpRt = vp.GetComponent<RectTransform>();
        vpRt.anchorMin = new Vector2(0, 0); vpRt.anchorMax = new Vector2(1, 1);
        vpRt.offsetMin = new Vector2(0, NavH); vpRt.offsetMax = Vector2.zero;
        vp.AddComponent<RectMask2D>();
        scroll = vp.AddComponent<LobbyVerticalScroll>();

        var contentGO = NewUI("Content", vp);
        content = contentGO.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0, S(844));

        var sso = new SerializedObject(scroll);
        sso.FindProperty("_content").objectReferenceValue = content;
        sso.ApplyModifiedPropertiesWithoutUndo();
        return page;
    }

    static void SetContentHeight(RectTransform content, float h) => content.sizeDelta = new Vector2(0, Mathf.Max(h, S(700)));

    // ════════════════════════════ resource bar ═══════════════════════════════

    static float BuildResourceBar(RectTransform content, string hero)
    {
        float top = S(40);
        var bar = Place(NewUI("ResourceBar", content), top, S(54), S(16));
        var ring = Circle(bar, "Ring", LobbyTheme.Gold, S(54));
        var ringRt = ring.rectTransform; ringRt.anchorMin = new Vector2(0, 0.5f); ringRt.anchorMax = new Vector2(0, 0.5f); ringRt.pivot = new Vector2(0, 0.5f);
        var inner = Circle(ring.gameObject, "Inner", LobbyTheme.InsetDark, S(47));
        inner.rectTransform.anchorMin = inner.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        var face = Icon(inner.gameObject, Spr(hero), Color.white, S(42));
        face.rectTransform.anchorMin = face.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        var lvlBg = Rect(bar, "LvlBg", LobbyTheme.Gold, ChipCorner);
        var lvlRt = lvlBg.rectTransform; lvlRt.anchorMin = new Vector2(0, 0); lvlRt.anchorMax = new Vector2(0, 0); lvlRt.pivot = new Vector2(0.5f, 0.5f);
        lvlRt.sizeDelta = new Vector2(S(30), S(20)); lvlRt.anchoredPosition = new Vector2(S(27), S(2));
        var lvl = Text(lvlBg.gameObject, "Lvl", "12", S(11), LobbyTheme.TextOnGold, TextAlignmentOptions.Center, true); Stretch(lvl.rectTransform);

        var gem = BuildCurrencyPill(bar, "gem", "96", true);
        var gemRt = gem.GetComponent<RectTransform>(); gemRt.anchorMin = new Vector2(1, 0.5f); gemRt.anchorMax = new Vector2(1, 0.5f); gemRt.pivot = new Vector2(1, 0.5f); gemRt.anchoredPosition = Vector2.zero;
        var coin = BuildCurrencyPill(bar, "coin", "2,480", false);
        var coinRt = coin.GetComponent<RectTransform>(); coinRt.anchorMin = new Vector2(1, 0.5f); coinRt.anchorMax = new Vector2(1, 0.5f); coinRt.pivot = new Vector2(1, 0.5f); coinRt.anchoredPosition = new Vector2(-S(108), 0);
        return top + S(54) + S(14);
    }

    static GameObject BuildCurrencyPill(GameObject parent, string icon, string number, bool plus)
    {
        var pill = SlicedImage(parent, "Pill_" + icon, Spr("pill_dark"), Color.white, PillCorner).gameObject;
        var rt = pill.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(S(98), S(34));
        var ic = Icon(pill, Spr(icon), Color.white, S(20));
        ic.rectTransform.anchorMin = new Vector2(0, 0.5f); ic.rectTransform.anchorMax = new Vector2(0, 0.5f); ic.rectTransform.pivot = new Vector2(0, 0.5f); ic.rectTransform.anchoredPosition = new Vector2(S(7), 0);
        var num = Text(pill, "Num", number, S(13), LobbyTheme.TextGold, TextAlignmentOptions.Left, true);
        var nrt = num.rectTransform; nrt.anchorMin = new Vector2(0, 0); nrt.anchorMax = new Vector2(1, 1); nrt.offsetMin = new Vector2(S(30), 0); nrt.offsetMax = new Vector2(plus ? -S(26) : -S(8), 0);
        if (plus)
        {
            var p = Circle(pill, "Plus", LobbyTheme.Green, S(20));
            p.rectTransform.anchorMin = new Vector2(1, 0.5f); p.rectTransform.anchorMax = new Vector2(1, 0.5f); p.rectTransform.pivot = new Vector2(1, 0.5f); p.rectTransform.anchoredPosition = new Vector2(-S(4), 0);
            var pi = Icon(p.gameObject, Spr("plus"), Color.white, S(14)); pi.rectTransform.anchorMin = pi.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        }
        return pill;
    }

    // ════════════════════════════ STORE ══════════════════════════════════════

    static void BuildStore(RectTransform content)
    {
        float y = BuildResourceBar(content, "embertail");

        var feat = BuildPanel(content, "Featured", "panel_purple", Color.white, y, S(140), S(16));
        var badge = SolidButton(feat.transform, "Badge", LobbyTheme.Red, LobbyTheme.RedBevel, "BEST VALUE · -45%", S(11), Color.white);
        var bRt = badge.GetComponent<RectTransform>(); bRt.anchorMin = new Vector2(0, 1); bRt.anchorMax = new Vector2(0, 1); bRt.pivot = new Vector2(0, 1); bRt.sizeDelta = new Vector2(S(180), S(28)); bRt.anchoredPosition = new Vector2(S(14), -S(12));
        var ft = Text(feat, "T", "Starter Bundle", S(20), LobbyTheme.TextWhite, TextAlignmentOptions.TopLeft, true); ft.enableWordWrapping = false; PlaceLR(ft.gameObject, S(48), S(28), S(16), S(140));
        var fs = Text(feat, "Sub", "5,000 Coins · 30 Gems · Rare Hero", S(11), LobbyTheme.TextLight, TextAlignmentOptions.TopLeft, false); fs.enableWordWrapping = false; PlaceLR(fs.gameObject, S(78), S(20), S(16), S(140));
        var buy = BuildButton(feat.transform, "Buy", "button_green", LobbyTheme.Green, LobbyTheme.GreenBevel, "$4.99", S(15), Color.white);
        var buyRt = buy.GetComponent<RectTransform>(); buyRt.anchorMin = new Vector2(0, 0); buyRt.anchorMax = new Vector2(0, 0); buyRt.pivot = new Vector2(0, 0); buyRt.sizeDelta = new Vector2(S(130), S(42)); buyRt.anchoredPosition = new Vector2(S(16), S(14));
        var critter = Icon(feat, Spr("frostnip"), Color.white, S(126));
        critter.rectTransform.anchorMin = new Vector2(1, 0.5f); critter.rectTransform.anchorMax = new Vector2(1, 0.5f); critter.rectTransform.pivot = new Vector2(1, 0.5f); critter.rectTransform.anchoredPosition = new Vector2(-S(6), 0);
        y += S(140) + S(18);

        AddSectionLabel(content, "Gems", ref y);
        y = BuildPackRow(content, y, "gem", new[] { "80", "500", "1,200" }, new[] { "$1.99", "$5.99", "$11.99" });
        AddSectionLabel(content, "Coins", ref y);
        y = BuildPackRow(content, y, "coin", new[] { "1,000", "6,500", "15,000" }, new[] { "$0.99", "$4.99", "$9.99" });

        var strip = BuildPanel(content, "DailyChest", "panel_blue", Color.white, y, S(80), S(16));
        var ci = Icon(strip, Spr("chest_gold"), Color.white, S(60));
        ci.rectTransform.anchorMin = new Vector2(0, 0.5f); ci.rectTransform.anchorMax = new Vector2(0, 0.5f); ci.rectTransform.pivot = new Vector2(0, 0.5f); ci.rectTransform.anchoredPosition = new Vector2(S(12), 0);
        var ct = Text(strip, "T", "Daily Chest", S(15), LobbyTheme.TextWhite, TextAlignmentOptions.TopLeft, true); PlaceLR(ct.gameObject, S(20), S(24), S(84), S(120));
        var cd = Text(strip, "D", "Free · refreshes in 8h", S(11), LobbyTheme.TextMuted, TextAlignmentOptions.TopLeft, false); PlaceLR(cd.gameObject, S(44), S(20), S(84), S(120));
        var open = BuildButton(strip.transform, "Open", "button_gold", LobbyTheme.Gold, LobbyTheme.GoldBevel, "OPEN", S(14), LobbyTheme.TextOnGold);
        var oRt = open.GetComponent<RectTransform>(); oRt.anchorMin = new Vector2(1, 0.5f); oRt.anchorMax = new Vector2(1, 0.5f); oRt.pivot = new Vector2(1, 0.5f); oRt.sizeDelta = new Vector2(S(108), S(46)); oRt.anchoredPosition = new Vector2(-S(14), 0);
        y += S(80) + S(20);
        SetContentHeight(content, y);
    }

    static float BuildPackRow(RectTransform content, float y, string icon, string[] amounts, string[] prices)
    {
        float pad = S(16), gap = S(12), cardH = S(150);
        var row = Place(NewUI("PackRow_" + icon, content), y, cardH, pad);
        for (int i = 0; i < 3; i++)
        {
            var card = SlicedImage(row, "Pack" + i, Spr("panel_blue"), Color.white, PanelCorner);
            ColCell(card.rectTransform, i, 3, gap);
            var ic = Icon(card.gameObject, Spr(icon), Color.white, S(54));
            ic.rectTransform.anchorMin = new Vector2(0.5f, 1); ic.rectTransform.anchorMax = new Vector2(0.5f, 1); ic.rectTransform.pivot = new Vector2(0.5f, 1); ic.rectTransform.anchoredPosition = new Vector2(0, -S(16));
            var amt = Text(card.gameObject, "Amt", amounts[i], S(15), LobbyTheme.TextGold, TextAlignmentOptions.Center, true); amt.enableWordWrapping = false;
            var aRt = amt.rectTransform; aRt.anchorMin = new Vector2(0, 1); aRt.anchorMax = new Vector2(1, 1); aRt.pivot = new Vector2(0.5f, 1); aRt.anchoredPosition = new Vector2(0, -S(80)); aRt.sizeDelta = new Vector2(0, S(24));
            var price = BuildButton(card.transform, "Price", "button_green", LobbyTheme.Green, LobbyTheme.GreenBevel, prices[i], S(13), Color.white);
            var pRt = price.GetComponent<RectTransform>(); pRt.anchorMin = new Vector2(0, 0); pRt.anchorMax = new Vector2(1, 0); pRt.pivot = new Vector2(0.5f, 0); pRt.offsetMin = new Vector2(S(12), S(12)); pRt.offsetMax = new Vector2(-S(12), S(12) + S(42));
        }
        return y + cardH + S(16);
    }

    // ════════════════════════════ HEROES ═════════════════════════════════════

    static void BuildHeroes(RectTransform content)
    {
        float y = TopPad;
        var title = Text(content, "Title", "My Heroes", S(20), LobbyTheme.TextWhite, TextAlignmentOptions.Left, true); Place(title.gameObject, y, S(30), S(16));
        var count = Text(content, "Count", "8 / 30", S(13), LobbyTheme.TextMuted, TextAlignmentOptions.Right, true); Place(count.gameObject, y + S(5), S(24), S(16));
        y += S(40);

        var hero = BuildPanel(content, "Hero", "panel_gold", Color.white, y, S(180), S(16));
        var art = Icon(hero, Spr("embertail"), Color.white, S(150));
        art.rectTransform.anchorMin = new Vector2(0, 0.5f); art.rectTransform.anchorMax = new Vector2(0, 0.5f); art.rectTransform.pivot = new Vector2(0, 0.5f); art.rectTransform.anchoredPosition = new Vector2(S(10), 0);
        var hn = Text(hero, "Name", "Embertail", S(22), LobbyTheme.TextWhite, TextAlignmentOptions.TopLeft, true); hn.enableWordWrapping = false; PlaceLR(hn.gameObject, S(16), S(30), S(172), S(16));
        var hstars = Stars(hero.transform, 4); ((HorizontalLayoutGroup)hstars.GetComponent<HorizontalLayoutGroup>()).childAlignment = TextAnchor.MiddleLeft; PlaceLR(hstars, S(48), S(22), S(172), S(16));
        StatBar(hero, S(78), "HEALTH", LobbyTheme.Green, 0.8f, S(172));
        StatBar(hero, S(106), "ATTACK", LobbyTheme.Orange, 0.6f, S(172));
        var lvlup = BuildButton(hero.transform, "LevelUp", "button_gold", LobbyTheme.Gold, LobbyTheme.GoldBevel, "LEVEL UP   2,500", S(14), LobbyTheme.TextOnGold);
        var luRt = lvlup.GetComponent<RectTransform>(); luRt.anchorMin = new Vector2(0, 0); luRt.anchorMax = new Vector2(1, 0); luRt.pivot = new Vector2(0.5f, 0); luRt.offsetMin = new Vector2(S(172), S(16)); luRt.offsetMax = new Vector2(-S(16), S(16) + S(44));
        y += S(180) + S(16);

        string[] roster = { "sproutling", "embertail", "bumblefuzz", "tweetle", "plumpkin", "frostnip", "cindersnap", "mossback" };
        int[] rarity = { 2, 4, 2, 3, 3, 2, 5, 3 };
        int[] level = { 7, 11, 6, 8, 9, 5, 12, 7 };
        y = BuildHeroGrid(content, y, roster, rarity, level);
        SetContentHeight(content, y);
    }

    static void StatBar(GameObject parent, float top, string label, Color fill, float frac, float left)
    {
        var lbl = Text(parent, "L_" + label, label, S(9), LobbyTheme.TextMuted, TextAlignmentOptions.Left, true); PlaceLR(lbl.gameObject, top, S(14), left, S(16));
        var track = Rect(parent, "Track_" + label, LobbyTheme.InsetDark, BarCorner);
        var rt = track.rectTransform; rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1); rt.offsetMin = new Vector2(left, 0); rt.offsetMax = new Vector2(-S(16), 0); rt.anchoredPosition = new Vector2(0, -(top + S(14))); rt.sizeDelta = new Vector2(rt.sizeDelta.x, S(12));
        var f = Rect(track.gameObject, "Fill", fill, BarCorner);
        var fRt = f.rectTransform; fRt.anchorMin = new Vector2(0, 0); fRt.anchorMax = new Vector2(frac, 1); fRt.offsetMin = Vector2.zero; fRt.offsetMax = Vector2.zero;
    }

    static float BuildHeroGrid(RectTransform content, float y, string[] names, int[] rarity, int[] level)
    {
        float pad = S(16), gap = S(12);
        int cols = 3, total = names.Length + 1, rows = Mathf.CeilToInt(total / (float)cols);
        float cardW = (RefW - pad * 2 - gap * (cols - 1)) / cols, cardH = cardW * 1.22f;
        var grid = Place(NewUI("Grid", content), y, rows * cardH + (rows - 1) * gap, pad);
        for (int i = 0; i < total; i++)
        {
            int r = i / cols, c = i % cols;
            var card = Rect(grid, "Card" + i, Color.white, PanelCorner);
            GridCell(card.rectTransform, c, cols, r, cardH, gap);
            if (i < names.Length)
            {
                card.color = LobbyTheme.RarityColor(rarity[i]);
                var innerB = SlicedImage(card.gameObject, "Inner", Spr("panel_blue"), Color.white, PanelCorner);
                Stretch(innerB.rectTransform); innerB.rectTransform.offsetMin = new Vector2(S(3), S(3)); innerB.rectTransform.offsetMax = new Vector2(-S(3), -S(3));
                var art = Icon(innerB.gameObject, Spr(names[i]), Color.white, S(96));
                art.rectTransform.anchorMin = new Vector2(0.5f, 1); art.rectTransform.anchorMax = new Vector2(0.5f, 1); art.rectTransform.pivot = new Vector2(0.5f, 1); art.rectTransform.anchoredPosition = new Vector2(0, -S(16));
                var nm = Text(innerB.gameObject, "Name", Cap(names[i]), S(13), LobbyTheme.TextWhite, TextAlignmentOptions.Center, true); nm.enableWordWrapping = false;
                var nRt = nm.rectTransform; nRt.anchorMin = new Vector2(0, 1); nRt.anchorMax = new Vector2(1, 1); nRt.pivot = new Vector2(0.5f, 1); nRt.anchoredPosition = new Vector2(0, -S(122)); nRt.sizeDelta = new Vector2(0, S(20));
                var st = Stars(innerB.transform, rarity[i]);
                var sRt = (RectTransform)st.transform; sRt.anchorMin = new Vector2(0, 1); sRt.anchorMax = new Vector2(1, 1); sRt.pivot = new Vector2(0.5f, 1); sRt.anchoredPosition = new Vector2(0, -S(148)); sRt.sizeDelta = new Vector2(0, S(16));
                var lvBg = Rect(innerB.gameObject, "LvBg", LobbyTheme.InsetDark, ChipCorner);
                lvBg.rectTransform.anchorMin = new Vector2(0, 1); lvBg.rectTransform.anchorMax = new Vector2(0, 1); lvBg.rectTransform.pivot = new Vector2(0, 1); lvBg.rectTransform.sizeDelta = new Vector2(S(40), S(18)); lvBg.rectTransform.anchoredPosition = new Vector2(S(5), -S(5));
                var lv = Text(lvBg.gameObject, "Lvl", "Lv " + level[i], S(10), LobbyTheme.TextGold, TextAlignmentOptions.Center, true); Stretch(lv.rectTransform);
            }
            else
            {
                card.color = new Color(1, 1, 1, 0.16f);
                var lk = Icon(card.gameObject, Spr("lock"), new Color(1, 1, 1, 0.5f), S(48)); lk.rectTransform.anchorMin = lk.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            }
        }
        return y + rows * cardH + (rows - 1) * gap + S(20);
    }

    // ════════════════════════════ HOME ═══════════════════════════════════════

    static void BuildHome(RectTransform content, ref Button battleBtn)
    {
        float y = BuildResourceBar(content, "embertail");

        // trophy bar (gold border + dark inner)
        var trophyBarGO = Place(NewUI("TrophyBar", content), y, S(48), S(60));
        var trophyBorder = Rect(trophyBarGO, "Border", LobbyTheme.Gold, PillCorner); Stretch(trophyBorder.rectTransform);
        var trophy = Rect(trophyBorder.gameObject, "Fill", LobbyTheme.InsetDark, PillCorner); Stretch(trophy.rectTransform); trophy.rectTransform.offsetMin = new Vector2(S(3), S(3)); trophy.rectTransform.offsetMax = new Vector2(-S(3), -S(3));
        var tIco = Icon(trophy.gameObject, Spr("trophy"), Color.white, S(28));
        tIco.rectTransform.anchorMin = new Vector2(0, 0.5f); tIco.rectTransform.anchorMax = new Vector2(0, 0.5f); tIco.rectTransform.pivot = new Vector2(0, 0.5f); tIco.rectTransform.anchoredPosition = new Vector2(S(16), 0);
        var tTxt = Text(trophy.gameObject, "T", "3,240    |    Frostpeak Arena", S(14), LobbyTheme.TextGold, TextAlignmentOptions.Center, true); Stretch(tTxt.rectTransform);
        y += S(48) + S(10);

        // event banner: dot + text
        var banner = SolidButton(content.transform, "Event", LobbyTheme.Red, LobbyTheme.RedBevel, null, 0, default); banner.interactable = false;
        var brt = banner.GetComponent<RectTransform>(); brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(0, 1); brt.pivot = new Vector2(0, 1); brt.sizeDelta = new Vector2(S(238), S(34)); brt.anchoredPosition = new Vector2(S(16), -y);
        var dot = Circle(banner.gameObject, "Dot", LobbyTheme.TextGold, S(12)); dot.rectTransform.anchorMin = new Vector2(0, 0.5f); dot.rectTransform.anchorMax = new Vector2(0, 0.5f); dot.rectTransform.pivot = new Vector2(0, 0.5f); dot.rectTransform.anchoredPosition = new Vector2(S(12), 0);
        var evTxt = Text(banner.gameObject, "T", "Carnival Event · 2d left", S(12), Color.white, TextAlignmentOptions.Left, true);
        var evRt = evTxt.rectTransform; evRt.anchorMin = new Vector2(0, 0); evRt.anchorMax = new Vector2(1, 1); evRt.offsetMin = new Vector2(S(30), 0); evRt.offsetMax = new Vector2(-S(10), 0);
        y += S(34) + S(4);

        // arena stage
        float stageH = S(296);
        var stage = Place(NewUI("Stage", content), y, stageH, S(8));
        var platform = Icon(stage, CircleSprite(), new Color(LobbyTheme.BgGlow.r, LobbyTheme.BgGlow.g, LobbyTheme.BgGlow.b, 0.55f), 0);
        platform.preserveAspect = false; var pl = platform.rectTransform; pl.anchorMin = new Vector2(0.5f, 0); pl.anchorMax = new Vector2(0.5f, 0); pl.pivot = new Vector2(0.5f, 0); pl.sizeDelta = new Vector2(S(280), S(80)); pl.anchoredPosition = new Vector2(0, S(28));
        var lead = Icon(stage, Spr("embertail"), Color.white, S(216));
        var ld = lead.rectTransform; ld.anchorMin = new Vector2(0.5f, 0); ld.anchorMax = new Vector2(0.5f, 0); ld.pivot = new Vector2(0.5f, 0); ld.sizeDelta = new Vector2(S(216), S(216)); ld.anchoredPosition = new Vector2(0, S(48));
        var np = Rect(stage, "Nameplate", LobbyTheme.InsetDark, PillCorner);
        var npRt = np.rectTransform; npRt.anchorMin = new Vector2(0.5f, 0); npRt.anchorMax = new Vector2(0.5f, 0); npRt.pivot = new Vector2(0.5f, 0); npRt.sizeDelta = new Vector2(S(210), S(40)); npRt.anchoredPosition = new Vector2(0, 0);
        var npTxt = Text(np.gameObject, "T", "Embertail · Lv 11", S(15), LobbyTheme.TextWhite, TextAlignmentOptions.Center, true); Stretch(npTxt.rectTransform);
        y += stageH + S(6);

        // chest slots: epic, gold(timer), locked, locked
        var chestRow = Place(NewUI("Chests", content), y, S(78), S(16));
        float gap = S(10);
        for (int i = 0; i < 4; i++)
        {
            bool filled = i < 2;
            var slot = Rect(chestRow, "Slot" + i, filled ? LobbyTheme.InsetDark : new Color(1, 1, 1, 0.08f), ChipCorner);
            ColCell(slot.rectTransform, i, 4, gap);
            var c = Icon(slot.gameObject, Spr(i == 0 ? "chest_epic" : i == 1 ? "chest_gold" : "lock"), filled ? Color.white : new Color(1, 1, 1, 0.45f), S(40));
            c.rectTransform.anchorMin = new Vector2(0.5f, 1); c.rectTransform.anchorMax = new Vector2(0.5f, 1); c.rectTransform.pivot = new Vector2(0.5f, 1); c.rectTransform.anchoredPosition = new Vector2(0, -S(8));
            if (filled)
            {
                var l = Text(slot.gameObject, "Lbl", i == 0 ? "EPIC" : "1:42:00", S(9), i == 0 ? LobbyTheme.TextGold : LobbyTheme.TextLight, TextAlignmentOptions.Center, true);
                var lRt = l.rectTransform; lRt.anchorMin = new Vector2(0, 0); lRt.anchorMax = new Vector2(1, 0); lRt.pivot = new Vector2(0.5f, 0); lRt.anchoredPosition = new Vector2(0, S(6)); lRt.sizeDelta = new Vector2(0, S(14));
            }
        }
        y += S(78) + S(12);

        // BATTLE (centered swords + label)
        var battle = BuildButton(content.transform, "PlayButton", "button_gold", LobbyTheme.Gold, LobbyTheme.GoldBevel, null, 0, default);
        Place(battle.gameObject, y, S(66), S(36));
        IconText(battle.gameObject, Spr("swords"), LobbyTheme.TextOnGold, S(30), "BATTLE", S(26), LobbyTheme.TextOnGold);
        battleBtn = battle;
        y += S(66) + S(10);

        var sec = Place(NewUI("Secondary", content), y, S(46), S(16));
        var duo = BuildButton(sec.transform, "Duo", "button_blue", LobbyTheme.Blue, LobbyTheme.BlueBevel, "2v2 Duo", S(14), Color.white);
        var duoRt = duo.GetComponent<RectTransform>(); duoRt.anchorMin = new Vector2(0, 0.5f); duoRt.anchorMax = new Vector2(0.5f, 0.5f); duoRt.pivot = new Vector2(0, 0.5f); duoRt.offsetMin = Vector2.zero; duoRt.offsetMax = new Vector2(-S(5), 0); duoRt.sizeDelta = new Vector2(duoRt.sizeDelta.x, S(46));
        var prac = BuildButton(sec.transform, "Practice", "button_dark", LobbyTheme.Dark, LobbyTheme.DarkBevel, "Practice", S(14), Color.white);
        var prRt = prac.GetComponent<RectTransform>(); prRt.anchorMin = new Vector2(0.5f, 0.5f); prRt.anchorMax = new Vector2(1, 0.5f); prRt.pivot = new Vector2(1, 0.5f); prRt.offsetMin = new Vector2(S(5), 0); prRt.offsetMax = Vector2.zero; prRt.sizeDelta = new Vector2(prRt.sizeDelta.x, S(46));
        y += S(46) + S(20);
        SetContentHeight(content, y);
    }

    // ════════════════════════════ GUILD ══════════════════════════════════════

    static void BuildGuild(RectTransform content)
    {
        float y = TopPad;
        var title = Text(content, "Title", "Guild", S(20), LobbyTheme.TextWhite, TextAlignmentOptions.Left, true); Place(title.gameObject, y, S(30), S(16)); y += S(40);

        var head = BuildPanel(content, "Header", "panel_gold", Color.white, y, S(120), S(16));
        var crestBg = Icon(head, Spr("shield"), LobbyTheme.PanelGoldBorder, S(84));
        crestBg.rectTransform.anchorMin = new Vector2(0, 0.5f); crestBg.rectTransform.anchorMax = new Vector2(0, 0.5f); crestBg.rectTransform.pivot = new Vector2(0, 0.5f); crestBg.rectTransform.anchoredPosition = new Vector2(S(10), 0);
        var crestC = Icon(crestBg.gameObject, Spr("embertail"), Color.white, S(44)); crestC.rectTransform.anchorMin = crestC.rectTransform.anchorMax = new Vector2(0.5f, 0.55f);
        var gn = Text(head, "Name", "Ember Wardens", S(18), LobbyTheme.TextWhite, TextAlignmentOptions.TopLeft, true); gn.enableWordWrapping = false; PlaceLR(gn.gameObject, S(16), S(26), S(106), S(16));
        var gs = Text(head, "Sub", "Open · 2,000 trophies required", S(11), LobbyTheme.TextMuted, TextAlignmentOptions.TopLeft, false); gs.enableWordWrapping = false; PlaceLR(gs.gameObject, S(44), S(20), S(106), S(16));
        MiniChip(head, "trophy", "124,500", S(106), S(72));
        MiniChip(head, "head", "28/30", S(106) + S(130), S(72));
        y += S(120) + S(12);

        var strip = Place(NewUI("Actions", content), y, S(46), S(16));
        var req = BuildButton(strip.transform, "Request", "button_gold", LobbyTheme.Gold, LobbyTheme.GoldBevel, "Request", S(14), LobbyTheme.TextOnGold);
        var rRt = req.GetComponent<RectTransform>(); rRt.anchorMin = new Vector2(0, 0.5f); rRt.anchorMax = new Vector2(0.5f, 0.5f); rRt.pivot = new Vector2(0, 0.5f); rRt.offsetMin = Vector2.zero; rRt.offsetMax = new Vector2(-S(5), 0); rRt.sizeDelta = new Vector2(rRt.sizeDelta.x, S(46));
        var don = BuildButton(strip.transform, "Donate", "button_green", LobbyTheme.Green, LobbyTheme.GreenBevel, "Donate All", S(14), Color.white);
        var dRt = don.GetComponent<RectTransform>(); dRt.anchorMin = new Vector2(0.5f, 0.5f); dRt.anchorMax = new Vector2(1, 0.5f); dRt.pivot = new Vector2(1, 0.5f); dRt.offsetMin = new Vector2(S(5), 0); dRt.offsetMax = Vector2.zero; dRt.sizeDelta = new Vector2(dRt.sizeDelta.x, S(46));
        y += S(46) + S(14);

        AddSectionLabel(content, "Members", ref y);
        string[] members = { "PixelKnight", "NovaFlare", "ZippyZap", "MossyMan", "CinderPaw" };
        string[] roles = { "Leader", "Co-leader", "Elder", "Member", "Member" };
        string[] avatars = { "frostnip", "embertail", "tweetle", "mossback", "cindersnap" };
        for (int i = 0; i < members.Length; i++) y = BuildMemberRow(content, y, members[i], roles[i], avatars[i]);

        var chat = SlicedImage(Place(NewUI("Chat", content), y, S(44), S(16)), "Pill", Spr("pill_dark"), Color.white, PillCorner); Stretch(chat.rectTransform);
        var chatTxt = Text(chat.gameObject, "T", "Say something to your guild...", S(12), LobbyTheme.TextMuted, TextAlignmentOptions.Left, false);
        var chatRt = chatTxt.rectTransform; chatRt.anchorMin = new Vector2(0, 0); chatRt.anchorMax = new Vector2(1, 1); chatRt.offsetMin = new Vector2(S(16), 0); chatRt.offsetMax = new Vector2(-S(16), 0);
        y += S(44) + S(14);
        SetContentHeight(content, y);
    }

    static GameObject MiniChip(GameObject parent, string icon, string text, float left, float top)
    {
        var chip = SlicedImage(parent, "Chip_" + icon, Spr("pill_dark"), Color.white, ChipCorner).gameObject;
        var rt = chip.GetComponent<RectTransform>(); rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1); rt.sizeDelta = new Vector2(S(118), S(30)); rt.anchoredPosition = new Vector2(left, -top);
        var ic = Icon(chip, Spr(icon), Color.white, S(18)); ic.rectTransform.anchorMin = new Vector2(0, 0.5f); ic.rectTransform.anchorMax = new Vector2(0, 0.5f); ic.rectTransform.pivot = new Vector2(0, 0.5f); ic.rectTransform.anchoredPosition = new Vector2(S(6), 0);
        var t = Text(chip, "T", text, S(11), LobbyTheme.TextGold, TextAlignmentOptions.Left, true);
        var trt = t.rectTransform; trt.anchorMin = new Vector2(0, 0); trt.anchorMax = new Vector2(1, 1); trt.offsetMin = new Vector2(S(28), 0); trt.offsetMax = new Vector2(-S(6), 0);
        return chip;
    }

    static float BuildMemberRow(RectTransform content, float y, string name, string role, string avatar)
    {
        var row = BuildPanel(content, "Member_" + name, "panel_blue", Color.white, y, S(56), S(16));
        var av = Circle(row, "Av", LobbyTheme.InsetDark, S(40)); av.rectTransform.anchorMin = new Vector2(0, 0.5f); av.rectTransform.anchorMax = new Vector2(0, 0.5f); av.rectTransform.pivot = new Vector2(0, 0.5f); av.rectTransform.anchoredPosition = new Vector2(S(8), 0);
        var face = Icon(av.gameObject, Spr(avatar), Color.white, S(36)); face.rectTransform.anchorMin = face.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        var dot = Circle(row, "Dot", LobbyTheme.Online, S(12)); dot.rectTransform.anchorMin = new Vector2(0, 0.5f); dot.rectTransform.anchorMax = new Vector2(0, 0.5f); dot.rectTransform.pivot = new Vector2(0.5f, 0.5f); dot.rectTransform.anchoredPosition = new Vector2(S(44), -S(13));
        var nm = Text(row, "Name", name, S(14), LobbyTheme.TextWhite, TextAlignmentOptions.Left, true); PlaceLR(nm.gameObject, S(10), S(20), S(58), S(120)); nm.alignment = TextAlignmentOptions.Left;
        var rl = Text(row, "Role", role, S(11), LobbyTheme.TextMuted, TextAlignmentOptions.Left, false); PlaceLR(rl.gameObject, S(30), S(18), S(58), S(120)); rl.alignment = TextAlignmentOptions.Left;
        var trIco = Icon(row, Spr("trophy"), Color.white, S(18)); trIco.rectTransform.anchorMin = new Vector2(1, 0.5f); trIco.rectTransform.anchorMax = new Vector2(1, 0.5f); trIco.rectTransform.pivot = new Vector2(1, 0.5f); trIco.rectTransform.anchoredPosition = new Vector2(-S(78), 0);
        var tr = Text(row, "Tro", "5,420", S(12), LobbyTheme.TextGold, TextAlignmentOptions.Right, true); PlaceLR(tr.gameObject, S(18), S(22), S(16), S(16)); tr.alignment = TextAlignmentOptions.Right;
        return y + S(56) + S(8);
    }

    // ════════════════════════════ MISSIONS ═══════════════════════════════════

    static void BuildMissions(RectTransform content)
    {
        float y = TopPad;
        var title = Text(content, "Title", "Missions", S(20), LobbyTheme.TextWhite, TextAlignmentOptions.Left, true); Place(title.gameObject, y, S(30), S(16)); y += S(40);

        const float passH = 150f;
        var pass = BuildPanel(content, "SeasonPass", "panel_purple", Color.white, y, S(passH), S(16));
        var pt = Text(pass, "T", "Season 4 · Carnival", S(16), LobbyTheme.TextWhite, TextAlignmentOptions.TopLeft, true); PlaceLR(pt.gameObject, S(14), S(24), S(16), S(90));
        var pe = Text(pass, "E", "Ends in 18 days", S(11), LobbyTheme.TextLight, TextAlignmentOptions.TopLeft, false); PlaceLR(pe.gameObject, S(38), S(18), S(16), S(90));
        var tier = Rect(pass, "Tier", LobbyTheme.InsetDark, ChipCorner);
        var tiRt = tier.rectTransform; tiRt.anchorMin = new Vector2(1, 1); tiRt.anchorMax = new Vector2(1, 1); tiRt.pivot = new Vector2(1, 1); tiRt.sizeDelta = new Vector2(S(70), S(46)); tiRt.anchoredPosition = new Vector2(-S(14), -S(12));
        var tierT = Text(tier.gameObject, "T", "TIER\n14", S(12), LobbyTheme.TextGold, TextAlignmentOptions.Center, true); Stretch(tierT.rectTransform);
        var track = Rect(pass, "Track", LobbyTheme.InsetDark, BarCorner);
        var trRt = track.rectTransform; trRt.anchorMin = new Vector2(0, 1); trRt.anchorMax = new Vector2(1, 1); trRt.pivot = new Vector2(0.5f, 1); trRt.offsetMin = new Vector2(S(16), 0); trRt.offsetMax = new Vector2(-S(16), 0); trRt.anchoredPosition = new Vector2(0, -S(66)); trRt.sizeDelta = new Vector2(trRt.sizeDelta.x, S(18));
        var fill = Rect(track.gameObject, "Fill", LobbyTheme.Gold, BarCorner); var fRt = fill.rectTransform; fRt.anchorMin = new Vector2(0, 0); fRt.anchorMax = new Vector2(0.62f, 1); fRt.offsetMin = Vector2.zero; fRt.offsetMax = Vector2.zero;
        var plbl = Text(track.gameObject, "L", "620 / 1000", S(10), LobbyTheme.TextOnGold, TextAlignmentOptions.Center, true); Stretch(plbl.rectTransform);
        var unlock = BuildButton(pass.transform, "Unlock", "button_gold", LobbyTheme.Gold, LobbyTheme.GoldBevel, "Unlock Premium Pass", S(14), LobbyTheme.TextOnGold);
        var uRt = unlock.GetComponent<RectTransform>(); uRt.anchorMin = new Vector2(0, 0); uRt.anchorMax = new Vector2(1, 0); uRt.pivot = new Vector2(0.5f, 0); uRt.offsetMin = new Vector2(S(16), S(14)); uRt.offsetMax = new Vector2(-S(16), S(14) + S(44));
        y += S(passH) + S(16);

        AddSectionLabelAt(content, "Daily Quests", y);
        var reset = Text(content, "Reset", "Resets in 06:24:11", S(11), LobbyTheme.TextMuted, TextAlignmentOptions.Right, false); Place(reset.gameObject, y, S(22), S(16));
        y += S(34);

        string[] quests = { "Win 3 battles", "Deal 5,000 damage", "Play 5 matches", "Open a chest" };
        string[] icons  = { "trophy", "swords", "head", "chest_gold" };
        Color[] tints   = { LobbyTheme.Gold, LobbyTheme.Red, LobbyTheme.Blue, LobbyTheme.Green };
        int[] prog      = { 100, 60, 40, 100 };
        string[] reward = { "150", "80", "120", "200" };
        for (int i = 0; i < quests.Length; i++) y = BuildQuestRow(content, y, quests[i], icons[i], tints[i], prog[i], reward[i]);
        SetContentHeight(content, y + S(10));
    }

    static float BuildQuestRow(RectTransform content, float y, string title, string icon, Color tileColor, int progress, string reward)
    {
        var row = BuildPanel(content, "Quest", "panel_blue", Color.white, y, S(72), S(16));
        var tile = Rect(row, "Tile", tileColor, ChipCorner); tile.rectTransform.anchorMin = new Vector2(0, 0.5f); tile.rectTransform.anchorMax = new Vector2(0, 0.5f); tile.rectTransform.pivot = new Vector2(0, 0.5f); tile.rectTransform.sizeDelta = new Vector2(S(46), S(46)); tile.rectTransform.anchoredPosition = new Vector2(S(10), 0);
        var ti = Icon(tile.gameObject, Spr(icon), Color.white, S(28)); ti.rectTransform.anchorMin = ti.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        var tt = Text(row, "T", title, S(13), LobbyTheme.TextWhite, TextAlignmentOptions.TopLeft, true); PlaceLR(tt.gameObject, S(12), S(20), S(68), S(116)); tt.alignment = TextAlignmentOptions.TopLeft;
        var track = Rect(row, "Track", LobbyTheme.InsetDark, BarCorner); var rt = track.rectTransform; rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0, 0); rt.pivot = new Vector2(0, 0); rt.sizeDelta = new Vector2(S(168), S(12)); rt.anchoredPosition = new Vector2(S(68), S(14));
        var fill = Rect(track.gameObject, "Fill", LobbyTheme.Green, BarCorner); var fRt = fill.rectTransform; fRt.anchorMin = new Vector2(0, 0); fRt.anchorMax = new Vector2(progress / 100f, 1); fRt.offsetMin = Vector2.zero; fRt.offsetMax = Vector2.zero;
        // reward: coin then amount, right-aligned, clear of the action button
        var rTxt = Text(row, "R", reward, S(12), LobbyTheme.TextGold, TextAlignmentOptions.Right, true); rTxt.enableWordWrapping = false;
        var rrt = rTxt.rectTransform; rrt.anchorMin = rrt.anchorMax = new Vector2(1, 1); rrt.pivot = new Vector2(1, 1); rrt.sizeDelta = new Vector2(S(52), S(22)); rrt.anchoredPosition = new Vector2(-S(120), -S(12));
        var rIco = Icon(row, Spr("coin"), Color.white, S(20)); var irt = rIco.rectTransform; irt.anchorMin = irt.anchorMax = new Vector2(1, 1); irt.pivot = new Vector2(1, 1); irt.anchoredPosition = new Vector2(-S(176), -S(11));
        bool claim = progress >= 100;
        var btn = BuildButton(row.transform, "Action", claim ? "button_green" : "button_dark", claim ? LobbyTheme.Green : LobbyTheme.Dark, claim ? LobbyTheme.GreenBevel : LobbyTheme.DarkBevel, claim ? "CLAIM" : "GO", S(13), Color.white);
        var bRt = btn.GetComponent<RectTransform>(); bRt.anchorMin = new Vector2(1, 0.5f); bRt.anchorMax = new Vector2(1, 0.5f); bRt.pivot = new Vector2(1, 0.5f); bRt.sizeDelta = new Vector2(S(96), S(44)); bRt.anchoredPosition = new Vector2(-S(10), 0);
        return y + S(72) + S(8);
    }

    // ════════════════════════════ bottom nav ═════════════════════════════════

    static LobbyBottomBar BuildBottomNav(Transform bottomBarGO, LobbyHorizontalScroll hscroll)
    {
        var barRt = bottomBarGO.GetComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0, 0); barRt.anchorMax = new Vector2(1, 0); barRt.pivot = new Vector2(0.5f, 0); barRt.anchoredPosition = Vector2.zero; barRt.sizeDelta = new Vector2(0, NavH);
        // bar bg: vertical gradient per spec §6 (#2C3F86 → #21317A)
        var bg = bottomBarGO.GetComponent<Image>(); if (bg == null) bg = bottomBarGO.gameObject.AddComponent<Image>();
        bg.sprite = BackgroundSprite("nav_bg", LobbyTheme.Hex("#2C3F86"), LobbyTheme.Hex("#21317A"), 0f); bg.type = Image.Type.Simple; bg.color = Color.white;
        var nav = bottomBarGO.GetComponent<LobbyBottomBar>(); if (nav == null) nav = bottomBarGO.gameObject.AddComponent<LobbyBottomBar>();

        // soft top shadow (dark feather just above the bar) then the 3px gold top border
        var topShadow = Rect(bottomBarGO.gameObject, "TopShadow", new Color(0, 0, 0, 0.22f), S(4));
        var shrt = topShadow.rectTransform; shrt.anchorMin = new Vector2(0, 1); shrt.anchorMax = new Vector2(1, 1); shrt.pivot = new Vector2(0.5f, 1); shrt.offsetMin = new Vector2(0, -S(8)); shrt.offsetMax = new Vector2(0, S(6));
        var border = Rect(bottomBarGO.gameObject, "TopBorder", LobbyTheme.Hex("#F5B324"), S(2));
        var brt = border.rectTransform; brt.anchorMin = new Vector2(0, 1); brt.anchorMax = new Vector2(1, 1); brt.pivot = new Vector2(0.5f, 1); brt.offsetMin = new Vector2(0, -S(3)); brt.offsetMax = Vector2.zero;

        string[] labels = { "Store", "Heroes", "Play", "Guild", "Missions" };
        string[] icons  = { "bag", "head", "swords", "shield", "scroll" };
        var tabs = new List<LobbyBottomBar.Tab>();
        for (int i = 0; i < 5; i++)
        {
            bool center = i == 2;
            var tabGO = NewUI("Tab_" + labels[i], bottomBarGO);
            var rt = tabGO.GetComponent<RectTransform>(); rt.anchorMin = new Vector2(i / 5f, 0); rt.anchorMax = new Vector2((i + 1) / 5f, 1); rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var btn = tabGO.AddComponent<Button>(); var raycast = tabGO.AddComponent<Image>(); raycast.color = new Color(0, 0, 0, 0); btn.targetGraphic = raycast;
            var tab = new LobbyBottomBar.Tab { button = btn, elevated = center };
            if (center)
            {
                // elevated play: rim + gold disc raised so the gold line meets its base (notch look)
                var rim = Circle(tabGO, "Rim", LobbyTheme.GoldBevel, S(64));
                var rrt = rim.rectTransform; rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 1); rrt.pivot = new Vector2(0.5f, 0.5f); rrt.anchoredPosition = new Vector2(0, S(8));
                var rsh = rim.gameObject.AddComponent<Shadow>(); rsh.effectColor = new Color(0, 0, 0, 0.35f); rsh.effectDistance = new Vector2(0, -S(6)); rsh.useGraphicAlpha = true;
                var circle = Circle(tabGO, "Circle", LobbyTheme.Gold, S(58));
                var crt = circle.rectTransform; crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 1); crt.pivot = new Vector2(0.5f, 0.5f); crt.anchoredPosition = new Vector2(0, S(8));
                var hi = Circle(circle.gameObject, "Hi", new Color(1f, 1f, 1f, 0.30f), S(46)); // soft top highlight
                var hrt = hi.rectTransform; hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0.5f); hrt.pivot = new Vector2(0.5f, 0.5f); hrt.anchoredPosition = new Vector2(0, S(6));
                var ic = Icon(circle.gameObject, Glyph(icons[i]), Color.white, S(32)); ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(0.5f, 0.5f); ic.rectTransform.anchoredPosition = Vector2.zero; tab.icon = ic;
                var lbl = Text(tabGO, "Label", labels[i], S(10.5f), LobbyTheme.TextGoldHi, TextAlignmentOptions.Center, true);
                var lrt = lbl.rectTransform; lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(1, 0); lrt.pivot = new Vector2(0.5f, 0); lrt.sizeDelta = new Vector2(0, S(16)); lrt.anchoredPosition = new Vector2(0, S(9)); tab.label = lbl;
            }
            else
            {
                var tile = SlicedImage(tabGO, "Tile", Spr("button_gold"), Color.white, ButtonCorner);
                var trt = tile.rectTransform; trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f); trt.pivot = new Vector2(0.5f, 0.5f); trt.sizeDelta = new Vector2(S(44), S(36)); trt.anchoredPosition = new Vector2(0, S(9)); tile.enabled = false;
                var ic = Icon(tabGO, Glyph(icons[i]), LobbyTheme.Hex("#A9B3E2"), S(26)); ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(0.5f, 0.5f); ic.rectTransform.anchoredPosition = new Vector2(0, S(9));
                var lbl = Text(tabGO, "Label", labels[i], S(10.5f), LobbyTheme.TextMutedNav, TextAlignmentOptions.Center, true);
                var lrt = lbl.rectTransform; lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(1, 0); lrt.pivot = new Vector2(0.5f, 0); lrt.sizeDelta = new Vector2(0, S(16)); lrt.anchoredPosition = new Vector2(0, S(9));
                tab.tile = tile; tab.icon = ic; tab.label = lbl;
            }
            tabs.Add(tab);
        }
        // draw the elevated center on top so the gold line passes behind its disc
        bottomBarGO.Find("Tab_Play")?.SetAsLastSibling();
        var nso = new SerializedObject(nav);
        nso.FindProperty("_mainScroll").objectReferenceValue = hscroll;
        var arr = nso.FindProperty("_tabs"); arr.arraySize = tabs.Count;
        for (int i = 0; i < tabs.Count; i++)
        {
            var e = arr.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("button").objectReferenceValue = tabs[i].button;
            e.FindPropertyRelative("icon").objectReferenceValue = tabs[i].icon;
            e.FindPropertyRelative("tile").objectReferenceValue = tabs[i].tile;
            e.FindPropertyRelative("label").objectReferenceValue = tabs[i].label;
            e.FindPropertyRelative("elevated").boolValue = tabs[i].elevated;
        }
        nso.ApplyModifiedPropertiesWithoutUndo();
        return nav;
    }

    // ════════════════════════════ helpers ════════════════════════════════════

    static GameObject NewUI(string name, GameObject parent) { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent.transform, false); return go; }
    static GameObject NewUI(string name, Component parent) => NewUI(name, parent.gameObject);

    static GameObject Place(GameObject go, float top, float height, float pad)
    { var rt = go.GetComponent<RectTransform>(); rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1); rt.anchoredPosition = new Vector2(0, -top); rt.sizeDelta = new Vector2(-2 * pad, height); return go; }

    static GameObject PlaceLR(GameObject go, float top, float height, float left, float right)
    { var rt = go.GetComponent<RectTransform>(); rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1); rt.offsetMin = new Vector2(left, -top - height); rt.offsetMax = new Vector2(-right, -top); return go; }

    static void Slice(Image img, float cornerPx)
    { img.type = Image.Type.Sliced; img.fillCenter = true; var b = img.sprite != null ? img.sprite.border : Vector4.zero; float avg = (b.x + b.y + b.z + b.w) / 4f; img.pixelsPerUnitMultiplier = avg > 1f ? Mathf.Max(0.05f, avg / Mathf.Max(2f, cornerPx)) : 1f; }

    static Image SlicedImage(GameObject parent, string name, Sprite sprite, Color color, float cornerPx) { var go = NewUI(name, parent); var img = go.AddComponent<Image>(); img.sprite = sprite; img.color = color; Slice(img, cornerPx); return img; }
    static Image SlicedImage(Component parent, string name, Sprite sprite, Color color, float cornerPx) => SlicedImage(parent.gameObject, name, sprite, color, cornerPx);

    static Image Rect(GameObject parent, string name, Color color, float cornerPx) { var go = NewUI(name, parent); var img = go.AddComponent<Image>(); img.sprite = RoundRectSprite(); img.color = color; Slice(img, cornerPx); return img; }
    static Image Rect(Component parent, string name, Color color, float cornerPx) => Rect(parent.gameObject, name, color, cornerPx);

    static Image Icon(GameObject parent, Sprite s, Color c, float size) { var go = NewUI("Icon", parent); var img = go.AddComponent<Image>(); img.sprite = s; img.color = c; img.preserveAspect = s != null; img.raycastTarget = false; var rt = go.GetComponent<RectTransform>(); if (size > 0) { rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(size, size); } return img; }
    static Image Icon(Component parent, Sprite s, Color c, float size) => Icon(parent.gameObject, s, c, size);

    static Image Circle(GameObject parent, string name, Color color, float size) { var go = NewUI(name, parent); var img = go.AddComponent<Image>(); img.sprite = CircleSprite(); img.color = color; img.type = Image.Type.Simple; go.GetComponent<RectTransform>().sizeDelta = new Vector2(size, size); return img; }
    static Image Circle(Component parent, string name, Color color, float size) => Circle(parent.gameObject, name, color, size);

    static TMP_Text Text(GameObject parent, string name, string text, float size, Color color, TextAlignmentOptions align, bool bold)
    { var go = NewUI(name, parent); var t = go.AddComponent<TextMeshProUGUI>(); t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.enableWordWrapping = true; t.raycastTarget = false; if (bold) t.fontStyle = FontStyles.Bold; return t; }
    static TMP_Text Text(Component parent, string name, string text, float size, Color color, TextAlignmentOptions align, bool bold) => Text(parent.gameObject, name, text, size, color, align, bold);

    static GameObject BuildPanel(GameObject parent, string name, string sprite, Color tint, float top, float height, float pad) { var img = SlicedImage(parent, name, Spr(sprite), tint, PanelCorner); Place(img.gameObject, top, height, pad); return img.gameObject; }
    static GameObject BuildPanel(RectTransform parent, string name, string sprite, Color tint, float top, float height, float pad) => BuildPanel(parent.gameObject, name, sprite, tint, top, height, pad);

    static Button BuildButton(Transform parent, string name, string sprite, Color tint, Color bevel, string text, float fontSize, Color textColor)
    {
        var img = SlicedImage(parent.gameObject, name, Spr(sprite), Color.white, ButtonCorner); var go = img.gameObject;
        var sh = go.AddComponent<Shadow>(); sh.effectColor = bevel; sh.effectDistance = new Vector2(0, -S(4)); sh.useGraphicAlpha = false;
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        if (!string.IsNullOrEmpty(text)) { var t = Text(go, "Label", text, fontSize, textColor, TextAlignmentOptions.Center, true); t.enableWordWrapping = false; Stretch(t.rectTransform); }
        return btn;
    }

    static Button SolidButton(Transform parent, string name, Color color, Color bevel, string text, float fontSize, Color textColor)
    {
        var img = Rect(parent.gameObject, name, color, ButtonCorner); var go = img.gameObject;
        var sh = go.AddComponent<Shadow>(); sh.effectColor = bevel; sh.effectDistance = new Vector2(0, -S(4)); sh.useGraphicAlpha = false;
        var btn = go.AddComponent<Button>(); btn.targetGraphic = img;
        if (!string.IsNullOrEmpty(text)) { var t = Text(go, "Label", text, fontSize, textColor, TextAlignmentOptions.Center, true); t.enableWordWrapping = false; Stretch(t.rectTransform); }
        return btn;
    }

    // centered icon+label group inside a button
    static void IconText(GameObject button, Sprite icon, Color iconColor, float iconSize, string text, float fontSize, Color textColor)
    {
        var row = NewUI("IconText", button);
        var rt = row.GetComponent<RectTransform>(); Stretch(rt);
        var hlg = row.AddComponent<HorizontalLayoutGroup>(); hlg.childAlignment = TextAnchor.MiddleCenter; hlg.spacing = S(10);
        hlg.childControlWidth = true; hlg.childControlHeight = true; hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        var ig = NewUI("Ico", row); var img = ig.AddComponent<Image>(); img.sprite = icon; img.color = iconColor; img.preserveAspect = true; img.raycastTarget = false;
        var le = ig.AddComponent<LayoutElement>(); le.preferredWidth = iconSize; le.preferredHeight = iconSize;
        var t = Text(row, "Label", text, fontSize, textColor, TextAlignmentOptions.Center, true); t.enableWordWrapping = false;
    }

    static GameObject Stars(Transform parent, int rarity)
    {
        rarity = Mathf.Clamp(rarity, 1, 5);
        var go = NewUI("Stars", parent);
        var hlg = go.AddComponent<HorizontalLayoutGroup>(); hlg.spacing = S(2); hlg.childAlignment = TextAnchor.MiddleCenter; hlg.childControlWidth = false; hlg.childControlHeight = false; hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        var star = StarSprite();
        for (int i = 0; i < 5; i++) { var s = NewUI("S" + i, go); var img = s.AddComponent<Image>(); img.sprite = star; img.preserveAspect = true; img.raycastTarget = false; img.color = i < rarity ? LobbyTheme.Coin : new Color(1, 1, 1, 0.22f); s.GetComponent<RectTransform>().sizeDelta = new Vector2(S(14), S(14)); }
        return go;
    }

    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; }

    // Column c of `cols`, filling the full height + its share of the parent's full width (gap between cols).
    // Resolution-independent: always spans the whole row, never left-aligned.
    static void ColCell(RectTransform rt, int c, int cols, float gap)
    {
        rt.anchorMin = new Vector2(c / (float)cols, 0f); rt.anchorMax = new Vector2((c + 1) / (float)cols, 1f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(c == 0 ? 0f : gap * 0.5f, 0f); rt.offsetMax = new Vector2(c == cols - 1 ? 0f : -gap * 0.5f, 0f);
    }
    // Grid cell: column c of `cols` (full-width share), row r, fixed height cardH, anchored to grid top.
    static void GridCell(RectTransform rt, int c, int cols, int r, float cardH, float gap)
    {
        rt.anchorMin = new Vector2(c / (float)cols, 1f); rt.anchorMax = new Vector2((c + 1) / (float)cols, 1f); rt.pivot = new Vector2(0.5f, 1f);
        float topY = r * (cardH + gap);
        rt.offsetMin = new Vector2(c == 0 ? 0f : gap * 0.5f, -(topY + cardH)); rt.offsetMax = new Vector2(c == cols - 1 ? 0f : -gap * 0.5f, -topY);
    }
    static void AddSectionLabel(RectTransform content, string text, ref float y) { AddSectionLabelAt(content, text, y); y += S(34); }
    static void AddSectionLabelAt(RectTransform content, string text, float y) { var t = Text(content, "Section", text, S(14), LobbyTheme.TextLight, TextAlignmentOptions.Left, true); Place(t.gameObject, y, S(24), S(16)); }
    static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);
    static void ClearChildren(Transform t) { for (int i = t.childCount - 1; i >= 0; i--) Object.DestroyImmediate(t.GetChild(i).gameObject); }

    // ── sprites / assets ───────────────────────────────────────────────────────

    static readonly string[] HeroNames = { "sproutling", "embertail", "bumblefuzz", "tweetle", "plumpkin", "frostnip", "cindersnap", "mossback" };

    static void PopulateUiAssets()
    {
        const string contentDir = "Assets/Core/Lobby/Content";
        const string resDir = contentDir + "/Resources";
        if (!AssetDatabase.IsValidFolder(resDir)) AssetDatabase.CreateFolder(contentDir, "Resources");
        string path = resDir + "/LobbyUiAssets.asset";
        var so = AssetDatabase.LoadAssetAtPath<LobbyUiAssets>(path);
        if (so == null) { so = ScriptableObject.CreateInstance<LobbyUiAssets>(); AssetDatabase.CreateAsset(so, path); }
        so.panelBlue = Spr("panel_blue"); so.panelGold = Spr("panel_gold"); so.panelPurple = Spr("panel_purple"); so.pillDark = Spr("pill_dark");
        so.buttonGold = Spr("button_gold"); so.buttonGreen = Spr("button_green"); so.buttonBlue = Spr("button_blue"); so.buttonDark = Spr("button_dark");
        so.back = Spr("back"); so.bolt = Spr("bolt"); so.trophy = Spr("trophy"); so.swords = Spr("swords"); so.star = StarSprite();
        so.background = BackgroundSprite("champ_bg", LobbyTheme.BgTopHome, LobbyTheme.BgBottom, 0.6f);
        var list = new List<LobbyUiAssets.NamedSprite>();
        foreach (var n in HeroNames) list.Add(new LobbyUiAssets.NamedSprite { name = n, sprite = Spr(n) });
        list.Add(new LobbyUiAssets.NamedSprite { name = "brawler", sprite = Spr("cindersnap") });
        list.Add(new LobbyUiAssets.NamedSprite { name = "mage", sprite = Spr("frostnip") });
        so.heroes = list.ToArray();
        EditorUtility.SetDirty(so); AssetDatabase.SaveAssets();
    }

    static void LoadSprites()
    {
        _sprites = new Dictionary<string, Sprite>();
        _glyphs = new Dictionary<string, Sprite>();
        foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { SpritesRoot }))
        { var p = AssetDatabase.GUIDToAssetPath(guid); var s = AssetDatabase.LoadAssetAtPath<Sprite>(p); if (s != null) _sprites[Path.GetFileNameWithoutExtension(p)] = s; }
    }

    static Sprite Spr(string name) { if (string.IsNullOrEmpty(name)) return null; if (_sprites == null) LoadSprites(); return _sprites.TryGetValue(name, out var s) ? s : null; }

    // Flat white silhouette of a colorful icon (keeps alpha, RGB→white) so nav glyphs tint uniformly
    // to one muted/selected/white color instead of each keeping its baked hue.
    static Dictionary<string, Sprite> _glyphs;
    static Sprite Glyph(string iconName)
    {
        if (_glyphs == null) _glyphs = new Dictionary<string, Sprite>();
        if (_glyphs.TryGetValue(iconName, out var cached)) return cached;
        var src = Spr(iconName);
        if (src == null) return null;
        EnsureGenDir();
        string path = $"{GenDir}/glyph_{iconName}.png";
        var srcTex = src.texture; int tw = srcTex.width, th = srcTex.height;
        var rtTmp = RenderTexture.GetTemporary(tw, th, 0, RenderTextureFormat.ARGB32);
        var prevActive = RenderTexture.active;
        Graphics.Blit(srcTex, rtTmp);
        RenderTexture.active = rtTmp;
        var read = new Texture2D(tw, th, TextureFormat.RGBA32, false);
        read.ReadPixels(new Rect(0, 0, tw, th), 0, 0); read.Apply();
        RenderTexture.active = prevActive; RenderTexture.ReleaseTemporary(rtTmp);
        var r = src.textureRect; int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
        int x0 = Mathf.RoundToInt(r.x), y0 = Mathf.RoundToInt(r.y);
        var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = read.GetPixels(x0, y0, w, h);
        for (int i = 0; i < px.Length; i++) px[i] = new Color(1f, 1f, 1f, px[i].a);
        outTex.SetPixels(px); outTex.Apply();
        File.WriteAllBytes(path, outTex.EncodeToPNG());
        Object.DestroyImmediate(read); Object.DestroyImmediate(outTex);
        AssetDatabase.ImportAsset(path); ConfigureGenSprite(path);
        var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        _glyphs[iconName] = sp; return sp;
    }

    // vertical gradient + soft upper-center radial glow → the bright, happy backdrop
    static Sprite BackgroundSprite(string name, Color top, Color bottom, float glowStrength)
    {
        EnsureGenDir();
        string path = $"{GenDir}/{name}.png";
        int w = 180, h = 320; var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Vector2 c = new Vector2(0.5f, 0.66f); // glow center (normalized, y from bottom)
        for (int yy = 0; yy < h; yy++)
            for (int xx = 0; xx < w; xx++)
            {
                Color baseC = Color.Lerp(bottom, top, yy / (float)(h - 1));
                if (glowStrength > 0f)
                {
                    float nx = (xx / (float)(w - 1) - c.x), ny = (yy / (float)(h - 1) - c.y);
                    float d = Mathf.Sqrt(nx * nx + ny * ny) / 0.62f;
                    float g = Mathf.Clamp01(1f - d); g = g * g; // soft falloff
                    baseC = Color.Lerp(baseC, LobbyTheme.BgGlow, g * glowStrength);
                }
                tex.SetPixel(xx, yy, baseC);
            }
        tex.Apply(); File.WriteAllBytes(path, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path); ConfigureGenSprite(path);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Sprite _circle;
    static Sprite CircleSprite()
    {
        if (_circle != null) return _circle;
        EnsureGenDir(); string path = $"{GenDir}/circle.png";
        if (!File.Exists(path))
        {
            int n = 128; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false); float r = n / 2f - 1f; Vector2 ctr = new Vector2(n / 2f, n / 2f);
            for (int yy = 0; yy < n; yy++) for (int xx = 0; xx < n; xx++) { float d = Vector2.Distance(new Vector2(xx, yy), ctr); float a = Mathf.Clamp01(r - d); tex.SetPixel(xx, yy, new Color(1, 1, 1, a >= 1 ? 1 : a)); }
            tex.Apply(); File.WriteAllBytes(path, tex.EncodeToPNG()); Object.DestroyImmediate(tex); AssetDatabase.ImportAsset(path); ConfigureGenSprite(path);
        }
        _circle = AssetDatabase.LoadAssetAtPath<Sprite>(path); return _circle;
    }

    static Sprite _round;
    static Sprite RoundRectSprite()
    {
        if (_round != null) return _round;
        EnsureGenDir(); string path = $"{GenDir}/roundrect.png"; int rad = 28;
        if (!File.Exists(path))
        {
            int n = 96; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int yy = 0; yy < n; yy++) for (int xx = 0; xx < n; xx++)
            { float dx = Mathf.Max(rad - xx, xx - (n - 1 - rad), 0f); float dy = Mathf.Max(rad - yy, yy - (n - 1 - rad), 0f); float a = 1f; if (dx > 0 && dy > 0) { float d = Mathf.Sqrt(dx * dx + dy * dy); a = Mathf.Clamp01(rad - d + 0.5f); } tex.SetPixel(xx, yy, new Color(1, 1, 1, a)); }
            tex.Apply(); File.WriteAllBytes(path, tex.EncodeToPNG()); Object.DestroyImmediate(tex); AssetDatabase.ImportAsset(path);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter; if (ti != null) { ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.alphaIsTransparency = true; ti.sRGBTexture = true; ti.spriteBorder = new Vector4(rad, rad, rad, rad); ti.SaveAndReimport(); }
        }
        _round = AssetDatabase.LoadAssetAtPath<Sprite>(path); return _round;
    }

    static Sprite _star;
    static Sprite StarSprite()
    {
        if (_star != null) return _star;
        EnsureGenDir(); string path = $"{GenDir}/star.png";
        if (!File.Exists(path))
        {
            int n = 128; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false); var pts = new Vector2[10]; Vector2 ctr = new Vector2(n / 2f, n / 2f); float outer = n / 2f - 2f, inner = outer * 0.42f;
            for (int i = 0; i < 10; i++) { float ang = Mathf.PI / 2f + i * Mathf.PI / 5f; float rr = (i % 2 == 0) ? outer : inner; pts[i] = ctr + new Vector2(Mathf.Cos(ang) * rr, Mathf.Sin(ang) * rr); }
            for (int yy = 0; yy < n; yy++) for (int xx = 0; xx < n; xx++) tex.SetPixel(xx, yy, new Color(1, 1, 1, PointInPoly(new Vector2(xx + 0.5f, yy + 0.5f), pts) ? 1 : 0));
            tex.Apply(); File.WriteAllBytes(path, tex.EncodeToPNG()); Object.DestroyImmediate(tex); AssetDatabase.ImportAsset(path); ConfigureGenSprite(path);
        }
        _star = AssetDatabase.LoadAssetAtPath<Sprite>(path); return _star;
    }

    static bool PointInPoly(Vector2 p, Vector2[] poly)
    { bool inside = false; for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++) if (((poly[i].y > p.y) != (poly[j].y > p.y)) && (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)) inside = !inside; return inside; }

    static void EnsureGenDir() { if (!AssetDatabase.IsValidFolder(GenDir)) AssetDatabase.CreateFolder(SpritesRoot, "gen"); }
    static void ConfigureGenSprite(string path) { var ti = AssetImporter.GetAtPath(path) as TextureImporter; if (ti == null) return; ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.alphaIsTransparency = true; ti.sRGBTexture = true; ti.SaveAndReimport(); }
}
