#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using System.IO;
using System.Collections.Generic;

public static class DungeonCleanersBuilder
{
    private static TMP_FontAsset defaultFont;
    private static Sprite circleSprite;
    private static Sprite squareSprite;

    private static Sprite panelFrameSprite;
    private static Sprite cardFrameSprite;
    private static Sprite btnNormalSprite;
    private static Sprite btnHoverSprite;
    private static Sprite btnPressedSprite;
    private static Sprite barBgSprite;
    private static Sprite barFillSprite;

    private static void InitResources()
    {
        var fontGuids = AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset");
        if (fontGuids.Length > 0)
        {
            defaultFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(fontGuids[0]));
        }

        circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Sprites/Circle.png");
        squareSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Sprites/Square.png");

        panelFrameSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/ui_panel_frame.png") ?? squareSprite;
        cardFrameSprite  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/ui_card_frame.png")  ?? squareSprite;
        btnNormalSprite  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/ui_btn_normal.png")  ?? squareSprite;
        btnHoverSprite   = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/ui_btn_hover.png")   ?? btnNormalSprite;
        btnPressedSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/ui_btn_pressed.png") ?? btnNormalSprite;
        barBgSprite      = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/ui_bar_bg.png")      ?? squareSprite;
        barFillSprite    = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/ui_bar_fill.png")    ?? squareSprite;

        if (!AssetDatabase.IsValidFolder("Assets/Resources/Prefabs"))
            AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");
    }

    [MenuItem("Tools/Dungeon Cleaners/Build All Scenes and Prefabs")]
    public static void BuildAll()
    {
        InitResources();
        BuildPrefabs();
        BuildMainMenuScene();
        BuildSquadBuilderScene();
        BuildFormationScene();
        BuildCombatScene();
        BuildRewardsScene();
        BuildGameOverScene();
        ConfigureBuildSettings();

        Debug.Log("<color=green><b>Dungeon Cleaners: All Scenes, UI, and Prefabs Built Successfully!</b></color>");
    }

    // =========================================================================
    // PREFAB BUILDERS
    // =========================================================================
    public static void BuildPrefabs()
    {
        BuildHeroCardPrefab();
        BuildTeamSlotPrefab();
        BuildGridSlotPrefab();
        BuildPortraitPrefab();
        BuildRewardCardPrefab();
        BuildRestHeroRowPrefab();
        BuildSurvivorPortraitPrefab();
    }

    private static void BuildHeroCardPrefab()
    {
        var root = CreateUIRoot("HeroCard", 195, 240);
        var img = root.AddComponent<Image>();
        img.sprite = cardFrameSprite;
        img.type = Image.Type.Sliced;
        img.color = new Color(0.22f, 0.26f, 0.36f, 0.98f);

        var cardUI = root.AddComponent<HeroCardUI>();
        cardUI.cardBackground = img;

        var nameText = CreateText("Name", root.transform, "Hero Name", 17, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(nameText.rectTransform, 0, 1, 1, 1, 0, -22, -12, 28);
        cardUI.nameLabel = nameText;

        var classText = CreateText("Class", root.transform, "Class", 14, FontStyles.Normal, TextAlignmentOptions.Center);
        classText.color = new Color(0.88f, 0.90f, 0.95f);
        SetRect(classText.rectTransform, 0, 1, 1, 1, 0, -48, -12, 22);
        cardUI.classLabel = classText;

        var portraitObj = CreateUIRoot("Portrait", 74, 74);
        portraitObj.transform.SetParent(root.transform, false);
        SetRect(portraitObj.GetComponent<RectTransform>(), 0.5f, 0.5f, 0.5f, 0.5f, 0, 6, 74, 74);
        var portraitBg = portraitObj.AddComponent<Image>();
        portraitBg.sprite = panelFrameSprite;
        portraitBg.type = Image.Type.Sliced;
        portraitBg.color = new Color(0.10f, 0.12f, 0.18f, 1f);

        var iconObj = CreateUIRoot("Icon", 58, 58);
        iconObj.transform.SetParent(portraitObj.transform, false);
        SetRect(iconObj.GetComponent<RectTransform>(), 0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 58, 58);
        var iconImg = iconObj.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;
        cardUI.portraitIcon = iconImg;

        var hpSlider = CreateSlider("HPSlider", root.transform, new Color(0.24f, 0.84f, 0.34f));
        SetRect(hpSlider.GetComponent<RectTransform>(), 0.1f, 0.9f, 0, 0, 0, 48, 0, 16);
        cardUI.hpBar = hpSlider;

        var enSlider = CreateSlider("EnergySlider", root.transform, new Color(0.26f, 0.58f, 0.98f));
        SetRect(enSlider.GetComponent<RectTransform>(), 0.1f, 0.9f, 0, 0, 0, 22, 0, 16);
        cardUI.energyBar = enSlider;

        SavePrefab(root, "Assets/Resources/Prefabs/HeroCardPrefab.prefab");
    }

    private static void BuildTeamSlotPrefab()
    {
        var root = CreateUIRoot("TeamSlot", 280, 68);
        var img = root.AddComponent<Image>();
        img.sprite = panelFrameSprite;
        img.type = Image.Type.Sliced;
        img.color = new Color(0.18f, 0.20f, 0.26f, 0.98f);

        var slotUI = root.AddComponent<TeamSlotUI>();
        slotUI.slotBackground = img;

        var heroText = CreateText("HeroLabel", root.transform, "Empty Slot", 17, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        SetRect(heroText.rectTransform, 0, 0.72f, 0.45f, 1, 15, 0, 0, 0);
        slotUI.heroLabel = heroText;

        var hpText = CreateText("HPLabel", root.transform, "", 14, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        hpText.color = new Color(0.85f, 0.88f, 0.92f);
        SetRect(hpText.rectTransform, 0, 0.72f, 0, 0.45f, 15, 0, 0, 0);
        slotUI.hpLabel = hpText;

        var btnObj = CreateUIRoot("ActionButton", 48, 48);
        btnObj.transform.SetParent(root.transform, false);
        SetRect(btnObj.GetComponent<RectTransform>(), 1, 1, 0.5f, 0.5f, -34, 0, 48, 48);
        var btnImg = btnObj.AddComponent<Image>();
        btnImg.sprite = btnNormalSprite;
        btnImg.type = Image.Type.Sliced;
        btnImg.color = new Color(0.32f, 0.38f, 0.50f);
        var btn = btnObj.AddComponent<Button>();
        ApplySpriteSwap(btn);
        slotUI.actionButton = btn;

        var btnText = CreateText("BtnLabel", btnObj.transform, "+", 24, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(btnText.rectTransform, 0, 1, 0, 1, 0, 0, 0, 0);
        slotUI.actionLabel = btnText;

        SavePrefab(root, "Assets/Resources/Prefabs/TeamSlotPrefab.prefab");
    }

    private static void BuildGridSlotPrefab()
    {
        var root = CreateUIRoot("GridSlot", 150, 150);
        var img = root.AddComponent<Image>();
        img.sprite = cardFrameSprite;
        img.type = Image.Type.Sliced;
        img.color = GridSlotUI.EmptyColor;

        var outlineObj = CreateUIRoot("Outline", 150, 150);
        outlineObj.transform.SetParent(root.transform, false);
        SetRect(outlineObj.GetComponent<RectTransform>(), 0, 1, 0, 1, 0, 0, 0, 0);
        var outlineImg = outlineObj.AddComponent<Image>();
        outlineImg.sprite = panelFrameSprite;
        outlineImg.type = Image.Type.Sliced;
        outlineImg.color = GridSlotUI.NormalBorder;
        outlineImg.raycastTarget = false;

        root.AddComponent<CanvasGroup>();
        root.AddComponent<GridSlotDrag>();
        var slotUI = root.AddComponent<GridSlotUI>();
        slotUI.background = img;
        slotUI.selectionOutline = outlineImg;

        var label = CreateText("HeroLabel", root.transform, "[Slot]", 17, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(label.rectTransform, 0.06f, 0.94f, 0.45f, 0.94f, 0, 0, 0, 0);
        slotUI.heroLabel = label;

        var hpLabel = CreateText("HPLabel", root.transform, "", 14, FontStyles.Normal, TextAlignmentOptions.Center);
        SetRect(hpLabel.rectTransform, 0.06f, 0.94f, 0.1f, 0.45f, 0, 0, 0, 0);
        slotUI.hpLabel = hpLabel;

        var warnObj = CreateUIRoot("WarningIcon", 28, 28);
        warnObj.transform.SetParent(root.transform, false);
        SetRect(warnObj.GetComponent<RectTransform>(), 1, 1, 1, 1, -18, -18, 28, 28);
        var warnImg = warnObj.AddComponent<Image>();
        warnImg.sprite = circleSprite;
        warnImg.color = Color.red;
        warnImg.raycastTarget = false;
        var warnText = CreateText("!", warnObj.transform, "!", 18, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(warnText.rectTransform, 0, 1, 0, 1, 0, 0, 0, 0);
        slotUI.warningIcon = warnImg;
        warnObj.SetActive(false);

        SavePrefab(root, "Assets/Resources/Prefabs/GridSlotPrefab.prefab");
    }

    private static void BuildPortraitPrefab()
    {
        // Enlarged, non-overlapping card layout for bottom combat bar
        var root = CreateUIRoot("Portrait", 164, 160);
        var rootBg = root.AddComponent<Image>();
        rootBg.sprite = cardFrameSprite;
        rootBg.type = Image.Type.Sliced;
        rootBg.color = new Color(0.14f, 0.16f, 0.23f, 0.96f);

        var portraitUI = root.AddComponent<PortraitUI>();

        // Top Sprite Frame (66x66) — never overlapped by name text
        var circleObj = CreateUIRoot("Circle", 64, 64);
        circleObj.transform.SetParent(root.transform, false);
        SetRect(circleObj.GetComponent<RectTransform>(), 0.5f, 0.5f, 1f, 1f, 0, -38, 64, 64);
        var circleImg = circleObj.AddComponent<Image>();
        circleImg.sprite = circleSprite;
        circleImg.color = Color.white;
        portraitUI.portraitCircle = circleImg;

        // Name cleanly below portrait sprite
        var nameText = CreateText("Name", root.transform, "Hero", 14, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(nameText.rectTransform, 0.04f, 0.96f, 1f, 1f, 0, -82, 0, 20);
        portraitUI.nameLabel = nameText;

        // Upgrades Badge Row above HP bar
        var badgeObj = CreateUIRoot("UpgradesBadge", 154, 16);
        badgeObj.transform.SetParent(root.transform, false);
        SetRect(badgeObj.GetComponent<RectTransform>(), 0.03f, 0.97f, 0f, 0f, 0, 56, 0, 16);
        var badgeText = CreateText("BadgeText", badgeObj.transform, "", 11, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(badgeText.rectTransform, 0, 1, 0, 1, 0, 0, 0, 0);
        portraitUI.upgradesBadgeLabel = badgeText;

        // HP Bar with numeric HP readout inside
        var hpSlider = CreateSlider("HPBar", root.transform, new Color(0.24f, 0.84f, 0.34f));
        SetRect(hpSlider.GetComponent<RectTransform>(), 0.08f, 0.92f, 0, 0, 0, 38, 0, 16);
        portraitUI.hpBar = hpSlider;
        portraitUI.hpFill = hpSlider.fillRect.GetComponent<Image>();

        var hpNumText = CreateText("HPText", hpSlider.transform, "100/100", 11, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(hpNumText.rectTransform, 0, 1, 0, 1, 0, 0, 0, 0);
        portraitUI.hpLabel = hpNumText;

        // Energy Bar
        var enSlider = CreateSlider("ENBar", root.transform, new Color(0.26f, 0.58f, 0.98f));
        SetRect(enSlider.GetComponent<RectTransform>(), 0.08f, 0.92f, 0, 0, 0, 20, 0, 12);
        portraitUI.energyBar = enSlider;
        portraitUI.energyFill = enSlider.fillRect.GetComponent<Image>();

        // Fatigued Label
        var fatText = CreateText("FatiguedLabel", root.transform, "FATIGUED", 12, FontStyles.Bold, TextAlignmentOptions.Center);
        fatText.color = new Color(0.98f, 0.72f, 0.20f);
        SetRect(fatText.rectTransform, 0.05f, 0.95f, 0, 0, 0, 8, 0, 14);
        portraitUI.fatiguedLabel = fatText;
        fatText.gameObject.SetActive(false);

        // Dead Overlay
        var deadObj = CreateUIRoot("DeadOverlay", 64, 64);
        deadObj.transform.SetParent(circleObj.transform, false);
        SetRect(deadObj.GetComponent<RectTransform>(), 0, 1, 0, 1, 0, 0, 0, 0);
        var deadImg = deadObj.AddComponent<Image>();
        deadImg.sprite = circleSprite;
        deadImg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
        var skullText = CreateText("Skull", deadObj.transform, "X", 30, FontStyles.Bold, TextAlignmentOptions.Center);
        skullText.color = Color.red;
        SetRect(skullText.rectTransform, 0, 1, 0, 1, 0, 0, 0, 0);
        portraitUI.deadOverlay = deadImg;
        deadObj.SetActive(false);

        SavePrefab(root, "Assets/Resources/Prefabs/PortraitPrefab.prefab");
    }

    private static void BuildRewardCardPrefab()
    {
        var root = CreateUIRoot("RewardCard", 240, 330);
        var img = root.AddComponent<Image>();
        img.sprite = cardFrameSprite;
        img.type = Image.Type.Sliced;
        img.color = new Color(0.22f, 0.26f, 0.36f);
        root.AddComponent<Button>();
        var cardUI = root.AddComponent<RewardCardUI>();
        cardUI.cardBackground = img;

        var borderObj = CreateUIRoot("SelectionBorder", 248, 338);
        borderObj.transform.SetParent(root.transform, false);
        SetRect(borderObj.GetComponent<RectTransform>(), 0, 1, 0, 1, 0, 0, 8, 8);
        var borderImg = borderObj.AddComponent<Image>();
        borderImg.sprite = panelFrameSprite;
        borderImg.type = Image.Type.Sliced;
        borderImg.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        cardUI.selectionBorder = borderImg;
        borderObj.transform.SetAsFirstSibling();
        borderObj.SetActive(false);

        var typeText = CreateText("TypeLabel", root.transform, "UPGRADE", 16, FontStyles.Bold, TextAlignmentOptions.Center);
        typeText.color = new Color(1f, 0.85f, 0.2f);
        SetRect(typeText.rectTransform, 0.06f, 0.94f, 1, 1, 0, -28, 0, 30);
        cardUI.typeLabel = typeText;

        var nameText = CreateText("NameLabel", root.transform, "Camp Coffee", 20, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(nameText.rectTransform, 0.06f, 0.94f, 0.6f, 0.85f, 0, 0, 0, 0);
        cardUI.rewardNameLabel = nameText;

        var descText = CreateText("DescLabel", root.transform, "Restores 50% energy to 2 chosen heroes.", 15, FontStyles.Normal, TextAlignmentOptions.Center);
        descText.color = new Color(0.9f, 0.92f, 0.96f);
        SetRect(descText.rectTransform, 0.08f, 0.92f, 0.12f, 0.6f, 0, 0, 0, 0);
        cardUI.descriptionLabel = descText;

        SavePrefab(root, "Assets/Resources/Prefabs/RewardCardPrefab.prefab");
    }

    private static void BuildRestHeroRowPrefab()
    {
        var root = CreateUIRoot("RestHeroRow", 480, 56);
        var img = root.AddComponent<Image>();
        img.sprite = panelFrameSprite;
        img.type = Image.Type.Sliced;
        img.color = new Color(0.18f, 0.20f, 0.26f);

        var text = CreateText("HeroInfo", root.transform, "Knight Alden - HP: 85/85 EN: 20/110", 16, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        SetRect(text.rectTransform, 0, 0.8f, 0, 1, 15, 0, 0, 0);

        var toggleObj = CreateUIRoot("Toggle", 40, 40);
        toggleObj.transform.SetParent(root.transform, false);
        SetRect(toggleObj.GetComponent<RectTransform>(), 1, 1, 0.5f, 0.5f, -32, 0, 40, 40);
        var toggle = toggleObj.AddComponent<Toggle>();
        var toggleBg = toggleObj.AddComponent<Image>();
        toggleBg.sprite = cardFrameSprite;
        toggleBg.type = Image.Type.Sliced;
        toggleBg.color = new Color(0.32f, 0.36f, 0.44f);
        toggle.targetGraphic = toggleBg;

        var checkObj = CreateUIRoot("Checkmark", 24, 24);
        checkObj.transform.SetParent(toggleObj.transform, false);
        SetRect(checkObj.GetComponent<RectTransform>(), 0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 24, 24);
        var checkImg = checkObj.AddComponent<Image>();
        checkImg.sprite = barFillSprite;
        checkImg.color = Color.green;
        toggle.graphic = checkImg;

        SavePrefab(root, "Assets/Resources/Prefabs/RestHeroRowPrefab.prefab");
    }

    private static void BuildSurvivorPortraitPrefab()
    {
        var root = CreateUIRoot("SurvivorPortrait", 100, 120);
        var rootImg = root.AddComponent<Image>();
        rootImg.sprite = cardFrameSprite;
        rootImg.type = Image.Type.Sliced;
        rootImg.color = new Color(0.14f, 0.16f, 0.22f, 0.96f);

        var circleObj = CreateUIRoot("Circle", 65, 65);
        circleObj.transform.SetParent(root.transform, false);
        SetRect(circleObj.GetComponent<RectTransform>(), 0.5f, 0.5f, 1, 1, 0, -40, 65, 65);
        var img = circleObj.AddComponent<Image>();
        img.sprite = circleSprite;
        img.color = Color.gray;

        var text = CreateText("Label", root.transform, "Hero\nAlive", 13, FontStyles.Normal, TextAlignmentOptions.Center);
        SetRect(text.rectTransform, 0.05f, 0.95f, 0, 0.4f, 0, 0, 0, 0);

        SavePrefab(root, "Assets/Resources/Prefabs/SurvivorPortraitPrefab.prefab");
    }

    // =========================================================================
    // SCENE 1: MAIN MENU
    // =========================================================================
    private static void BuildMainMenuScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SetupCameraAndEventSystem();

        var canvas = CreateCanvas();
        var ctrl = canvas.gameObject.AddComponent<MainMenuController>();

        var bg = CreatePanel("Background", canvas.transform, new Color(0.05f, 0.06f, 0.09f), false);
        SetRect(bg.GetComponent<RectTransform>(), 0, 1, 0, 1, 0, 0, 0, 0);

        var leftPanel = CreatePanel("PlanningScenePanel", canvas.transform, new Color(0.14f, 0.16f, 0.22f, 0.95f));
        SetRect(leftPanel.GetComponent<RectTransform>(), 0.05f, 0.55f, 0.1f, 0.9f, 0, 0, 0, 0);

        var mapTitle = CreateText("MapTitle", leftPanel.transform, "[ RAID PLANNING TABLE ]", 24, FontStyles.Bold, TextAlignmentOptions.Center);
        mapTitle.color = new Color(0.95f, 0.85f, 0.4f);
        SetRect(mapTitle.rectTransform, 0, 1, 1, 1, 0, -40, 0, 40);

        var mapDesc = CreateText("MapDesc", leftPanel.transform,
            "[ 2D Hand-Crafted Art Placeholder ]\n\n5 Heroes gathered around a weathered dungeon map,\nplanning their raid by lantern light.\n\nKnights tracing corridors,\nMages reading ancient runes,\nHealer preparing potions.",
            18, FontStyles.Italic, TextAlignmentOptions.Center);
        mapDesc.color = new Color(0.8f, 0.82f, 0.9f);
        SetRect(mapDesc.rectTransform, 0.1f, 0.9f, 0.1f, 0.85f, 0, 0, 0, 0);

        var rightPanel = CreatePanel("MenuPanel", canvas.transform, Color.clear, false);
        SetRect(rightPanel.GetComponent<RectTransform>(), 0.6f, 0.95f, 0.1f, 0.9f, 0, 0, 0, 0);

        var title = CreateText("GameTitle", rightPanel.transform, "DUNGEON\nCLEANERS", 44, FontStyles.Bold, TextAlignmentOptions.Center);
        title.color = new Color(1f, 0.85f, 0.2f);
        SetRect(title.rectTransform, 0, 1, 1, 1, 0, -60, 0, 100);

        var skull = CreateText("Skull", rightPanel.transform, "*  *  *", 24, FontStyles.Bold, TextAlignmentOptions.Center);
        skull.color = new Color(0.85f, 0.3f, 0.3f);
        SetRect(skull.rectTransform, 0, 1, 1, 1, 0, -130, 0, 30);

        var startBtn = CreateButton("StartRunBtn", rightPanel.transform, "START RUN", 22, new Color(0.24f, 0.68f, 0.38f));
        SetRect(startBtn.GetComponent<RectTransform>(), 0.1f, 0.9f, 0, 0, 0, 240, 0, 65);
        ctrl.startRunButton = startBtn;

        var squadBtn = CreateButton("SquadBtn", rightPanel.transform, "SQUAD", 20, new Color(0.24f, 0.48f, 0.84f));
        SetRect(squadBtn.GetComponent<RectTransform>(), 0.1f, 0.9f, 0, 0, 0, 160, 0, 58);
        ctrl.squadButton = squadBtn;

        var settingsBtn = CreateButton("SettingsBtn", rightPanel.transform, "SETTINGS", 18, new Color(0.32f, 0.38f, 0.50f));
        SetRect(settingsBtn.GetComponent<RectTransform>(), 0.1f, 0.9f, 0, 0, 0, 85, 0, 54);
        ctrl.settingsButton = settingsBtn;

        var settingsModal = CreatePanel("SettingsPanel", canvas.transform, new Color(0.14f, 0.16f, 0.22f, 0.98f));
        SetRect(settingsModal.GetComponent<RectTransform>(), 0.25f, 0.75f, 0.2f, 0.8f, 0, 0, 0, 0);
        ctrl.settingsPanel = settingsModal;

        var setHeader = CreateText("SettingsHeader", settingsModal.transform, "SETTINGS", 28, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(setHeader.rectTransform, 0, 1, 1, 1, 0, -40, 0, 40);

        var closeSetBtn = CreateButton("CloseSettings", settingsModal.transform, "CLOSE", 18, new Color(0.76f, 0.24f, 0.26f));
        SetRect(closeSetBtn.GetComponent<RectTransform>(), 0.35f, 0.65f, 0, 0, 0, 45, 0, 52);
        closeSetBtn.onClick.AddListener(() => settingsModal.SetActive(false));

        settingsModal.SetActive(false);

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainMenu.unity");
    }

    // =========================================================================
    // SCENE 2: SQUAD BUILDER
    // =========================================================================
    private static void BuildSquadBuilderScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SetupCameraAndEventSystem();

        var canvas = CreateCanvas();
        var ctrl = canvas.gameObject.AddComponent<SquadBuilderController>();

        var bg = CreatePanel("Background", canvas.transform, new Color(0.05f, 0.06f, 0.09f), false);
        SetRect(bg.GetComponent<RectTransform>(), 0, 1, 0, 1, 0, 0, 0, 0);

        var backBtn = CreateButton("BackButton", canvas.transform, "< BACK", 16, new Color(0.32f, 0.38f, 0.50f));
        SetRect(backBtn.GetComponent<RectTransform>(), 0, 0, 1, 1, 80, -45, 130, 50);
        ctrl.backButton = backBtn;
        backBtn.transform.SetAsLastSibling();

        var title = CreateText("Title", canvas.transform, "BUILD YOUR SQUAD", 28, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, 0, 1, 1, 1, 0, -45, 0, 45);

        var guide = CreateText("GuideText", canvas.transform, "Pick your squad of 5 heroes (0/5)", 18, FontStyles.Italic, TextAlignmentOptions.Center);
        guide.color = new Color(0.98f, 0.82f, 0.28f);
        SetRect(guide.rectTransform, 0, 1, 0, 0, 0, 40, 0, 30);
        ctrl.guideText = guide;

        var rosterPanel = CreatePanel("RosterContainer", canvas.transform, Color.clear, false);
        SetRect(rosterPanel.GetComponent<RectTransform>(), 0.05f, 0.65f, 0.15f, 0.88f, 0, 0, 0, 0);
        var grid = rosterPanel.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(195, 240);
        grid.spacing = new Vector2(22, 22);
        grid.childAlignment = TextAnchor.UpperCenter;
        ctrl.rosterContainer = rosterPanel.transform;

        var teamPanel = CreatePanel("TeamSlotsPanel", canvas.transform, new Color(0.14f, 0.16f, 0.22f, 0.96f));
        SetRect(teamPanel.GetComponent<RectTransform>(), 0.68f, 0.95f, 0.22f, 0.88f, 0, 0, 0, 0);

        var teamHeader = CreateText("TeamHeader", teamPanel.transform, "YOUR TEAM (5)", 20, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(teamHeader.rectTransform, 0, 1, 1, 1, 0, -30, 0, 30);

        var slotsContainer = CreatePanel("SlotsContainer", teamPanel.transform, Color.clear, false);
        SetRect(slotsContainer.GetComponent<RectTransform>(), 0.05f, 0.95f, 0.05f, 0.85f, 0, 0, 0, 0);
        var vlg = slotsContainer.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 15;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        ctrl.teamSlotsContainer = slotsContainer.transform;

        var confirmBtn = CreateButton("ConfirmBtn", canvas.transform, "CONFIRM TEAM", 20, new Color(0.24f, 0.68f, 0.38f));
        SetRect(confirmBtn.GetComponent<RectTransform>(), 0.72f, 0.92f, 0, 0, 0, 50, 0, 58);
        ctrl.confirmButton = confirmBtn;

        ctrl.heroCardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/HeroCardPrefab.prefab");
        ctrl.teamSlotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/TeamSlotPrefab.prefab");

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/SquadBuilder.unity");
    }

    // =========================================================================
    // SCENE 3: FORMATION GRID
    // =========================================================================
    private static void BuildFormationScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SetupCameraAndEventSystem();

        var canvas = CreateCanvas();
        var ctrl = canvas.gameObject.AddComponent<FormationController>();

        var bg = CreatePanel("Background", canvas.transform, new Color(0.05f, 0.06f, 0.09f), false);
        SetRect(bg.GetComponent<RectTransform>(), 0, 1, 0, 1, 0, 0, 0, 0);

        var backBtn = CreateButton("BackButton", canvas.transform, "< BACK", 16, new Color(0.32f, 0.38f, 0.50f));
        SetRect(backBtn.GetComponent<RectTransform>(), 0, 0, 1, 1, 80, -45, 130, 50);
        ctrl.backButton = backBtn;
        backBtn.transform.SetAsLastSibling();

        var title = CreateText("Title", canvas.transform, "PLACE YOUR SQUAD (3x3 TACTICAL GRID)", 26, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, 0, 1, 1, 1, 0, -45, 0, 45);

        var roomLabel = CreateText("RoomLabel", canvas.transform, "Room 1/7", 18, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
        SetRect(roomLabel.rectTransform, 1, 1, 1, 1, -120, -45, 180, 45);
        ctrl.roomLabel = roomLabel;

        var benchPanel = CreatePanel("BenchPanel", canvas.transform, new Color(0.14f, 0.16f, 0.22f, 0.96f));
        SetRect(benchPanel.GetComponent<RectTransform>(), 0.03f, 0.22f, 0.08f, 0.88f, 0, 0, 0, 0);

        var benchTitle = CreateText("BenchTitle", benchPanel.transform, "BENCH", 18, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(benchTitle.rectTransform, 0, 1, 1, 1, 0, -25, 0, 30);

        var benchContainer = CreatePanel("BenchCards", benchPanel.transform, Color.clear, false);
        SetRect(benchContainer.GetComponent<RectTransform>(), 0.05f, 0.95f, 0.05f, 0.9f, 0, 0, 0, 0);
        var bvlg = benchContainer.AddComponent<VerticalLayoutGroup>();
        bvlg.spacing = 15;
        bvlg.childAlignment = TextAnchor.UpperCenter;
        ctrl.benchContainer = benchContainer.transform;

        var gridRoot = CreatePanel("3x3Grid", canvas.transform, new Color(0.14f, 0.16f, 0.22f, 0.96f));
        SetRect(gridRoot.GetComponent<RectTransform>(), 0.5f, 0.5f, 0.5f, 0.5f, -10, 10, 520, 520);

        var gridLayout = gridRoot.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(150, 150);
        gridLayout.spacing = new Vector2(16, 16);
        gridLayout.childAlignment = TextAnchor.MiddleCenter;
        ctrl.gridContainer = gridRoot.transform;

        var statsPanel = CreatePanel("StatsPanel", canvas.transform, new Color(0.14f, 0.16f, 0.22f, 0.96f));
        SetRect(statsPanel.GetComponent<RectTransform>(), 0.77f, 0.98f, 0.22f, 0.88f, 0, 0, 0, 0);
        ctrl.statsPanel = statsPanel;

        var statsHeader = CreateText("StatsHeader", statsPanel.transform, "HERO DOSSIER", 20, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(statsHeader.rectTransform, 0, 1, 1, 1, 0, -25, 0, 30);

        ctrl.statsHeroName = CreateText("HeroName", statsPanel.transform, "Name", 18, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        SetRect(ctrl.statsHeroName.rectTransform, 0, 1, 1, 1, 15, -60, -30, 26);

        ctrl.statsClass = CreateText("Class", statsPanel.transform, "Class: Knight", 15, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        SetRect(ctrl.statsClass.rectTransform, 0, 1, 1, 1, 15, -90, -30, 24);

        ctrl.statsHP = CreateText("HP", statsPanel.transform, "HP: 85/85", 15, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        SetRect(ctrl.statsHP.rectTransform, 0, 1, 1, 1, 15, -118, -30, 24);

        ctrl.statsEnergy = CreateText("Energy", statsPanel.transform, "Energy: 110/110", 15, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        SetRect(ctrl.statsEnergy.rectTransform, 0, 1, 1, 1, 15, -146, -30, 24);

        ctrl.statsAttack = CreateText("ATK", statsPanel.transform, "ATK: 18", 15, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        SetRect(ctrl.statsAttack.rectTransform, 0, 1, 1, 1, 15, -174, -30, 24);

        var autoDeployBtn = CreateButton("AutoDeployBtn", statsPanel.transform, "AUTO DEPLOY", 14, new Color(0.24f, 0.68f, 0.38f));
        SetRect(autoDeployBtn.GetComponent<RectTransform>(), 0.08f, 0.92f, 0, 0, 0, 115, 0, 44);
        ctrl.autoDeployButton = autoDeployBtn;

        var unassignBtn = CreateButton("UnassignBtn", statsPanel.transform, "BENCH HERO", 14, new Color(0.58f, 0.40f, 0.20f));
        SetRect(unassignBtn.GetComponent<RectTransform>(), 0.08f, 0.92f, 0, 0, 0, 65, 0, 44);
        ctrl.unassignSelectedButton = unassignBtn;

        var clearBtn = CreateButton("ClearBtn", statsPanel.transform, "CLEAR ALL", 14, new Color(0.76f, 0.24f, 0.26f));
        SetRect(clearBtn.GetComponent<RectTransform>(), 0.08f, 0.92f, 0, 0, 0, 15, 0, 44);
        ctrl.clearGridButton = clearBtn;

        var battleBtn = CreateButton("BattleButton", canvas.transform, "BATTLE!", 22, new Color(0.78f, 0.24f, 0.26f));
        SetRect(battleBtn.GetComponent<RectTransform>(), 0.77f, 0.98f, 0, 0, 0, 75, 0, 65);
        ctrl.battleButton = battleBtn;
        battleBtn.transform.SetAsLastSibling();

        ctrl.gridSlotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/GridSlotPrefab.prefab");
        ctrl.benchCardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/HeroCardPrefab.prefab");

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Formation.unity");
    }

    // =========================================================================
    // SCENE 4: COMBAT HUD
    // =========================================================================
    private static void BuildCombatScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SetupCameraAndEventSystem();

        var arenaGO = new GameObject("Arena");
        arenaGO.transform.position = Vector3.zero;

        CreateArenaObstacle("Obstacle_1", new Vector3(-1f, 1.5f, 0), new Vector3(1f, 1f, 1f));
        CreateArenaObstacle("Obstacle_2", new Vector3(0.5f, -1.2f, 0), new Vector3(1.2f, 0.8f, 1f));

        var canvas = CreateCanvas();
        var ctrl = canvas.gameObject.AddComponent<CombatController>();
        ctrl.arenaContainer = arenaGO.transform;

        var minimap = CreatePanel("Minimap", canvas.transform, new Color(0.14f, 0.16f, 0.22f, 0.94f));
        SetRect(minimap.GetComponent<RectTransform>(), 0, 0, 1, 1, 80, -60, 110, 75);
        var miniText = CreateText("Label", minimap.transform, "MINI\n[#][ ]\n[ ][*]", 14, FontStyles.Normal, TextAlignmentOptions.Center);
        SetRect(miniText.rectTransform, 0, 1, 0, 1, 0, 0, 0, 0);
        ctrl.minimapRoot = minimap;

        var goldText = CreateText("Gold", canvas.transform, "GOLD: 100", 18, FontStyles.Bold, TextAlignmentOptions.Center);
        goldText.color = new Color(1f, 0.85f, 0.2f);
        SetRect(goldText.rectTransform, 0.5f, 0.5f, 1, 1, -420, -22, 180, 44);
        ctrl.goldLabel = goldText;

        var roomText = CreateText("Room", canvas.transform, "Room 1/7", 14, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(roomText.rectTransform, 0.5f, 0.5f, 1, 1, 0, -22, 750, 44);
        ctrl.roomLabel = roomText;

        var pauseBtn = CreateButton("PauseBtn", canvas.transform, "PAUSE", 15, new Color(0.32f, 0.38f, 0.50f));
        SetRect(pauseBtn.GetComponent<RectTransform>(), 1, 1, 1, 1, -55, -32, 76, 44);
        ctrl.pauseButton = pauseBtn;

        var speedBtn = CreateButton("SpeedBtn", canvas.transform, "x2", 18, new Color(0.32f, 0.38f, 0.50f));
        SetRect(speedBtn.GetComponent<RectTransform>(), 1, 1, 1, 1, -138, -32, 62, 44);
        ctrl.speedButton = speedBtn;
        ctrl.speedLabel = speedBtn.GetComponentInChildren<TextMeshProUGUI>();

        var repoBtn = CreateButton("RepoBtn", canvas.transform, "REPO", 15, new Color(0.24f, 0.48f, 0.84f));
        SetRect(repoBtn.GetComponent<RectTransform>(), 1, 1, 0.5f, 0.5f, -40, 0, 72, 65);
        ctrl.repoButton = repoBtn;

        var bossRoot = CreatePanel("BossHP", canvas.transform, new Color(0.20f, 0.08f, 0.10f, 0.96f));
        SetRect(bossRoot.GetComponent<RectTransform>(), 0.3f, 0.7f, 1, 1, 0, -50, 0, 68);
        ctrl.bossHpRoot = bossRoot;

        var bossName = CreateText("BossName", bossRoot.transform, "SKELETON KING", 20, FontStyles.Bold, TextAlignmentOptions.Center);
        bossName.color = new Color(1f, 0.35f, 0.35f);
        SetRect(bossName.rectTransform, 0, 1, 1, 1, 0, -18, 0, 26);
        ctrl.bossNameLabel = bossName;

        var bossSlider = CreateSlider("BossSlider", bossRoot.transform, new Color(0.94f, 0.24f, 0.24f));
        SetRect(bossSlider.GetComponent<RectTransform>(), 0.08f, 0.95f, 0, 0, 0, 18, 0, 18);
        ctrl.bossHpBar = bossSlider;

        var hitCounter = CreateText("HitCounter", bossRoot.transform, "Minion Summon: [ ][ ][ ]", 15, FontStyles.Bold, TextAlignmentOptions.Center);
        hitCounter.color = new Color(0.9f, 0.4f, 0.9f);
        SetRect(hitCounter.rectTransform, 0, 1, 0, 0, 0, -22, 0, 22);
        ctrl.bossHitCounter = hitCounter;
        bossRoot.SetActive(false);

        var bottomPanel = CreatePanel("BottomPanel", canvas.transform, new Color(0.11f, 0.13f, 0.18f, 0.96f));
        SetRect(bottomPanel.GetComponent<RectTransform>(), 0, 1, 0, 0, 0, 88, 0, 176);

        var portraitContainer = CreatePanel("PortraitContainer", bottomPanel.transform, Color.clear, false);
        SetRect(portraitContainer.GetComponent<RectTransform>(), 0.05f, 0.95f, 0, 1, 0, 0, 0, 0);
        var hlg = portraitContainer.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 24;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        ctrl.portraitContainer = portraitContainer.transform;

        var pauseModal = CreatePanel("PausePanel", canvas.transform, new Color(0.14f, 0.16f, 0.22f, 0.98f));
        SetRect(pauseModal.GetComponent<RectTransform>(), 0.35f, 0.65f, 0.3f, 0.7f, 0, 0, 0, 0);
        ctrl.pausePanel = pauseModal;

        var pauseTitle = CreateText("Title", pauseModal.transform, "PAUSED", 32, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(pauseTitle.rectTransform, 0, 1, 1, 1, 0, -50, 0, 45);

        var resumeBtn = CreateButton("ResumeBtn", pauseModal.transform, "RESUME", 20, new Color(0.24f, 0.68f, 0.38f));
        SetRect(resumeBtn.GetComponent<RectTransform>(), 0.2f, 0.8f, 0.4f, 0.4f, 0, 30, 0, 54);
        ctrl.resumeButton = resumeBtn;

        var quitBtn = CreateButton("QuitBtn", pauseModal.transform, "QUIT TO MENU", 18, new Color(0.76f, 0.24f, 0.26f));
        SetRect(quitBtn.GetComponent<RectTransform>(), 0.2f, 0.8f, 0.4f, 0.4f, 0, -40, 0, 54);
        ctrl.quitButton = quitBtn;
        pauseModal.SetActive(false);

        var repoModal = CreatePanel("RepoPanel", canvas.transform, new Color(0.14f, 0.16f, 0.22f, 0.98f));
        SetRect(repoModal.GetComponent<RectTransform>(), 0.3f, 0.7f, 0.2f, 0.8f, 0, 0, 0, 0);
        ctrl.repoPanel = repoModal;

        var repoTitle = CreateText("RepoTitle", repoModal.transform, "REPOSITION SQUAD (1 per room)", 20, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(repoTitle.rectTransform, 0, 1, 1, 1, 0, -35, 0, 35);

        var repoGrid = CreatePanel("RepoGrid", repoModal.transform, Color.clear, false);
        SetRect(repoGrid.GetComponent<RectTransform>(), 0.5f, 0.5f, 0.5f, 0.5f, 0, 20, 280, 280);
        var repoGl = repoGrid.AddComponent<GridLayoutGroup>();
        repoGl.cellSize = new Vector2(80, 80);
        repoGl.spacing = new Vector2(10, 10);
        repoGl.childAlignment = TextAnchor.MiddleCenter;
        ctrl.repoPanelGrid = repoGrid.transform;

        var repoConfirm = CreateButton("ConfirmRepo", repoModal.transform, "CONFIRM", 18, new Color(0.24f, 0.68f, 0.38f));
        SetRect(repoConfirm.GetComponent<RectTransform>(), 0.15f, 0.45f, 0, 0, 0, 45, 0, 50);
        ctrl.repoConfirmBtn = repoConfirm;

        var repoCancel = CreateButton("CancelRepo", repoModal.transform, "CANCEL", 18, new Color(0.32f, 0.38f, 0.50f));
        SetRect(repoCancel.GetComponent<RectTransform>(), 0.55f, 0.85f, 0, 0, 0, 45, 0, 50);
        ctrl.repoCancelBtn = repoCancel;
        repoModal.SetActive(false);

        var healModal = CreatePanel("HealerPriorityPanel", canvas.transform, new Color(0.14f, 0.16f, 0.22f, 0.98f));
        SetRect(healModal.GetComponent<RectTransform>(), 0, 1, 0, 0, 0, 110, 0, 220);
        ctrl.healerPriorityPanel = healModal;

        var healTitle = CreateText("Title", healModal.transform, "EDITING HEALER PRIORITY (Tap an ally to prioritize)", 18, FontStyles.Bold, TextAlignmentOptions.Center);
        healTitle.color = Color.yellow;
        SetRect(healTitle.rectTransform, 0, 1, 1, 1, 0, -25, 0, 25);

        var healTargets = CreatePanel("Targets", healModal.transform, Color.clear, false);
        SetRect(healTargets.GetComponent<RectTransform>(), 0.1f, 0.75f, 0, 1, 0, -25, 0, 0);
        var htl = healTargets.AddComponent<HorizontalLayoutGroup>();
        htl.spacing = 20;
        htl.childAlignment = TextAnchor.MiddleCenter;
        ctrl.healerTargetContainer = healTargets.transform;

        var healConfirm = CreateButton("ConfirmHeal", healModal.transform, "CONFIRM", 18, new Color(0.24f, 0.68f, 0.38f));
        SetRect(healConfirm.GetComponent<RectTransform>(), 0.82f, 0.96f, 0.3f, 0.7f, 0, 0, 0, 0);
        ctrl.healerConfirmBtn = healConfirm;
        healModal.SetActive(false);

        var detailModal = CreatePanel("HeroDetailPanel", canvas.transform, new Color(0.14f, 0.16f, 0.22f, 0.98f));
        SetRect(detailModal.GetComponent<RectTransform>(), 0.1f, 0.9f, 0, 0, 0, 110, 0, 220);
        ctrl.heroDetailPanel = detailModal;

        ctrl.detailHeroName = CreateText("Name", detailModal.transform, "Knight Alden", 22, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        SetRect(ctrl.detailHeroName.rectTransform, 0, 0.35f, 0.7f, 1, 24, 0, 0, 0);

        ctrl.detailHP = CreateText("HP", detailModal.transform, "HP: 85/85", 17, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        SetRect(ctrl.detailHP.rectTransform, 0, 0.35f, 0.4f, 0.7f, 24, 0, 0, 0);

        ctrl.detailEnergy = CreateText("EN", detailModal.transform, "Energy: 110/110", 17, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        SetRect(ctrl.detailEnergy.rectTransform, 0, 0.35f, 0.1f, 0.4f, 24, 0, 0, 0);

        ctrl.detailUpgrades = CreateText("Upgrades", detailModal.transform, "Upgrades:\n- Vampiric Blade", 16, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        SetRect(ctrl.detailUpgrades.rectTransform, 0.35f, 0.8f, 0.1f, 0.9f, 0, 0, 0, 0);

        var detailBack = CreateButton("DetailBack", detailModal.transform, "BACK", 18, new Color(0.32f, 0.38f, 0.50f));
        SetRect(detailBack.GetComponent<RectTransform>(), 0.82f, 0.96f, 0.3f, 0.7f, 0, 0, 0, 0);
        ctrl.detailBackBtn = detailBack;
        detailModal.SetActive(false);

        ctrl.heroCombatantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/HeroCombatant.prefab");
        ctrl.enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/EnemyCombatant.prefab");
        ctrl.portraitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/PortraitPrefab.prefab");
        ctrl.healerTargetCardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/HeroCardPrefab.prefab");

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Combat.unity");
    }

    private static void CreateArenaObstacle(string name, Vector3 pos, Vector3 scale)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.transform.localScale = scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = squareSprite;
        sr.color = new Color(0.35f, 0.35f, 0.4f);
        sr.sortingOrder = 2;
        go.AddComponent<BoxCollider2D>();
    }

    // =========================================================================
    // SCENE 5: REWARDS
    // =========================================================================
    private static void BuildRewardsScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SetupCameraAndEventSystem();

        var canvas = CreateCanvas();
        var ctrl = canvas.gameObject.AddComponent<RewardController>();

        var bg = CreatePanel("Background", canvas.transform, new Color(0.05f, 0.06f, 0.09f), false);
        SetRect(bg.GetComponent<RectTransform>(), 0, 1, 0, 1, 0, 0, 0, 0);

        var backBtn = CreateButton("BackButton", canvas.transform, "< BACK", 16, new Color(0.32f, 0.38f, 0.50f));
        SetRect(backBtn.GetComponent<RectTransform>(), 0, 0, 1, 1, 80, -45, 130, 50);
        ctrl.backButton = backBtn;
        backBtn.transform.SetAsLastSibling();

        var title = CreateText("Title", canvas.transform, "ROOM CLEARED! CHOOSE 2 REWARDS", 26, FontStyles.Bold, TextAlignmentOptions.Center);
        title.color = new Color(1f, 0.85f, 0.2f);
        SetRect(title.rectTransform, 0, 1, 1, 1, 0, -50, 0, 45);

        var cardContainer = CreatePanel("CardContainer", canvas.transform, Color.clear, false);
        SetRect(cardContainer.GetComponent<RectTransform>(), 0.15f, 0.85f, 0.25f, 0.8f, 0, 0, 0, 0);
        var hlg = cardContainer.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 32;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        ctrl.cardContainer = cardContainer.transform;

        var confirmBtn = CreateButton("ConfirmBtn", canvas.transform, "CONFIRM REWARDS", 20, new Color(0.24f, 0.68f, 0.38f));
        SetRect(confirmBtn.GetComponent<RectTransform>(), 0.38f, 0.62f, 0, 0, 0, 70, 0, 56);
        ctrl.confirmButton = confirmBtn;

        var restModal = CreatePanel("RestModal", canvas.transform, new Color(0.14f, 0.16f, 0.22f, 0.98f));
        SetRect(restModal.GetComponent<RectTransform>(), 0.25f, 0.75f, 0.15f, 0.85f, 0, 0, 0, 0);
        ctrl.restModal = restModal;

        var restTitle = CreateText("RestTitle", restModal.transform, "SELECT 2 HEROES TO REST (+50% Energy)", 22, FontStyles.Bold, TextAlignmentOptions.Center);
        restTitle.color = new Color(0.95f, 0.85f, 0.3f);
        SetRect(restTitle.rectTransform, 0, 1, 1, 1, 0, -35, 0, 35);

        var restList = CreatePanel("RestHeroList", restModal.transform, Color.clear, false);
        SetRect(restList.GetComponent<RectTransform>(), 0.05f, 0.95f, 0.18f, 0.88f, 0, 0, 0, 0);
        var rvlg = restList.AddComponent<VerticalLayoutGroup>();
        rvlg.spacing = 10;
        rvlg.childAlignment = TextAnchor.UpperCenter;
        ctrl.restHeroList = restList.transform;

        var restConfirm = CreateButton("ConfirmRest", restModal.transform, "CONFIRM REST", 18, new Color(0.24f, 0.68f, 0.38f));
        SetRect(restConfirm.GetComponent<RectTransform>(), 0.3f, 0.7f, 0, 0, 0, 35, 0, 52);
        ctrl.restConfirmBtn = restConfirm;
        restModal.SetActive(false);

        ctrl.rewardCardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/RewardCardPrefab.prefab");
        ctrl.restHeroRowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/RestHeroRowPrefab.prefab");

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Rewards.unity");
    }

    // =========================================================================
    // SCENE 6: GAME OVER / VICTORY
    // =========================================================================
    private static void BuildGameOverScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SetupCameraAndEventSystem();

        var canvas = CreateCanvas();
        var ctrl = canvas.gameObject.AddComponent<GameOverController>();

        var bg = CreatePanel("Background", canvas.transform, new Color(0.05f, 0.06f, 0.09f), false);
        SetRect(bg.GetComponent<RectTransform>(), 0, 1, 0, 1, 0, 0, 0, 0);

        ctrl.titleLabel = CreateText("Title", canvas.transform, "DUNGEON CLEARED!", 36, FontStyles.Bold, TextAlignmentOptions.Center);
        ctrl.titleLabel.color = new Color(1f, 0.85f, 0.2f);
        SetRect(ctrl.titleLabel.rectTransform, 0, 1, 1, 1, 0, -80, 0, 60);

        var statsBox = CreatePanel("StatsBox", canvas.transform, new Color(0.14f, 0.16f, 0.22f, 0.96f));
        SetRect(statsBox.GetComponent<RectTransform>(), 0.15f, 0.5f, 0.3f, 0.78f, 0, 0, 0, 0);

        var boxHeader = CreateText("Header", statsBox.transform, "RUN STATISTICS", 20, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(boxHeader.rectTransform, 0, 1, 1, 1, 0, -30, 0, 30);

        ctrl.roomsClearedLabel = CreateText("Rooms", statsBox.transform, "Rooms Cleared: 7/7", 18, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        SetRect(ctrl.roomsClearedLabel.rectTransform, 0, 1, 1, 1, 20, -75, -20, 30);

        ctrl.survivorsLabel = CreateText("Survivors", statsBox.transform, "Heroes Survived: 4/5", 18, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        SetRect(ctrl.survivorsLabel.rectTransform, 0, 1, 1, 1, 20, -115, -20, 30);

        ctrl.goldLabel = CreateText("Gold", statsBox.transform, "Gold Earned: 1,250", 18, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        SetRect(ctrl.goldLabel.rectTransform, 0, 1, 1, 1, 20, -155, -20, 30);

        ctrl.upgradesLabel = CreateText("Upgrades", statsBox.transform, "Upgrades Used: 6", 18, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        SetRect(ctrl.upgradesLabel.rectTransform, 0, 1, 1, 1, 20, -195, -20, 30);

        var surBox = CreatePanel("SurvivorsBox", canvas.transform, new Color(0.14f, 0.16f, 0.22f, 0.96f));
        SetRect(surBox.GetComponent<RectTransform>(), 0.55f, 0.85f, 0.3f, 0.78f, 0, 0, 0, 0);

        var surHeader = CreateText("Header", surBox.transform, "HERO STATUS", 20, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(surHeader.rectTransform, 0, 1, 1, 1, 0, -30, 0, 30);

        var surContainer = CreatePanel("List", surBox.transform, Color.clear, false);
        SetRect(surContainer.GetComponent<RectTransform>(), 0.05f, 0.95f, 0.05f, 0.85f, 0, 0, 0, 0);
        var shlg = surContainer.AddComponent<HorizontalLayoutGroup>();
        shlg.spacing = 15;
        shlg.childAlignment = TextAnchor.MiddleCenter;
        ctrl.survivorContainer = surContainer.transform;

        var playAgain = CreateButton("PlayAgainBtn", canvas.transform, "PLAY AGAIN", 20, new Color(0.24f, 0.68f, 0.38f));
        SetRect(playAgain.GetComponent<RectTransform>(), 0.3f, 0.48f, 0, 0, 0, 80, 0, 58);
        ctrl.playAgainButton = playAgain;

        var menuBtn = CreateButton("MainMenuBtn", canvas.transform, "MAIN MENU", 20, new Color(0.32f, 0.38f, 0.50f));
        SetRect(menuBtn.GetComponent<RectTransform>(), 0.52f, 0.7f, 0, 0, 0, 80, 0, 58);
        ctrl.mainMenuButton = menuBtn;

        ctrl.survivorPortraitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/SurvivorPortraitPrefab.prefab");

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/GameOver.unity");
    }

    // =========================================================================
    // BUILD SETTINGS
    // =========================================================================
    public static void ConfigureBuildSettings()
    {
        var scenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/SquadBuilder.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Formation.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Combat.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Rewards.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/GameOver.unity", true)
        };
        EditorBuildSettings.scenes = scenes;
    }

    // =========================================================================
    // HELPER UTILITIES
    // =========================================================================
    private static void SetupCameraAndEventSystem()
    {
        var camGO = new GameObject("Main Camera");
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        camGO.transform.position = new Vector3(0, 0, -10);
        camGO.tag = "MainCamera";

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    private static Canvas CreateCanvas()
    {
        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static GameObject CreateUIRoot(string name, float width, float height)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, height);
        return go;
    }

    private static GameObject CreatePanel(string name, Transform parent, Color color, bool useFrame = true)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = useFrame ? panelFrameSprite : squareSprite;
        if (useFrame && panelFrameSprite != squareSprite) img.type = Image.Type.Sliced;
        img.color = color;
        return go;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string content, float size, FontStyles style, TextAlignmentOptions align, bool raycastTarget = false)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (defaultFont != null) tmp.font = defaultFont;
        tmp.text = content;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = raycastTarget;
        return tmp;
    }

    private static Button CreateButton(string name, Transform parent, string label, float fontSize, Color bgColor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = btnNormalSprite;
        if (btnNormalSprite != squareSprite) img.type = Image.Type.Sliced;
        img.color = bgColor;
        var btn = go.AddComponent<Button>();
        ApplySpriteSwap(btn);

        var text = CreateText("Label", go.transform, label, fontSize, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(text.rectTransform, 0, 1, 0, 1, 0, 0, 0, 0);

        return btn;
    }

    private static void ApplySpriteSwap(Button btn)
    {
        if (btn == null || btnHoverSprite == null || btnPressedSprite == null) return;
        btn.transition = Selectable.Transition.SpriteSwap;
        var state = btn.spriteState;
        state.highlightedSprite = btnHoverSprite;
        state.selectedSprite = btnHoverSprite;
        state.pressedSprite = btnPressedSprite;
        state.disabledSprite = btnPressedSprite;
        btn.spriteState = state;
    }

    private static Slider CreateSlider(string name, Transform parent, Color fillColor)
    {
        var sliderGO = new GameObject(name, typeof(RectTransform));
        sliderGO.transform.SetParent(parent, false);
        var slider = sliderGO.AddComponent<Slider>();

        var bg = new GameObject("Background", typeof(RectTransform));
        bg.transform.SetParent(sliderGO.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.sprite = barBgSprite;
        if (barBgSprite != squareSprite) bgImg.type = Image.Type.Sliced;
        bgImg.color = Color.white;
        SetRect(bg.GetComponent<RectTransform>(), 0, 1, 0, 1, 0, 0, 0, 0);

        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderGO.transform, false);
        SetRect(fillArea.GetComponent<RectTransform>(), 0, 1, 0, 1, 0, 0, 0, 0);

        var fill = new GameObject("Fill", typeof(RectTransform));
        fill.transform.SetParent(fillArea.transform, false);
        var fillImg = fill.AddComponent<Image>();
        fillImg.sprite = barFillSprite;
        if (barFillSprite != squareSprite) fillImg.type = Image.Type.Sliced;
        fillImg.color = fillColor;
        SetRect(fill.GetComponent<RectTransform>(), 0, 0, 0, 1, 0, 0, 0, 0);

        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.targetGraphic = bgImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0;
        slider.maxValue = 100;
        slider.value = 100;

        return slider;
    }

    private static void SetRect(RectTransform rt, float minX, float maxX, float minY, float maxY, float posX, float posY, float sizeX, float sizeY)
    {
        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.anchoredPosition = new Vector2(posX, posY);
        rt.sizeDelta = new Vector2(sizeX, sizeY);
    }

    private static void SavePrefab(GameObject go, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(go, path);
        GameObject.DestroyImmediate(go);
    }
}
#endif
