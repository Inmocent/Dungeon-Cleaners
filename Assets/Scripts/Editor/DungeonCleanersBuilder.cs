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

    private static void InitResources()
    {
        var fontGuids = AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset");
        if (fontGuids.Length > 0)
        {
            defaultFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(fontGuids[0]));
        }

        circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Sprites/Circle.png");
        squareSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Sprites/Square.png");

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
        var root = CreateUIRoot("HeroCard", 180, 220);
        var img = root.AddComponent<Image>();
        img.sprite = squareSprite;
        img.type = Image.Type.Sliced;
        img.color = new Color(0.2f, 0.25f, 0.35f, 0.95f);

        var cardUI = root.AddComponent<HeroCardUI>();
        cardUI.cardBackground = img;

        // Name
        var nameText = CreateText("Name", root.transform, "Hero Name", 16, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(nameText.rectTransform, 0, 1, 0, 1, 0, -25, 0, 30);
        cardUI.nameLabel = nameText;

        // Class
        var classText = CreateText("Class", root.transform, "Class", 13, FontStyles.Normal, TextAlignmentOptions.Center);
        classText.color = new Color(0.85f, 0.85f, 0.85f);
        SetRect(classText.rectTransform, 0, 1, 0, 1, 0, -50, 0, 20);
        cardUI.classLabel = classText;

        // HP Slider
        var hpSlider = CreateSlider("HPSlider", root.transform, new Color(0.85f, 0.2f, 0.2f));
        SetRect(hpSlider.GetComponent<RectTransform>(), 0.1f, 0.9f, 0, 0, 0, 55, 0, 16);
        cardUI.hpBar = hpSlider;

        // Energy Slider
        var enSlider = CreateSlider("EnergySlider", root.transform, new Color(0.2f, 0.5f, 0.9f));
        SetRect(enSlider.GetComponent<RectTransform>(), 0.1f, 0.9f, 0, 0, 0, 25, 0, 16);
        cardUI.energyBar = enSlider;

        SavePrefab(root, "Assets/Resources/Prefabs/HeroCardPrefab.prefab");
    }

    private static void BuildTeamSlotPrefab()
    {
        var root = CreateUIRoot("TeamSlot", 280, 65);
        var img = root.AddComponent<Image>();
        img.sprite = squareSprite;
        img.color = new Color(0.2f, 0.2f, 0.25f, 0.95f);

        var slotUI = root.AddComponent<TeamSlotUI>();
        slotUI.slotBackground = img;

        // Label
        var heroText = CreateText("HeroLabel", root.transform, "Empty Slot", 16, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        SetRect(heroText.rectTransform, 0, 0.7f, 0.45f, 1, 15, 0, 0, 0);
        slotUI.heroLabel = heroText;

        // HP Label
        var hpText = CreateText("HPLabel", root.transform, "", 13, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        hpText.color = new Color(0.8f, 0.8f, 0.8f);
        SetRect(hpText.rectTransform, 0, 0.7f, 0, 0.45f, 15, 0, 0, 0);
        slotUI.hpLabel = hpText;

        // Button (+ / -)
        var btnObj = CreateUIRoot("ActionButton", 45, 45);
        btnObj.transform.SetParent(root.transform, false);
        SetRect(btnObj.GetComponent<RectTransform>(), 1, 1, 0.5f, 0.5f, -32, 0, 45, 45);
        var btnImg = btnObj.AddComponent<Image>();
        btnImg.sprite = circleSprite;
        btnImg.color = new Color(0.3f, 0.35f, 0.45f);
        var btn = btnObj.AddComponent<Button>();
        slotUI.actionButton = btn;

        var btnText = CreateText("BtnLabel", btnObj.transform, "+", 22, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(btnText.rectTransform, 0, 1, 0, 1, 0, 0, 0, 0);
        slotUI.actionLabel = btnText;

        SavePrefab(root, "Assets/Resources/Prefabs/TeamSlotPrefab.prefab");
    }

    private static void BuildGridSlotPrefab()
    {
        var root = CreateUIRoot("GridSlot", 110, 110);
        var img = root.AddComponent<Image>();
        img.sprite = squareSprite;
        img.color = new Color(0.2f, 0.22f, 0.28f, 0.9f);

        root.AddComponent<CanvasGroup>();
        var drag = root.AddComponent<GridSlotDrag>();
        var slotUI = root.AddComponent<GridSlotUI>();
        slotUI.background = img;

        var label = CreateText("HeroLabel", root.transform, "[Slot]", 14, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(label.rectTransform, 0, 1, 0.4f, 1, 0, 0, 0, 0);
        slotUI.heroLabel = label;

        var hpLabel = CreateText("HPLabel", root.transform, "", 12, FontStyles.Normal, TextAlignmentOptions.Center);
        SetRect(hpLabel.rectTransform, 0, 1, 0, 0.4f, 0, 0, 0, 0);
        slotUI.hpLabel = hpLabel;

        var warnObj = CreateUIRoot("WarningIcon", 24, 24);
        warnObj.transform.SetParent(root.transform, false);
        SetRect(warnObj.GetComponent<RectTransform>(), 1, 1, 1, 1, -15, -15, 24, 24);
        var warnImg = warnObj.AddComponent<Image>();
        warnImg.sprite = circleSprite;
        warnImg.color = Color.red;
        var warnText = CreateText("!", warnObj.transform, "!", 16, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(warnText.rectTransform, 0, 1, 0, 1, 0, 0, 0, 0);
        slotUI.warningIcon = warnImg;
        warnObj.SetActive(false);

        SavePrefab(root, "Assets/Resources/Prefabs/GridSlotPrefab.prefab");
    }

    private static void BuildPortraitPrefab()
    {
        var root = CreateUIRoot("Portrait", 110, 150);
        var portraitUI = root.AddComponent<PortraitUI>();

        // Circle Portrait
        var circleObj = CreateUIRoot("Circle", 75, 75);
        circleObj.transform.SetParent(root.transform, false);
        SetRect(circleObj.GetComponent<RectTransform>(), 0.5f, 0.5f, 1f, 1f, 0, -42, 75, 75);
        var circleImg = circleObj.AddComponent<Image>();
        circleImg.sprite = circleSprite;
        circleImg.color = Color.gray;
        portraitUI.portraitCircle = circleImg;

        // Name
        var nameText = CreateText("Name", circleObj.transform, "Hero", 12, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(nameText.rectTransform, 0, 1, 0, 1, 0, 0, 0, 0);
        portraitUI.nameLabel = nameText;

        // HP Bar
        var hpSlider = CreateSlider("HPBar", root.transform, new Color(0.2f, 0.8f, 0.25f));
        SetRect(hpSlider.GetComponent<RectTransform>(), 0.1f, 0.9f, 0, 0, 0, 40, 0, 14);
        portraitUI.hpBar = hpSlider;
        portraitUI.hpFill = hpSlider.fillRect.GetComponent<Image>();

        // Energy Bar
        var enSlider = CreateSlider("ENBar", root.transform, new Color(0.25f, 0.5f, 0.95f));
        SetRect(enSlider.GetComponent<RectTransform>(), 0.1f, 0.9f, 0, 0, 0, 20, 0, 14);
        portraitUI.energyBar = enSlider;
        portraitUI.energyFill = enSlider.fillRect.GetComponent<Image>();

        // Fatigued Label
        var fatText = CreateText("FatiguedLabel", root.transform, "FATIGUED", 11, FontStyles.Bold, TextAlignmentOptions.Center);
        fatText.color = Color.yellow;
        SetRect(fatText.rectTransform, 0, 1, 0, 0, 0, 5, 0, 16);
        portraitUI.fatiguedLabel = fatText;
        fatText.gameObject.SetActive(false);

        // Dead Overlay
        var deadObj = CreateUIRoot("DeadOverlay", 75, 75);
        deadObj.transform.SetParent(circleObj.transform, false);
        SetRect(deadObj.GetComponent<RectTransform>(), 0, 1, 0, 1, 0, 0, 0, 0);
        var deadImg = deadObj.AddComponent<Image>();
        deadImg.sprite = circleSprite;
        deadImg.color = new Color(0.1f, 0.1f, 0.1f, 0.75f);
        var skullText = CreateText("Skull", deadObj.transform, "X", 30, FontStyles.Bold, TextAlignmentOptions.Center);
        skullText.color = Color.red;
        SetRect(skullText.rectTransform, 0, 1, 0, 1, 0, 0, 0, 0);
        portraitUI.deadOverlay = deadImg;
        deadObj.SetActive(false);

        SavePrefab(root, "Assets/Resources/Prefabs/PortraitPrefab.prefab");
    }

    private static void BuildRewardCardPrefab()
    {
        var root = CreateUIRoot("RewardCard", 220, 320);
        var img = root.AddComponent<Image>();
        img.sprite = squareSprite;
        img.color = new Color(0.25f, 0.28f, 0.38f);
        var btn = root.AddComponent<Button>();
        var cardUI = root.AddComponent<RewardCardUI>();
        cardUI.cardBackground = img;

        // Border selection
        var borderObj = CreateUIRoot("SelectionBorder", 228, 328);
        borderObj.transform.SetParent(root.transform, false);
        SetRect(borderObj.GetComponent<RectTransform>(), 0, 1, 0, 1, 0, 0, 8, 8);
        var borderImg = borderObj.AddComponent<Image>();
        borderImg.sprite = squareSprite;
        borderImg.color = new Color(1f, 0.85f, 0.2f, 0.8f);
        cardUI.selectionBorder = borderImg;
        borderObj.transform.SetAsFirstSibling();
        borderObj.SetActive(false);

        // Type
        var typeText = CreateText("TypeLabel", root.transform, "UPGRADE", 16, FontStyles.Bold, TextAlignmentOptions.Center);
        typeText.color = new Color(1f, 0.85f, 0.2f);
        SetRect(typeText.rectTransform, 0, 1, 1, 1, 0, -30, 0, 30);
        cardUI.typeLabel = typeText;

        // Name
        var nameText = CreateText("NameLabel", root.transform, "Camp Coffee", 18, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(nameText.rectTransform, 0, 1, 0.6f, 0.85f, 0, 0, 0, 0);
        cardUI.rewardNameLabel = nameText;

        // Description
        var descText = CreateText("DescLabel", root.transform, "Restores 50% energy to 2 chosen heroes.", 14, FontStyles.Normal, TextAlignmentOptions.Center);
        descText.color = new Color(0.85f, 0.85f, 0.85f);
        SetRect(descText.rectTransform, 0.08f, 0.92f, 0.15f, 0.6f, 0, 0, 0, 0);
        cardUI.descriptionLabel = descText;

        SavePrefab(root, "Assets/Resources/Prefabs/RewardCardPrefab.prefab");
    }

    private static void BuildRestHeroRowPrefab()
    {
        var root = CreateUIRoot("RestHeroRow", 460, 50);
        var img = root.AddComponent<Image>();
        img.sprite = squareSprite;
        img.color = new Color(0.2f, 0.22f, 0.28f);

        var text = CreateText("HeroInfo", root.transform, "Knight Alden - HP: 85/85 EN: 20/110", 15, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        SetRect(text.rectTransform, 0, 0.8f, 0, 1, 15, 0, 0, 0);

        var toggleObj = CreateUIRoot("Toggle", 36, 36);
        toggleObj.transform.SetParent(root.transform, false);
        SetRect(toggleObj.GetComponent<RectTransform>(), 1, 1, 0.5f, 0.5f, -30, 0, 36, 36);
        var toggle = toggleObj.AddComponent<Toggle>();
        var toggleBg = toggleObj.AddComponent<Image>();
        toggleBg.sprite = squareSprite;
        toggleBg.color = new Color(0.35f, 0.38f, 0.45f);
        toggle.targetGraphic = toggleBg;

        var checkObj = CreateUIRoot("Checkmark", 24, 24);
        checkObj.transform.SetParent(toggleObj.transform, false);
        SetRect(checkObj.GetComponent<RectTransform>(), 0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 24, 24);
        var checkImg = checkObj.AddComponent<Image>();
        checkImg.sprite = squareSprite;
        checkImg.color = Color.green;
        toggle.graphic = checkImg;

        SavePrefab(root, "Assets/Resources/Prefabs/RestHeroRowPrefab.prefab");
    }

    private static void BuildSurvivorPortraitPrefab()
    {
        var root = CreateUIRoot("SurvivorPortrait", 90, 110);
        var circleObj = CreateUIRoot("Circle", 65, 65);
        circleObj.transform.SetParent(root.transform, false);
        SetRect(circleObj.GetComponent<RectTransform>(), 0.5f, 0.5f, 1, 1, 0, -35, 65, 65);
        var img = circleObj.AddComponent<Image>();
        img.sprite = circleSprite;
        img.color = Color.gray;

        var text = CreateText("Label", root.transform, "Hero\nAlive", 12, FontStyles.Normal, TextAlignmentOptions.Center);
        SetRect(text.rectTransform, 0, 1, 0, 0.4f, 0, 0, 0, 0);

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

        // Background
        var bg = CreatePanel("Background", canvas.transform, new Color(0.08f, 0.08f, 0.12f));
        SetRect(bg.GetComponent<RectTransform>(), 0, 1, 0, 1, 0, 0, 0, 0);

        // Left Panel: Planning Table concept
        var leftPanel = CreatePanel("PlanningScenePanel", canvas.transform, new Color(0.12f, 0.12f, 0.18f, 0.9f));
        SetRect(leftPanel.GetComponent<RectTransform>(), 0.05f, 0.55f, 0.1f, 0.9f, 0, 0, 0, 0);

        var mapTitle = CreateText("MapTitle", leftPanel.transform, "[ RAID PLANNING TABLE ]", 24, FontStyles.Bold, TextAlignmentOptions.Center);
        mapTitle.color = new Color(0.95f, 0.85f, 0.4f);
        SetRect(mapTitle.rectTransform, 0, 1, 1, 1, 0, -40, 0, 40);

        var mapDesc = CreateText("MapDesc", leftPanel.transform,
            "[ 2D Hand-Crafted Art Placeholder ]\n\n5 Heroes gathered around a weathered dungeon map,\nplanning their raid by lantern light.\n\nKnights tracing corridors,\nMages reading ancient runes,\nHealer preparing potions.",
            18, FontStyles.Italic, TextAlignmentOptions.Center);
        mapDesc.color = new Color(0.75f, 0.75f, 0.85f);
        SetRect(mapDesc.rectTransform, 0.1f, 0.9f, 0.1f, 0.85f, 0, 0, 0, 0);

        // Right Panel: Menu Buttons
        var rightPanel = CreatePanel("MenuPanel", canvas.transform, Color.clear);
        SetRect(rightPanel.GetComponent<RectTransform>(), 0.6f, 0.95f, 0.1f, 0.9f, 0, 0, 0, 0);

        var title = CreateText("GameTitle", rightPanel.transform, "DUNGEON\nCLEANERS", 44, FontStyles.Bold, TextAlignmentOptions.Center);
        title.color = new Color(1f, 0.85f, 0.2f);
        SetRect(title.rectTransform, 0, 1, 1, 1, 0, -60, 0, 100);

        var skull = CreateText("Skull", rightPanel.transform, "*  *  *", 24, FontStyles.Bold, TextAlignmentOptions.Center);
        skull.color = new Color(0.85f, 0.3f, 0.3f);
        SetRect(skull.rectTransform, 0, 1, 1, 1, 0, -130, 0, 30);

        var startBtn = CreateButton("StartRunBtn", rightPanel.transform, "START RUN", 22, new Color(0.18f, 0.55f, 0.25f));
        SetRect(startBtn.GetComponent<RectTransform>(), 0.1f, 0.9f, 0, 0, 0, 240, 0, 65);
        ctrl.startRunButton = startBtn;

        var squadBtn = CreateButton("SquadBtn", rightPanel.transform, "SQUAD", 20, new Color(0.2f, 0.4f, 0.65f));
        SetRect(squadBtn.GetComponent<RectTransform>(), 0.1f, 0.9f, 0, 0, 0, 160, 0, 55);
        ctrl.squadButton = squadBtn;

        var settingsBtn = CreateButton("SettingsBtn", rightPanel.transform, "SETTINGS", 18, new Color(0.3f, 0.32f, 0.4f));
        SetRect(settingsBtn.GetComponent<RectTransform>(), 0.1f, 0.9f, 0, 0, 0, 85, 0, 50);
        ctrl.settingsButton = settingsBtn;

        // Settings Modal
        var settingsModal = CreatePanel("SettingsPanel", canvas.transform, new Color(0.1f, 0.1f, 0.15f, 0.95f));
        SetRect(settingsModal.GetComponent<RectTransform>(), 0.25f, 0.75f, 0.2f, 0.8f, 0, 0, 0, 0);
        ctrl.settingsPanel = settingsModal;

        var setHeader = CreateText("SettingsHeader", settingsModal.transform, "SETTINGS", 28, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(setHeader.rectTransform, 0, 1, 1, 1, 0, -40, 0, 40);

        var closeSetBtn = CreateButton("CloseSettings", settingsModal.transform, "CLOSE", 18, new Color(0.6f, 0.2f, 0.2f));
        SetRect(closeSetBtn.GetComponent<RectTransform>(), 0.35f, 0.65f, 0, 0, 0, 40, 0, 45);
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

        // Background
        var bg = CreatePanel("Background", canvas.transform, new Color(0.08f, 0.08f, 0.12f));
        SetRect(bg.GetComponent<RectTransform>(), 0, 1, 0, 1, 0, 0, 0, 0);

        // Top Bar
        var backBtn = CreateButton("BackButton", canvas.transform, "< BACK", 16, new Color(0.3f, 0.32f, 0.4f));
        SetRect(backBtn.GetComponent<RectTransform>(), 0, 0, 1, 1, 80, -45, 120, 45);
        ctrl.backButton = backBtn;

        var title = CreateText("Title", canvas.transform, "BUILD YOUR SQUAD", 28, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, 0, 1, 1, 1, 0, -45, 0, 45);

        // Guide Text
        var guide = CreateText("GuideText", canvas.transform, "Pick your squad of 5 heroes (0/5)", 18, FontStyles.Italic, TextAlignmentOptions.Center);
        guide.color = new Color(0.9f, 0.85f, 0.3f);
        SetRect(guide.rectTransform, 0, 1, 0, 0, 0, 40, 0, 30);
        ctrl.guideText = guide;

        // Left: Roster Container
        var rosterPanel = CreatePanel("RosterContainer", canvas.transform, Color.clear);
        SetRect(rosterPanel.GetComponent<RectTransform>(), 0.05f, 0.65f, 0.15f, 0.88f, 0, 0, 0, 0);
        var grid = rosterPanel.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(180, 220);
        grid.spacing = new Vector2(25, 25);
        grid.childAlignment = TextAnchor.UpperCenter;
        ctrl.rosterContainer = rosterPanel.transform;

        // Right: Team Slots
        var teamPanel = CreatePanel("TeamSlotsPanel", canvas.transform, new Color(0.12f, 0.14f, 0.2f, 0.9f));
        SetRect(teamPanel.GetComponent<RectTransform>(), 0.68f, 0.95f, 0.22f, 0.88f, 0, 0, 0, 0);

        var teamHeader = CreateText("TeamHeader", teamPanel.transform, "YOUR TEAM (5)", 20, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(teamHeader.rectTransform, 0, 1, 1, 1, 0, -30, 0, 30);

        var slotsContainer = CreatePanel("SlotsContainer", teamPanel.transform, Color.clear);
        SetRect(slotsContainer.GetComponent<RectTransform>(), 0.05f, 0.95f, 0.05f, 0.85f, 0, 0, 0, 0);
        var vlg = slotsContainer.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 15;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        ctrl.teamSlotsContainer = slotsContainer.transform;

        // Confirm Button
        var confirmBtn = CreateButton("ConfirmBtn", canvas.transform, "CONFIRM TEAM", 20, new Color(0.18f, 0.55f, 0.25f));
        SetRect(confirmBtn.GetComponent<RectTransform>(), 0.72f, 0.92f, 0, 0, 0, 50, 0, 55);
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

        // Background
        var bg = CreatePanel("Background", canvas.transform, new Color(0.08f, 0.08f, 0.12f));
        SetRect(bg.GetComponent<RectTransform>(), 0, 1, 0, 1, 0, 0, 0, 0);

        // Top Bar
        var backBtn = CreateButton("BackButton", canvas.transform, "< BACK", 16, new Color(0.3f, 0.32f, 0.4f));
        SetRect(backBtn.GetComponent<RectTransform>(), 0, 0, 1, 1, 80, -45, 120, 45);
        ctrl.backButton = backBtn;

        var title = CreateText("Title", canvas.transform, "PLACE YOUR SQUAD (3x3 TACTICAL GRID)", 26, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, 0, 1, 1, 1, 0, -45, 0, 45);

        var roomLabel = CreateText("RoomLabel", canvas.transform, "Room 1/7", 18, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
        SetRect(roomLabel.rectTransform, 1, 1, 1, 1, -120, -45, 180, 45);
        ctrl.roomLabel = roomLabel;

        // Left Sidebar: Bench
        var benchPanel = CreatePanel("BenchPanel", canvas.transform, new Color(0.12f, 0.14f, 0.2f, 0.9f));
        SetRect(benchPanel.GetComponent<RectTransform>(), 0.03f, 0.22f, 0.08f, 0.88f, 0, 0, 0, 0);

        var benchTitle = CreateText("BenchTitle", benchPanel.transform, "BENCH", 18, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(benchTitle.rectTransform, 0, 1, 1, 1, 0, -25, 0, 30);

        var benchContainer = CreatePanel("BenchCards", benchPanel.transform, Color.clear);
        SetRect(benchContainer.GetComponent<RectTransform>(), 0.05f, 0.95f, 0.05f, 0.9f, 0, 0, 0, 0);
        var bvlg = benchContainer.AddComponent<VerticalLayoutGroup>();
        bvlg.spacing = 15;
        bvlg.childAlignment = TextAnchor.UpperCenter;
        ctrl.benchContainer = benchContainer.transform;

        // Center: 3x3 Grid
        var gridRoot = CreatePanel("3x3Grid", canvas.transform, new Color(0.1f, 0.12f, 0.16f, 0.95f));
        SetRect(gridRoot.GetComponent<RectTransform>(), 0.5f, 0.5f, 0.5f, 0.5f, 0, 0, 400, 400);

        var gridLayout = gridRoot.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(110, 110);
        gridLayout.spacing = new Vector2(15, 15);
        gridLayout.childAlignment = TextAnchor.MiddleCenter;
        ctrl.gridContainer = gridRoot.transform;

        // Right Sidebar: Hero Stats Panel
        var statsPanel = CreatePanel("StatsPanel", canvas.transform, new Color(0.12f, 0.14f, 0.2f, 0.9f));
        SetRect(statsPanel.GetComponent<RectTransform>(), 0.78f, 0.97f, 0.35f, 0.88f, 0, 0, 0, 0);
        ctrl.statsPanel = statsPanel;

        var statsHeader = CreateText("StatsHeader", statsPanel.transform, "HERO STATS", 20, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(statsHeader.rectTransform, 0, 1, 1, 1, 0, -30, 0, 30);

        ctrl.statsHeroName = CreateText("HeroName", statsPanel.transform, "Name", 18, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        SetRect(ctrl.statsHeroName.rectTransform, 0, 1, 1, 1, 15, -70, -30, 30);

        ctrl.statsClass = CreateText("Class", statsPanel.transform, "Class: Knight", 15, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        SetRect(ctrl.statsClass.rectTransform, 0, 1, 1, 1, 15, -100, -30, 25);

        ctrl.statsHP = CreateText("HP", statsPanel.transform, "HP: 85/85", 15, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        SetRect(ctrl.statsHP.rectTransform, 0, 1, 1, 1, 15, -130, -30, 25);

        ctrl.statsEnergy = CreateText("Energy", statsPanel.transform, "Energy: 110/110", 15, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        SetRect(ctrl.statsEnergy.rectTransform, 0, 1, 1, 1, 15, -160, -30, 25);

        ctrl.statsAttack = CreateText("ATK", statsPanel.transform, "ATK: 18", 15, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        SetRect(ctrl.statsAttack.rectTransform, 0, 1, 1, 1, 15, -190, -30, 25);

        // Battle Button
        var battleBtn = CreateButton("BattleButton", canvas.transform, "BATTLE!", 24, new Color(0.85f, 0.2f, 0.2f));
        SetRect(battleBtn.GetComponent<RectTransform>(), 0.78f, 0.97f, 0, 0, 0, 80, 0, 65);
        ctrl.battleButton = battleBtn;

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

        // Arena Container in World Space
        var arenaGO = new GameObject("Arena");
        arenaGO.transform.position = Vector3.zero;

        // Obstacles (Stones / Crates)
        CreateArenaObstacle("Obstacle_1", new Vector3(-1f, 1.5f, 0), new Vector3(1f, 1f, 1f));
        CreateArenaObstacle("Obstacle_2", new Vector3(0.5f, -1.2f, 0), new Vector3(1.2f, 0.8f, 1f));

        var canvas = CreateCanvas();
        var ctrl = canvas.gameObject.AddComponent<CombatController>();
        ctrl.arenaContainer = arenaGO.transform;

        // Top Bar
        // Minimap
        var minimap = CreatePanel("Minimap", canvas.transform, new Color(0.15f, 0.15f, 0.22f, 0.9f));
        SetRect(minimap.GetComponent<RectTransform>(), 0, 0, 1, 1, 80, -60, 110, 75);
        var miniText = CreateText("Label", minimap.transform, "MINI\n[#][ ]\n[ ][*]", 12, FontStyles.Normal, TextAlignmentOptions.Center);
        SetRect(miniText.rectTransform, 0, 1, 0, 1, 0, 0, 0, 0);
        ctrl.minimapRoot = minimap;

        // Gold & Room
        var goldText = CreateText("Gold", canvas.transform, "GOLD: 100", 20, FontStyles.Bold, TextAlignmentOptions.Center);
        goldText.color = new Color(1f, 0.85f, 0.2f);
        SetRect(goldText.rectTransform, 0.5f, 0.5f, 1, 1, -150, -40, 160, 40);
        ctrl.goldLabel = goldText;

        var roomText = CreateText("Room", canvas.transform, "Room 1/7", 20, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(roomText.rectTransform, 0.5f, 0.5f, 1, 1, 150, -40, 200, 40);
        ctrl.roomLabel = roomText;

        // Pause & Speed
        var pauseBtn = CreateButton("PauseBtn", canvas.transform, "PAUSE", 14, new Color(0.3f, 0.32f, 0.4f));
        SetRect(pauseBtn.GetComponent<RectTransform>(), 1, 1, 1, 1, -55, -45, 70, 45);
        ctrl.pauseButton = pauseBtn;

        var speedBtn = CreateButton("SpeedBtn", canvas.transform, "x2", 18, new Color(0.3f, 0.32f, 0.4f));
        SetRect(speedBtn.GetComponent<RectTransform>(), 1, 1, 1, 1, -135, -45, 55, 45);
        ctrl.speedButton = speedBtn;
        ctrl.speedLabel = speedBtn.GetComponentInChildren<TextMeshProUGUI>();

        // Right Edge: Reposition Button
        var repoBtn = CreateButton("RepoBtn", canvas.transform, "REPO", 14, new Color(0.25f, 0.45f, 0.75f));
        SetRect(repoBtn.GetComponent<RectTransform>(), 1, 1, 0.5f, 0.5f, -40, 0, 60, 65);
        ctrl.repoButton = repoBtn;

        // Boss Bar (hidden by default)
        var bossRoot = CreatePanel("BossHP", canvas.transform, new Color(0.12f, 0.08f, 0.08f, 0.95f));
        SetRect(bossRoot.GetComponent<RectTransform>(), 0.3f, 0.7f, 1, 1, 0, -50, 0, 60);
        ctrl.bossHpRoot = bossRoot;

        var bossName = CreateText("BossName", bossRoot.transform, "SKELETON KING", 18, FontStyles.Bold, TextAlignmentOptions.Center);
        bossName.color = Color.red;
        SetRect(bossName.rectTransform, 0, 1, 1, 1, 0, -18, 0, 24);
        ctrl.bossNameLabel = bossName;

        var bossSlider = CreateSlider("BossSlider", bossRoot.transform, new Color(0.85f, 0.15f, 0.15f));
        SetRect(bossSlider.GetComponent<RectTransform>(), 0.05f, 0.95f, 0, 0, 0, 18, 0, 16);
        ctrl.bossHpBar = bossSlider;

        var hitCounter = CreateText("HitCounter", bossRoot.transform, "Minion Summon: [ ][ ][ ]", 14, FontStyles.Bold, TextAlignmentOptions.Center);
        hitCounter.color = new Color(0.9f, 0.4f, 0.9f);
        SetRect(hitCounter.rectTransform, 0, 1, 0, 0, 0, -20, 0, 20);
        ctrl.bossHitCounter = hitCounter;
        bossRoot.SetActive(false);

        // Bottom Portrait Panel
        var bottomPanel = CreatePanel("BottomPanel", canvas.transform, new Color(0.1f, 0.11f, 0.16f, 0.95f));
        SetRect(bottomPanel.GetComponent<RectTransform>(), 0, 1, 0, 0, 0, 85, 0, 170);

        var portraitContainer = CreatePanel("PortraitContainer", bottomPanel.transform, Color.clear);
        SetRect(portraitContainer.GetComponent<RectTransform>(), 0.1f, 0.9f, 0, 1, 0, 0, 0, 0);
        var hlg = portraitContainer.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 30;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        ctrl.portraitContainer = portraitContainer.transform;

        // Pause Panel Overlay
        var pauseModal = CreatePanel("PausePanel", canvas.transform, new Color(0.08f, 0.08f, 0.12f, 0.95f));
        SetRect(pauseModal.GetComponent<RectTransform>(), 0.35f, 0.65f, 0.3f, 0.7f, 0, 0, 0, 0);
        ctrl.pausePanel = pauseModal;

        var pauseTitle = CreateText("Title", pauseModal.transform, "PAUSED", 32, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(pauseTitle.rectTransform, 0, 1, 1, 1, 0, -50, 0, 45);

        var resumeBtn = CreateButton("ResumeBtn", pauseModal.transform, "RESUME", 20, new Color(0.18f, 0.55f, 0.25f));
        SetRect(resumeBtn.GetComponent<RectTransform>(), 0.2f, 0.8f, 0.4f, 0.4f, 0, 30, 0, 50);
        ctrl.resumeButton = resumeBtn;

        var quitBtn = CreateButton("QuitBtn", pauseModal.transform, "QUIT TO MENU", 18, new Color(0.6f, 0.2f, 0.2f));
        SetRect(quitBtn.GetComponent<RectTransform>(), 0.2f, 0.8f, 0.4f, 0.4f, 0, -40, 0, 50);
        ctrl.quitButton = quitBtn;
        pauseModal.SetActive(false);

        // Reposition Modal Overlay
        var repoModal = CreatePanel("RepoPanel", canvas.transform, new Color(0.08f, 0.08f, 0.12f, 0.95f));
        SetRect(repoModal.GetComponent<RectTransform>(), 0.3f, 0.7f, 0.2f, 0.8f, 0, 0, 0, 0);
        ctrl.repoPanel = repoModal;

        var repoTitle = CreateText("RepoTitle", repoModal.transform, "REPOSITION SQUAD (1 per room)", 20, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(repoTitle.rectTransform, 0, 1, 1, 1, 0, -35, 0, 35);

        var repoGrid = CreatePanel("RepoGrid", repoModal.transform, Color.clear);
        SetRect(repoGrid.GetComponent<RectTransform>(), 0.5f, 0.5f, 0.5f, 0.5f, 0, 20, 280, 280);
        var repoGl = repoGrid.AddComponent<GridLayoutGroup>();
        repoGl.cellSize = new Vector2(80, 80);
        repoGl.spacing = new Vector2(10, 10);
        repoGl.childAlignment = TextAnchor.MiddleCenter;
        ctrl.repoPanelGrid = repoGrid.transform;

        var repoConfirm = CreateButton("ConfirmRepo", repoModal.transform, "CONFIRM", 18, new Color(0.18f, 0.55f, 0.25f));
        SetRect(repoConfirm.GetComponent<RectTransform>(), 0.15f, 0.45f, 0, 0, 0, 45, 0, 45);
        ctrl.repoConfirmBtn = repoConfirm;

        var repoCancel = CreateButton("CancelRepo", repoModal.transform, "CANCEL", 18, new Color(0.4f, 0.42f, 0.5f));
        SetRect(repoCancel.GetComponent<RectTransform>(), 0.55f, 0.85f, 0, 0, 0, 45, 0, 45);
        ctrl.repoCancelBtn = repoCancel;
        repoModal.SetActive(false);

        // Healer Priority Panel
        var healModal = CreatePanel("HealerPriorityPanel", canvas.transform, new Color(0.1f, 0.14f, 0.12f, 0.98f));
        SetRect(healModal.GetComponent<RectTransform>(), 0, 1, 0, 0, 0, 110, 0, 220);
        ctrl.healerPriorityPanel = healModal;

        var healTitle = CreateText("Title", healModal.transform, "EDITING HEALER PRIORITY (Tap an ally to prioritize)", 18, FontStyles.Bold, TextAlignmentOptions.Center);
        healTitle.color = Color.yellow;
        SetRect(healTitle.rectTransform, 0, 1, 1, 1, 0, -25, 0, 25);

        var healTargets = CreatePanel("Targets", healModal.transform, Color.clear);
        SetRect(healTargets.GetComponent<RectTransform>(), 0.1f, 0.75f, 0, 1, 0, -25, 0, 0);
        var htl = healTargets.AddComponent<HorizontalLayoutGroup>();
        htl.spacing = 20;
        htl.childAlignment = TextAnchor.MiddleCenter;
        ctrl.healerTargetContainer = healTargets.transform;

        var healConfirm = CreateButton("ConfirmHeal", healModal.transform, "CONFIRM", 18, new Color(0.18f, 0.55f, 0.25f));
        SetRect(healConfirm.GetComponent<RectTransform>(), 0.82f, 0.96f, 0.3f, 0.7f, 0, 0, 0, 0);
        ctrl.healerConfirmBtn = healConfirm;
        healModal.SetActive(false);

        // Hero Detail Panel
        var detailModal = CreatePanel("HeroDetailPanel", canvas.transform, new Color(0.1f, 0.12f, 0.18f, 0.98f));
        SetRect(detailModal.GetComponent<RectTransform>(), 0.1f, 0.9f, 0, 0, 0, 110, 0, 220);
        ctrl.heroDetailPanel = detailModal;

        ctrl.detailHeroName = CreateText("Name", detailModal.transform, "Knight Alden", 20, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        SetRect(ctrl.detailHeroName.rectTransform, 0, 0.35f, 0.7f, 1, 20, 0, 0, 0);

        ctrl.detailHP = CreateText("HP", detailModal.transform, "HP: 85/85", 15, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        SetRect(ctrl.detailHP.rectTransform, 0, 0.35f, 0.4f, 0.7f, 20, 0, 0, 0);

        ctrl.detailEnergy = CreateText("EN", detailModal.transform, "Energy: 110/110", 15, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        SetRect(ctrl.detailEnergy.rectTransform, 0, 0.35f, 0.1f, 0.4f, 20, 0, 0, 0);

        ctrl.detailUpgrades = CreateText("Upgrades", detailModal.transform, "Upgrades:\n- Vampiric Blade (+20% Lifesteal)", 15, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        SetRect(ctrl.detailUpgrades.rectTransform, 0.35f, 0.8f, 0.1f, 0.9f, 0, 0, 0, 0);

        var detailBack = CreateButton("DetailBack", detailModal.transform, "BACK", 16, new Color(0.4f, 0.42f, 0.5f));
        SetRect(detailBack.GetComponent<RectTransform>(), 0.82f, 0.96f, 0.3f, 0.7f, 0, 0, 0, 0);
        ctrl.detailBackBtn = detailBack;
        detailModal.SetActive(false);

        // Prefab references
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
        var col = go.AddComponent<BoxCollider2D>();
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

        // Background
        var bg = CreatePanel("Background", canvas.transform, new Color(0.08f, 0.08f, 0.12f));
        SetRect(bg.GetComponent<RectTransform>(), 0, 1, 0, 1, 0, 0, 0, 0);

        // Top Bar
        var backBtn = CreateButton("BackButton", canvas.transform, "< BACK", 16, new Color(0.3f, 0.32f, 0.4f));
        SetRect(backBtn.GetComponent<RectTransform>(), 0, 0, 1, 1, 80, -45, 120, 45);
        ctrl.backButton = backBtn;

        var title = CreateText("Title", canvas.transform, "ROOM CLEARED! CHOOSE 2 REWARDS", 26, FontStyles.Bold, TextAlignmentOptions.Center);
        title.color = new Color(1f, 0.85f, 0.2f);
        SetRect(title.rectTransform, 0, 1, 1, 1, 0, -50, 0, 45);

        // Card Container
        var cardContainer = CreatePanel("CardContainer", canvas.transform, Color.clear);
        SetRect(cardContainer.GetComponent<RectTransform>(), 0.15f, 0.85f, 0.25f, 0.8f, 0, 0, 0, 0);
        var hlg = cardContainer.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 40;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        ctrl.cardContainer = cardContainer.transform;

        // Confirm Button
        var confirmBtn = CreateButton("ConfirmBtn", canvas.transform, "CONFIRM REWARDS", 20, new Color(0.18f, 0.55f, 0.25f));
        SetRect(confirmBtn.GetComponent<RectTransform>(), 0.38f, 0.62f, 0, 0, 0, 70, 0, 55);
        ctrl.confirmButton = confirmBtn;

        // Rest Modal Overlay
        var restModal = CreatePanel("RestModal", canvas.transform, new Color(0.08f, 0.08f, 0.12f, 0.98f));
        SetRect(restModal.GetComponent<RectTransform>(), 0.25f, 0.75f, 0.15f, 0.85f, 0, 0, 0, 0);
        ctrl.restModal = restModal;

        var restTitle = CreateText("RestTitle", restModal.transform, "SELECT 2 HEROES TO REST (+50% Energy)", 22, FontStyles.Bold, TextAlignmentOptions.Center);
        restTitle.color = new Color(0.95f, 0.85f, 0.3f);
        SetRect(restTitle.rectTransform, 0, 1, 1, 1, 0, -35, 0, 35);

        var restList = CreatePanel("RestHeroList", restModal.transform, Color.clear);
        SetRect(restList.GetComponent<RectTransform>(), 0.05f, 0.95f, 0.18f, 0.88f, 0, 0, 0, 0);
        var rvlg = restList.AddComponent<VerticalLayoutGroup>();
        rvlg.spacing = 10;
        rvlg.childAlignment = TextAnchor.UpperCenter;
        ctrl.restHeroList = restList.transform;

        var restConfirm = CreateButton("ConfirmRest", restModal.transform, "CONFIRM REST", 18, new Color(0.18f, 0.55f, 0.25f));
        SetRect(restConfirm.GetComponent<RectTransform>(), 0.3f, 0.7f, 0, 0, 0, 35, 0, 48);
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

        // Background
        var bg = CreatePanel("Background", canvas.transform, new Color(0.08f, 0.08f, 0.12f));
        SetRect(bg.GetComponent<RectTransform>(), 0, 1, 0, 1, 0, 0, 0, 0);

        // Title
        ctrl.titleLabel = CreateText("Title", canvas.transform, "DUNGEON CLEARED!", 36, FontStyles.Bold, TextAlignmentOptions.Center);
        ctrl.titleLabel.color = new Color(1f, 0.85f, 0.2f);
        SetRect(ctrl.titleLabel.rectTransform, 0, 1, 1, 1, 0, -80, 0, 60);

        // Stats Box
        var statsBox = CreatePanel("StatsBox", canvas.transform, new Color(0.12f, 0.14f, 0.2f, 0.9f));
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

        // Survivors Container
        var surBox = CreatePanel("SurvivorsBox", canvas.transform, new Color(0.12f, 0.14f, 0.2f, 0.9f));
        SetRect(surBox.GetComponent<RectTransform>(), 0.55f, 0.85f, 0.3f, 0.78f, 0, 0, 0, 0);

        var surHeader = CreateText("Header", surBox.transform, "HERO STATUS", 20, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(surHeader.rectTransform, 0, 1, 1, 1, 0, -30, 0, 30);

        var surContainer = CreatePanel("List", surBox.transform, Color.clear);
        SetRect(surContainer.GetComponent<RectTransform>(), 0.05f, 0.95f, 0.05f, 0.85f, 0, 0, 0, 0);
        var shlg = surContainer.AddComponent<HorizontalLayoutGroup>();
        shlg.spacing = 15;
        shlg.childAlignment = TextAnchor.MiddleCenter;
        ctrl.survivorContainer = surContainer.transform;

        // Buttons
        var playAgain = CreateButton("PlayAgainBtn", canvas.transform, "PLAY AGAIN", 20, new Color(0.18f, 0.55f, 0.25f));
        SetRect(playAgain.GetComponent<RectTransform>(), 0.3f, 0.48f, 0, 0, 0, 80, 0, 55);
        ctrl.playAgainButton = playAgain;

        var menuBtn = CreateButton("MainMenuBtn", canvas.transform, "MAIN MENU", 20, new Color(0.35f, 0.38f, 0.48f));
        SetRect(menuBtn.GetComponent<RectTransform>(), 0.52f, 0.7f, 0, 0, 0, 80, 0, 55);
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
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.12f);
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

    private static GameObject CreatePanel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = squareSprite;
        img.color = color;
        return go;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string content, float size, FontStyles style, TextAlignmentOptions align)
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
        return tmp;
    }

    private static Button CreateButton(string name, Transform parent, string label, float fontSize, Color bgColor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = squareSprite;
        img.color = bgColor;
        var btn = go.AddComponent<Button>();

        var text = CreateText("Label", go.transform, label, fontSize, FontStyles.Bold, TextAlignmentOptions.Center);
        SetRect(text.rectTransform, 0, 1, 0, 1, 0, 0, 0, 0);

        return btn;
    }

    private static Slider CreateSlider(string name, Transform parent, Color fillColor)
    {
        var sliderGO = new GameObject(name, typeof(RectTransform));
        sliderGO.transform.SetParent(parent, false);
        var slider = sliderGO.AddComponent<Slider>();

        // Background
        var bg = new GameObject("Background", typeof(RectTransform));
        bg.transform.SetParent(sliderGO.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.sprite = squareSprite;
        bgImg.color = new Color(0.15f, 0.15f, 0.2f);
        SetRect(bg.GetComponent<RectTransform>(), 0, 1, 0, 1, 0, 0, 0, 0);

        // Fill Area
        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderGO.transform, false);
        SetRect(fillArea.GetComponent<RectTransform>(), 0, 1, 0, 1, 0, 0, 0, 0);

        var fill = new GameObject("Fill", typeof(RectTransform));
        fill.transform.SetParent(fillArea.transform, false);
        var fillImg = fill.AddComponent<Image>();
        fillImg.sprite = squareSprite;
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
        if (minX == maxX && minY == maxY)
        {
            rt.sizeDelta = new Vector2(sizeX, sizeY);
        }
        else
        {
            rt.sizeDelta = new Vector2(sizeX, sizeY);
        }
    }

    private static void SavePrefab(GameObject go, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(go, path);
        GameObject.DestroyImmediate(go);
    }
}
#endif
