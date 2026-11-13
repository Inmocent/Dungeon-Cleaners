#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Method 1 Procedural Pixel-Art UI Generator:
/// Creates crisp dark-dungeon pixel-art PNG sprites in Assets/Sprites/UI/,
/// configures TextureImporters (Point filter, Uncompressed, PPU=32, 9-slice borders),
/// stores Assets/Resources/UI/DungeonUITheme.asset, and applies the theme across
/// the active Canvas, all prefabs, and all scenes.
/// </summary>
[InitializeOnLoad]
public static class DungeonUIThemeGenerator
{
    private const string SpritesFolder = "Assets/Sprites/UI";
    private const string ThemeAssetPath = "Assets/Resources/UI/DungeonUITheme.asset";

    static DungeonUIThemeGenerator()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(ThemeAssetPath))
            {
                GenerateAndApplyUITheme();
            }
        };
    }

    [MenuItem("Tools/Dungeon Cleaners/Generate & Apply UI Theme")]
    public static void GenerateAndApplyUITheme()
    {
        EnsureFolder("Assets", "Sprites");
        EnsureFolder("Assets/Sprites", "UI");
        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "UI");

        // 1. Generate crisp pixel-art PNGs
        CreatePanelFrame($"{SpritesFolder}/ui_panel_frame.png");
        CreateCardFrame($"{SpritesFolder}/ui_card_frame.png");
        CreateButtonNormal($"{SpritesFolder}/ui_btn_normal.png");
        CreateButtonHover($"{SpritesFolder}/ui_btn_hover.png");
        CreateButtonPressed($"{SpritesFolder}/ui_btn_pressed.png");
        CreateBarBg($"{SpritesFolder}/ui_bar_bg.png");
        CreateBarFill($"{SpritesFolder}/ui_bar_fill.png");

        AssetDatabase.Refresh();

        // 2. Configure TextureImporters (Sprite, Single, Point, Uncompressed, PPU=32, 9-slice borders)
        ConfigureImporter($"{SpritesFolder}/ui_panel_frame.png", new Vector4(10, 10, 10, 10));
        ConfigureImporter($"{SpritesFolder}/ui_card_frame.png",  new Vector4(8, 8, 8, 8));
        ConfigureImporter($"{SpritesFolder}/ui_btn_normal.png",  new Vector4(8, 8, 8, 8));
        ConfigureImporter($"{SpritesFolder}/ui_btn_hover.png",   new Vector4(8, 8, 8, 8));
        ConfigureImporter($"{SpritesFolder}/ui_btn_pressed.png", new Vector4(8, 8, 8, 8));
        ConfigureImporter($"{SpritesFolder}/ui_bar_bg.png",      new Vector4(4, 4, 4, 4));
        ConfigureImporter($"{SpritesFolder}/ui_bar_fill.png",    new Vector4(2, 2, 2, 2));

        // 3. Create / update DungeonUITheme asset in Resources/UI/
        DungeonUITheme theme = AssetDatabase.LoadAssetAtPath<DungeonUITheme>(ThemeAssetPath);
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<DungeonUITheme>();
            AssetDatabase.CreateAsset(theme, ThemeAssetPath);
        }

        theme.panelFrame = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesFolder}/ui_panel_frame.png");
        theme.cardFrame  = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesFolder}/ui_card_frame.png");
        theme.btnNormal  = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesFolder}/ui_btn_normal.png");
        theme.btnHover   = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesFolder}/ui_btn_hover.png");
        theme.btnPressed = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesFolder}/ui_btn_pressed.png");
        theme.barBg      = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesFolder}/ui_bar_bg.png");
        theme.barFill    = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesFolder}/ui_bar_fill.png");

        EditorUtility.SetDirty(theme);
        AssetDatabase.SaveAssets();
        DungeonUITheme.ClearCache();

        // 4. Apply to currently active scene Canvas, all prefabs, and all project scenes
        ApplyThemeToScene(theme);
        ApplyToPrefabs(theme);
        ApplyToAllScenes(theme);

        AssetDatabase.SaveAssets();
        Debug.Log("<color=green><b>[DungeonUIThemeGenerator] Generated pixel-art sprites in Assets/Sprites/UI/ and applied UI Theme to active Canvas, Prefabs, and Scenes!</b></color>");
    }

    // =========================================================================
    // PROCEDURAL PIXEL-ART SPRITE GENERATORS
    // =========================================================================

    private static void CreatePanelFrame(string path)
    {
        int w = 48, h = 48;
        var tex = NewTex(w, h);

        Color ink        = Hex("#07090D");
        Color bevelTop   = Hex("#8294B0");
        Color bevelMid   = Hex("#4C586E");
        Color bevelBot   = Hex("#232A38");
        Color rivetGold  = Hex("#E3B341");
        Color stoneLight = Hex("#D8DFEC"); // Neutral light-stone so Image.color tints cleanly
        Color stoneDark  = Hex("#CCD4E3");

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (IsChamfer(x, y, w, h, 2))
                {
                    tex.SetPixel(x, y, Color.clear);
                    continue;
                }

                int d = EdgeDist(x, y, w, h);
                if (IsChamfer(x, y, w, h, 3) || d == 0)
                {
                    tex.SetPixel(x, y, ink);
                }
                else if (d == 1 || d == 2)
                {
                    bool lit = (y >= h - 3) || (x <= 2);
                    tex.SetPixel(x, y, lit ? bevelTop : bevelBot);
                }
                else if (d == 3)
                {
                    tex.SetPixel(x, y, bevelMid);
                }
                else if (d == 4)
                {
                    tex.SetPixel(x, y, ink);
                }
                else
                {
                    bool dither = ((x + y) % 4 == 0);
                    tex.SetPixel(x, y, dither ? stoneDark : stoneLight);
                }
            }
        }

        // Corner iron/gold rivets (2x2)
        PaintRivet(tex, 5, 5, rivetGold, ink);
        PaintRivet(tex, w - 7, 5, rivetGold, ink);
        PaintRivet(tex, 5, h - 7, rivetGold, ink);
        PaintRivet(tex, w - 7, h - 7, rivetGold, ink);

        SaveTex(tex, path);
    }

    private static void CreateCardFrame(string path)
    {
        int w = 48, h = 48;
        var tex = NewTex(w, h);

        Color ink       = Hex("#080A0F");
        Color goldHi    = Hex("#FCE38A");
        Color goldMid   = Hex("#C89B3C");
        Color goldLo    = Hex("#684B18");
        Color innerHi   = Hex("#F0F4FA");
        Color innerBase = Hex("#E2E8F4");

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (IsChamfer(x, y, w, h, 1))
                {
                    tex.SetPixel(x, y, Color.clear);
                    continue;
                }

                int d = EdgeDist(x, y, w, h);
                if (IsChamfer(x, y, w, h, 2) || d == 0)
                {
                    tex.SetPixel(x, y, ink);
                }
                else if (d == 1)
                {
                    tex.SetPixel(x, y, (y >= h - 3 || x <= 2) ? goldHi : goldLo);
                }
                else if (d == 2)
                {
                    tex.SetPixel(x, y, goldMid);
                }
                else if (d == 3)
                {
                    tex.SetPixel(x, y, ink);
                }
                else if (d == 4 && (y >= h - 6 || x <= 5))
                {
                    tex.SetPixel(x, y, innerHi);
                }
                else
                {
                    tex.SetPixel(x, y, innerBase);
                }
            }
        }

        SaveTex(tex, path);
    }

    private static void CreateButtonNormal(string path)
    {
        CreateButtonPng(path,
            topHi: Hex("#FFFFFF"),
            body: Hex("#E4EAF5"),
            sideShade: Hex("#94A3B8"),
            bottomLip: Hex("#475569"),
            notch: Hex("#FCD34D"),
            pressedOffset: false);
    }

    private static void CreateButtonHover(string path)
    {
        CreateButtonPng(path,
            topHi: Hex("#FFFFFF"),
            body: Hex("#F8FAFC"),
            sideShade: Hex("#CBD5E1"),
            bottomLip: Hex("#64748B"),
            notch: Hex("#FEF08A"),
            pressedOffset: false);
    }

    private static void CreateButtonPressed(string path)
    {
        CreateButtonPng(path,
            topHi: Hex("#64748B"),
            body: Hex("#B8C3D4"),
            sideShade: Hex("#64748B"),
            bottomLip: Hex("#334155"),
            notch: Hex("#D97706"),
            pressedOffset: true);
    }

    private static void CreateButtonPng(string path, Color topHi, Color body, Color sideShade, Color bottomLip, Color notch, bool pressedOffset)
    {
        int w = 48, h = 32;
        var tex = NewTex(w, h);
        Color ink = Hex("#06080C");

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (IsChamfer(x, y, w, h, 1))
                {
                    tex.SetPixel(x, y, Color.clear);
                    continue;
                }

                int d = EdgeDist(x, y, w, h);
                if (IsChamfer(x, y, w, h, 2) || d == 0)
                {
                    tex.SetPixel(x, y, ink);
                }
                else if (pressedOffset)
                {
                    if (y >= h - 3) tex.SetPixel(x, y, bottomLip);
                    else if (y <= 2) tex.SetPixel(x, y, topHi);
                    else if (x == 1 || x == w - 2) tex.SetPixel(x, y, sideShade);
                    else tex.SetPixel(x, y, body);
                }
                else
                {
                    if (y == h - 2 || y == h - 3) tex.SetPixel(x, y, topHi);
                    else if (y >= 1 && y <= 3) tex.SetPixel(x, y, bottomLip);
                    else if (x == 1 || x == w - 2) tex.SetPixel(x, y, sideShade);
                    else tex.SetPixel(x, y, body);
                }
            }
        }

        // Left & right chunky pixel notches
        for (int yy = h / 2 - 2; yy <= h / 2 + 2; yy++)
        {
            tex.SetPixel(2, yy, notch);
            tex.SetPixel(w - 3, yy, notch);
        }

        SaveTex(tex, path);
    }

    private static void CreateBarBg(string path)
    {
        int w = 32, h = 16;
        var tex = NewTex(w, h);
        Color ink = Hex("#06080C");
        Color shadow = Hex("#121621");
        Color trough = Hex("#22293A");

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int d = EdgeDist(x, y, w, h);
                if (d == 0) tex.SetPixel(x, y, ink);
                else if (y >= h - 3 || x == 1) tex.SetPixel(x, y, shadow);
                else tex.SetPixel(x, y, trough);
            }
        }
        SaveTex(tex, path);
    }

    private static void CreateBarFill(string path)
    {
        int w = 16, h = 16;
        var tex = NewTex(w, h);
        Color spec = Hex("#FFFFFF");
        Color mid  = Hex("#E5E7EB");
        Color low  = Hex("#9CA3AF");

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (y >= h - 3) tex.SetPixel(x, y, spec);
                else if (y <= 2) tex.SetPixel(x, y, low);
                else tex.SetPixel(x, y, (x % 4 == 0) ? low : mid);
            }
        }
        SaveTex(tex, path);
    }

    // =========================================================================
    // APPLY TO SCENE, PREFABS, AND ALL SCENES
    // =========================================================================

    private static void ApplyToPrefabs(DungeonUITheme theme)
    {
        string[] prefabPaths =
        {
            "Assets/Resources/Prefabs/HeroCardPrefab.prefab",
            "Assets/Resources/Prefabs/TeamSlotPrefab.prefab",
            "Assets/Resources/Prefabs/GridSlotPrefab.prefab",
            "Assets/Resources/Prefabs/PortraitPrefab.prefab",
            "Assets/Resources/Prefabs/RewardCardPrefab.prefab",
            "Assets/Resources/Prefabs/RestHeroRowPrefab.prefab",
            "Assets/Resources/Prefabs/SurvivorPortraitPrefab.prefab"
        };

        foreach (string path in prefabPaths)
        {
            if (!File.Exists(path)) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) continue;

            if (path.Contains("HeroCardPrefab"))
            {
                var card = root.GetComponent<HeroCardUI>();
                if (card != null && card.cardBackground != null)
                {
                    card.cardBackground.sprite = theme.cardFrame;
                    card.cardBackground.type = Image.Type.Sliced;
                }
                foreach (var sl in root.GetComponentsInChildren<Slider>(true))
                {
                    Color c = sl.name.ToLowerInvariant().Contains("hp") ? theme.hpGreen : theme.energyBlue;
                    ApplySliderSprites(sl, theme, c);
                }
            }
            else if (path.Contains("TeamSlotPrefab"))
            {
                var slot = root.GetComponent<TeamSlotUI>();
                if (slot != null && slot.slotBackground != null)
                {
                    slot.slotBackground.sprite = theme.panelFrame;
                    slot.slotBackground.type = Image.Type.Sliced;
                }
                if (slot != null && slot.actionButton != null)
                {
                    ApplyButtonComponent(slot.actionButton, theme, null);
                }
            }
            else if (path.Contains("GridSlotPrefab"))
            {
                var gs = root.GetComponent<GridSlotUI>();
                if (gs != null && gs.background != null)
                {
                    gs.background.sprite = theme.cardFrame;
                    gs.background.type = Image.Type.Sliced;
                }
                if (gs != null && gs.selectionOutline != null)
                {
                    gs.selectionOutline.sprite = theme.panelFrame;
                    gs.selectionOutline.type = Image.Type.Sliced;
                }
            }
            else if (path.Contains("PortraitPrefab"))
            {
                var rootImg = root.GetComponent<Image>() ?? root.AddComponent<Image>();
                rootImg.sprite = theme.cardFrame;
                rootImg.type = Image.Type.Sliced;
                rootImg.color = new Color(0.14f, 0.16f, 0.22f, 0.96f);

                foreach (var sl in root.GetComponentsInChildren<Slider>(true))
                {
                    Color c = sl.name.ToLowerInvariant().Contains("hp") ? theme.hpGreen : theme.energyBlue;
                    ApplySliderSprites(sl, theme, c);
                }
            }
            else if (path.Contains("RewardCardPrefab"))
            {
                var rc = root.GetComponent<RewardCardUI>();
                if (rc != null && rc.cardBackground != null)
                {
                    rc.cardBackground.sprite = theme.cardFrame;
                    rc.cardBackground.type = Image.Type.Sliced;
                }
                if (rc != null && rc.selectionBorder != null)
                {
                    rc.selectionBorder.sprite = theme.panelFrame;
                    rc.selectionBorder.type = Image.Type.Sliced;
                }
            }
            else if (path.Contains("RestHeroRowPrefab"))
            {
                var img = root.GetComponent<Image>();
                if (img != null)
                {
                    img.sprite = theme.panelFrame;
                    img.type = Image.Type.Sliced;
                }
            }
            else if (path.Contains("SurvivorPortraitPrefab"))
            {
                var rootImg = root.GetComponent<Image>() ?? root.AddComponent<Image>();
                rootImg.sprite = theme.cardFrame;
                rootImg.type = Image.Type.Sliced;
                rootImg.color = theme.panelSlate;
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ApplyToAllScenes(DungeonUITheme theme)
    {
        string activeScenePath = EditorSceneManager.GetActiveScene().path;
        string[] scenes =
        {
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/SquadBuilder.unity",
            "Assets/Scenes/Formation.unity",
            "Assets/Scenes/Combat.unity",
            "Assets/Scenes/Rewards.unity",
            "Assets/Scenes/GameOver.unity"
        };

        foreach (string scenePath in scenes)
        {
            if (!File.Exists(scenePath)) continue;
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            ApplyThemeToScene(theme);
            EditorSceneManager.SaveScene(scene);
        }

        if (!string.IsNullOrEmpty(activeScenePath) && File.Exists(activeScenePath))
        {
            EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
        }
    }

    public static void ApplyThemeToScene(DungeonUITheme theme)
    {
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }

            foreach (var btn in canvas.GetComponentsInChildren<Button>(true))
            {
                string bn = btn.gameObject.name.ToLowerInvariant();
                Color? tint = null;
                if (bn.Contains("start") || bn.Contains("confirm") || bn.Contains("play") || bn.Contains("resume"))
                    tint = theme.btnPrimaryGreen;
                else if (bn.Contains("battle") || bn.Contains("quit"))
                    tint = theme.btnDangerRed;
                else if (bn.Contains("repo") || bn.Contains("squad"))
                    tint = theme.btnArcaneBlue;

                ApplyButtonComponent(btn, theme, tint);
            }

            foreach (var img in canvas.GetComponentsInChildren<Image>(true))
            {
                if (img.GetComponent<Button>() != null) continue;
                string n = img.gameObject.name.ToLowerInvariant();

                // Never overwrite character portrait/avatar/fill sprites
                if (n.Contains("circle") || n.Contains("avatar") || n.Contains("sprite") || n.Contains("icon") ||
                    n.Contains("fill") || n.Contains("background") && img.GetComponentInParent<Slider>() != null ||
                    n.Contains("dead") || n.Contains("checkmark") || n.Contains("banner"))
                {
                    if (n == "background" && img.GetComponentInParent<Slider>() == null)
                    {
                        img.color = theme.bgVoid;
                    }
                    continue;
                }

                if (n.Contains("card"))
                {
                    img.sprite = theme.cardFrame;
                    img.type = Image.Type.Sliced;
                }
                else if (n.Contains("panel") || n.Contains("modal") || n.Contains("box") || n.Contains("minimap") ||
                         n.Contains("grid") || n.Contains("bosshp"))
                {
                    img.sprite = theme.panelFrame;
                    img.type = Image.Type.Sliced;
                    if (img.color.a > 0.05f && !n.Contains("bosshp"))
                    {
                        img.color = n.Contains("bottom") ? theme.panelHeaderTint : theme.panelSlate;
                    }
                }
            }

            foreach (var sl in canvas.GetComponentsInChildren<Slider>(true))
            {
                Color c = sl.name.ToLowerInvariant().Contains("boss") ? theme.hpLowRed : theme.hpGreen;
                ApplySliderSprites(sl, theme, c);
            }

            foreach (var tmp in canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (tmp.fontSize < 14f) tmp.fontSize = 14f;
                if (tmp.color.a < 0.9f) tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, 1f);
            }
        }
    }

    private static void ApplyButtonComponent(Button btn, DungeonUITheme theme, Color? tint)
    {
        if (btn == null || theme == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = theme.btnNormal;
            img.type = Image.Type.Sliced;
            if (tint.HasValue) img.color = tint.Value;
        }
        btn.transition = Selectable.Transition.SpriteSwap;
        var state = btn.spriteState;
        state.highlightedSprite = theme.btnHover;
        state.selectedSprite = theme.btnHover;
        state.pressedSprite = theme.btnPressed;
        state.disabledSprite = theme.btnPressed;
        btn.spriteState = state;
    }

    private static void ApplySliderSprites(Slider slider, DungeonUITheme theme, Color fillColor)
    {
        if (slider == null || theme == null) return;
        if (slider.targetGraphic is Image bg)
        {
            bg.sprite = theme.barBg;
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;
        }
        if (slider.fillRect != null)
        {
            var fill = slider.fillRect.GetComponent<Image>();
            if (fill != null)
            {
                fill.sprite = theme.barFill;
                fill.type = Image.Type.Sliced;
                fill.color = fillColor;
            }
        }
    }

    // =========================================================================
    // HELPERS & TEXTURE IMPORTER
    // =========================================================================

    private static Texture2D NewTex(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    private static void SaveTex(Texture2D tex, string path)
    {
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    private static void ConfigureImporter(string assetPath, Vector4 border)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 32f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.spriteBorder = border;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteBorder = border;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    private static int EdgeDist(int x, int y, int w, int h) =>
        Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));

    private static bool IsChamfer(int x, int y, int w, int h, int radius)
    {
        int dx = Mathf.Min(x, w - 1 - x);
        int dy = Mathf.Min(y, h - 1 - y);
        return (dx + dy) < radius;
    }

    private static void PaintRivet(Texture2D tex, int x, int y, Color col, Color ink)
    {
        tex.SetPixel(x, y, col);
        tex.SetPixel(x + 1, y, col);
        tex.SetPixel(x, y + 1, col);
        tex.SetPixel(x + 1, y + 1, ink);
    }

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string full = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(full))
            AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
