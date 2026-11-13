using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Main dungeon combat controller — Soul Knight-style room-by-room auto-battle.
/// Generates a floor with procedural rooms, spawns enemies per room,
/// heroes auto-navigate and fight through each room sequentially.
/// Includes interactive 3-option upgrade selection between rooms.
/// </summary>
public class DungeonCombatController : MonoBehaviour, ICombatController
{
    [Header("Dungeon")]
    public DungeonGenerator dungeonGenerator;
    public Transform entityContainer;         // parent for all hero/enemy GameObjects
    public Camera mainCamera;

    [Header("HUD - Top Bar")]
    public TextMeshProUGUI roomLabel;
    public TextMeshProUGUI floorLabel;
    public TextMeshProUGUI goldLabel;
    public Button pauseButton;
    public Button speedButton;
    public TextMeshProUGUI speedLabel;
    public GameObject pausePanel;
    public Button resumeButton;
    public Button quitButton;

    [Header("HUD - Minimap")]
    public RectTransform minimapContainer;
    public GameObject minimapDotPrefab;

    [Header("HUD - Bottom Portrait Panel")]
    public Transform portraitContainer;
    public GameObject portraitPrefab;

    [Header("HUD - Boss HP")]
    public GameObject bossHpRoot;
    public Slider bossHpBar;
    public TextMeshProUGUI bossNameLabel;
    public TextMeshProUGUI bossHitCounter;

    [Header("Hero Detail Panel")]
    public GameObject heroDetailPanel;
    public TextMeshProUGUI detailHeroName;
    public TextMeshProUGUI detailHP;
    public TextMeshProUGUI detailEnergy;
    public TextMeshProUGUI detailUpgrades;
    public Button detailBackBtn;

    [Header("Room Upgrade Modal")]
    public GameObject roomUpgradeModal;
    private int upgradeSelectedCardIdx = 0;
    private int lastRoomGoldEarned = 50;

    [Header("Route Choice Modal")]
    private GameObject routeChoiceModal;
    private List<GameObject> activeTraps = new();

    [Header("Cyberpunk Guide Dialogue & Mercenary Market")]
    private GameObject dialogueBoxRoot;
    private TextMeshProUGUI dialogueSpeakerText;
    private TextMeshProUGUI dialogueBodyText;
    private GameObject dialogueCalloutArrow;
    private GameObject mercenaryMarketModal;
    private Coroutine dialogueDismissCoroutine;

    [Header("Dedicated Non-Overlapping HUD Announcement & Boss Alert Banners")]
    private GameObject bossAlertBannerRoot;
    private TextMeshProUGUI bossAlertText;
    private Coroutine bossAlertCoroutine;
    private GameObject screenAnnouncementBannerRoot;
    private TextMeshProUGUI screenAnnouncementText;
    private Coroutine screenAnnouncementCoroutine;
    private readonly List<Transform> activeCombatantTransforms = new List<Transform>(32);

    [Header("HUD - Reposition (Repo)")]
    public Button repoButton;
    public GameObject repoModal;
    private HeroInstance[] repoFormation = new HeroInstance[9];
    private GridSlotUI[] repoGridSlots = new GridSlotUI[9];
    private int repoSelectedSlot = -1;
    private GameObject repoStatsPanel;
    private TextMeshProUGUI repoStatsName;
    private TextMeshProUGUI repoStatsClass;
    private TextMeshProUGUI repoStatsHP;
    private TextMeshProUGUI repoStatsEnergy;
    private TextMeshProUGUI repoStatsATK;

    [Header("HUD - Quick Buffs Bar")]
    private GameObject quickBuffBarRoot;
    private Button powerBuffBtn;
    private Button speedBuffBtn;
    private Button reviveBuffBtn;
    private TextMeshProUGUI powerBuffCostText;
    private TextMeshProUGUI speedBuffCostText;
    private TextMeshProUGUI reviveBuffCostText;
    private Image powerBuffBg;
    private Image speedBuffBg;
    private Image reviveBuffBg;

    // ---- Runtime State ----
    private List<HeroCombatAgent> heroAgents = new();
    private List<EnemyCombatAgent> enemyAgents = new();
    private List<PortraitUI> portraits = new();

    private bool isPaused = false;
    private bool isDoubleSpeed = false;
    private bool isInCombatPhase = false;
    private int currentRoomIndex = 0;

    // Boss fight state
    private int bossMaxHP = 500;
    private int bossCurrentHP;
    private int bossHitCount = 0;
    private const int BOSS_HIT_LIMIT = 3;

    // Camera follow
    private Vector3 cameraTargetPos;
    private float cameraFollowSpeed = 3.5f;

    // ---- Public Accessors ----
    public List<HeroCombatAgent> HeroAgents => heroAgents;
    public List<EnemyCombatAgent> EnemyAgents => enemyAgents;
    public bool IsPaused => isPaused;
    public bool IsInCombatPhase => isInCombatPhase;

    /// <summary>
    /// Returns the inner walkable bounds (with wall margin) for the current combat room.
    /// </summary>
    public Rect? GetCurrentRoomBounds()
    {
        if (dungeonGenerator == null) return null;
        return dungeonGenerator.GetInnerRoomBounds(currentRoomIndex);
    }

    void Start()
    {
        Time.timeScale = 1f;
        Application.runInBackground = true;

        // Preload sprite cache
        SpriteCache.PreloadAll();

        GameRunData.Instance?.EnsureSquadAndFormation();

        // Check if entire squad is wiped out
        if (GameRunData.Instance != null && GameRunData.Instance.AliveCount == 0 && GameRunData.Instance.currentRoom > 0)
        {
            SceneNav.GoTo(SceneNames.GameOver);
            return;
        }

        // Only revive / initialize full HP on brand new run (Floor 0). Subsequent floors preserve surviving hero HP & casualties!
        if (GameRunData.Instance != null && GameRunData.Instance.currentRoom == 0)
        {
            foreach (var h in GameRunData.Instance.squad)
            {
                if (h != null)
                {
                    if (h.currentHP <= 0) h.currentHP = h.data.maxHP;
                    h.currentEnergy = h.data.maxEnergy;
                }
            }
        }

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera != null)
        {
            mainCamera.orthographicSize = 6.0f; // Wide, clear framing for all heroes and enemies
            if (mainCamera.GetComponent<CombatCameraController>() == null)
            {
                mainCamera.gameObject.AddComponent<CombatCameraController>();
            }
        }

        if (dungeonGenerator == null)
        {
            dungeonGenerator = GetComponent<DungeonGenerator>() ?? FindAnyObjectByType<DungeonGenerator>();
        }

        try
        {
            SetupButtons();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DungeonCombatController] Error in SetupButtons: {ex}");
        }

        try
        {
            BuildPortraits();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[DungeonCombatController] Error in BuildPortraits: {ex}");
        }

        // Generate dungeon floor
        int floorNumber = GameRunData.Instance?.currentRoom ?? 0;
        if (dungeonGenerator != null)
        {
            dungeonGenerator.GenerateFloor(floorNumber);
        }

        // Second-half alert announcement (Floor 3+)
        if (floorNumber >= 2)
        {
            ShowScreenAnnouncement("⚠ ENTERING DEEP CORRIDORS — ENEMY STATS ESCALATED (2x ATK, 1.5x HP)!", new Color(1f, 0.25f, 0.25f));
        }

        // Initialize camera arena bounds for first room
        if (dungeonGenerator != null)
        {
            CombatCameraController.Instance?.SetArenaBounds(dungeonGenerator.GetInnerRoomBounds(0));

            // Position camera immediately at first room center (locking Z to -10)
            Vector2 firstCenter = dungeonGenerator.GetRoomCenter(0);
            cameraTargetPos = new Vector3(firstCenter.x, firstCenter.y, -10f);
            if (mainCamera != null)
            {
                mainCamera.transform.position = cameraTargetPos;
            }
        }

        // Spawn heroes at first room
        SpawnHeroes();

        // Start the dungeon loop
        StartCoroutine(DungeonLoop());

        RefreshGold();
        UpdateFloorLabel();
        UpdateRoomLabel();
        UpdateStakeTrackerUI();
    }

    void SetupButtons()
    {
        if (pauseButton) pauseButton.onClick.AddListener(TogglePause);
        if (resumeButton) resumeButton.onClick.AddListener(TogglePause);
        if (quitButton) quitButton.onClick.AddListener(() => SceneNav.GoTo(SceneNames.MainMenu));
        if (speedButton) speedButton.onClick.AddListener(ToggleSpeed);
        if (detailBackBtn) detailBackBtn.onClick.AddListener(CloseDetailPanel);

        SetupRepoButton();
        SetupQuickBuffBar();

        if (pausePanel) pausePanel.SetActive(false);
        if (heroDetailPanel) heroDetailPanel.SetActive(false);
        if (bossHpRoot) bossHpRoot.SetActive(false);
        if (roomUpgradeModal) roomUpgradeModal.SetActive(false);
        if (bossClearModal) bossClearModal.SetActive(false);
        if (repoModal) repoModal.SetActive(false);
        if (routeChoiceModal) routeChoiceModal.SetActive(false);
        if (mercenaryMarketModal) mercenaryMarketModal.SetActive(false);
        if (dialogueBoxRoot) dialogueBoxRoot.SetActive(false);
    }

    // ---- Portrait Panel ----
    void BuildPortraits()
    {
        if (GameRunData.Instance == null || portraitPrefab == null || portraitContainer == null) return;
        portraits.Clear();

        for (int i = portraitContainer.childCount - 1; i >= 0; i--)
        {
            var child = portraitContainer.GetChild(i);
            if (child != null) Destroy(child.gameObject);
        }

        if (GameRunData.Instance.squad == null) return;

        foreach (var hero in GameRunData.Instance.squad)
        {
            if (hero == null || hero.data == null) continue;
            if (hero.upgrades == null) hero.upgrades = new();
            var go = Instantiate(portraitPrefab, portraitContainer);
            if (go == null) continue;
            var p = go.GetComponent<PortraitUI>();
            if (p != null)
            {
                p.Setup(hero, OnPortraitTapped);
                portraits.Add(p);
            }
        }
    }

    void OnPortraitTapped(HeroInstance hero)
    {
        if (hero == null) return;
        OpenHeroDetail(hero);
    }

    // ---- Hero Spawning ----
    void SpawnHeroes()
    {
        if (GameRunData.Instance == null) return;

        // Ensure squad has 5 heroes
        GameRunData.Instance.EnsureSquadAndFormation();
        var squad = GameRunData.Instance.squad;
        if (squad == null || squad.Count == 0) return;

        var formation = GameRunData.Instance.formation;
        if (formation == null || formation.Length != 9)
        {
            formation = new HeroInstance[9];
            GameRunData.Instance.formation = formation;
        }

        // Check which squad heroes are currently placed in formation
        var placed = new HashSet<HeroInstance>();
        for (int i = 0; i < 9; i++)
        {
            if (formation[i] != null)
            {
                if (squad.Contains(formation[i]))
                    placed.Add(formation[i]);
                else
                    formation[i] = null;
            }
        }

        // Fallback default slots: Frontline (2, 8), Midline (1, 7), Backline (0, 3, 4, 5, 6)
        int[] fallbackSlots = new int[] { 2, 8, 1, 7, 4, 0, 3, 5, 6 };
        int fallbackIdx = 0;
        foreach (var hero in squad)
        {
            if (hero == null) continue;
            if (!placed.Contains(hero))
            {
                while (fallbackIdx < fallbackSlots.Length && formation[fallbackSlots[fallbackIdx]] != null)
                    fallbackIdx++;

                if (fallbackIdx < fallbackSlots.Length)
                {
                    formation[fallbackSlots[fallbackIdx]] = hero;
                    hero.gridIndex = fallbackSlots[fallbackIdx];
                    placed.Add(hero);
                }
            }
        }

        heroAgents.Clear();
        int spawnOrder = 0;
        for (int i = 0; i < 9; i++)
        {
            var hero = formation[i];
            if (hero == null) continue;

            var go = new GameObject($"Hero_{hero.displayName}");
            go.transform.SetParent(entityContainer, false);

            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = 10;

            var agent = go.AddComponent<HeroCombatAgent>();
            agent.Init(hero, i, this);

            // Position near first room entrance with clean spacing
            Vector2 spawnPos = dungeonGenerator.GetSpawnPosition(0, false, spawnOrder, squad.Count);
            go.transform.position = (Vector3)spawnPos;
            heroAgents.Add(agent);
            spawnOrder++;

            if (!hero.isAlive)
            {
                agent.SetInitiallyDead();
            }
        }
    }

    // ---- Dungeon Room Loop ----
    IEnumerator DungeonLoop()
    {
        var rooms = dungeonGenerator.Rooms;
        currentRoomIndex = 0;

        for (int r = 0; r < rooms.Count; r++)
        {
            currentRoomIndex = r;
            var room = rooms[r];

            UpdateRoomLabel();

            // Set arena boundaries for camera confinement
            CombatCameraController.Instance?.SetArenaBounds(dungeonGenerator.GetInnerRoomBounds(r));

            // 1. Move heroes to room
            yield return StartCoroutine(MoveHeroesToRoom(r));

            // Slide in tactical quick-buffs HUD
            StartCoroutine(SlideInQuickBuffBar());

            // 2. Wait a beat before spawning enemies
            yield return new WaitForSeconds(0.4f);

            // 3. Spawn enemies in this room
            SpawnEnemiesInRoom(r, room);

            // Contextual Cyberpunk Guide Dialogue (docked cleanly on the top-left)
            if (r == 0)
            {
                ShowCyberpunkDialogue("FIXER // KIRA", "Drop in, Cleaners! Hostile signatures ahead. Check your tactical Quick-Buffs in the bottom-left if things get messy. Move out!", "quickbuff");
            }
            else if (room.isBossRoom)
            {
                ShowCyberpunkDialogue("FIXER // KIRA", "High-threat signature detected: SKELETON KING! Watch for his heavy slam telegraph — when he flashes red, clear the impact zone!");
            }

            // 4. Setup boss if boss room
            if (room.isBossRoom)
                SetupBossRoom();

            // 5. Enter combat phase
            isInCombatPhase = true;

            // 6. Wait for combat to resolve
            yield return StartCoroutine(WaitForCombatEnd());

            // 7. Check if heroes wiped
            bool heroesWiped = heroAgents.TrueForAll(a => !a.IsAlive);
            if (heroesWiped)
            {
                yield return new WaitForSeconds(1.2f);
                SceneNav.GoTo(SceneNames.GameOver);
                yield break;
            }

            // 8. Room cleared
            room.isCleared = true;
            isInCombatPhase = false;
            ClearActiveTraps();

            // Calculate enemies defeated in this room and award gold bounty
            int enemiesDefeatedInRoom = 0;
            foreach (var e in enemyAgents)
            {
                if (e != null && e.RoomIndex == currentRoomIndex && !e.IsAlive)
                    enemiesDefeatedInRoom++;
            }

            int goldBefore = GameRunData.Instance?.gold ?? 0;
            GameRunData.Instance?.OnRoomCleared(enemiesDefeatedInRoom);
            int baseEarned = (GameRunData.Instance?.gold ?? 0) - goldBefore;

            // Apply route gold multiplier if chosen
            float goldMult = room.activeRoute != null ? room.activeRoute.goldMultiplier : 1.0f;
            int totalEarned = baseEarned;
            if (goldMult > 1.01f)
            {
                int bonus = Mathf.RoundToInt(baseEarned * (goldMult - 1f));
                GameRunData.Instance?.AddGold(bonus);
                totalEarned += bonus;
            }

            RefreshGold();

            // Process maturing investments staked in previous rooms
            ProcessActiveInvestments();

            if (totalEarned > 0)
            {
                lastRoomGoldEarned = totalEarned;
                string bonusTag = goldMult > 1.01f ? $" ({Mathf.RoundToInt(goldMult * 100)}% Bounty!)" : "";
                if (room.isBossRoom || r >= rooms.Count - 1)
                {
                    CashOutAllInvestments();
                    ShowScreenAnnouncement($"FLOOR CLEARED! +{totalEarned} GOLD{bonusTag}", new Color(1f, 0.88f, 0.2f));
                }
                CombatCameraController.Instance?.AddTrauma(0.25f);
            }

            // 9. Interactive 3-Upgrade Choice: Normal Room -> Previous Simple Panel; Boss Clear -> Stylized Window Panel
            if (!room.isBossRoom && r < rooms.Count - 1)
            {
                yield return PresentRoomUpgradeChoice();
            }
            else if (room.isBossRoom || r >= rooms.Count - 1)
            {
                yield return PresentBossClearUpgradeChoice();
            }

            // 10. Branching 2-Road Route Choice for NEXT room (if not the boss room)
            if (r + 1 < rooms.Count && !rooms[r + 1].isBossRoom)
            {
                yield return PresentRouteChoiceModal(rooms[r + 1]);
            }

            // Short breather before corridor march
            yield return new WaitForSeconds(0.5f);
        }

        // All rooms cleared — advance floor
        yield return new WaitForSeconds(1f);

        // Post-Boss Mercenary Exchange Market ("How Many Dudes" 2-Column UI)
        yield return StartCoroutine(PresentMercenaryMarketModal());

        if (GameRunData.Instance != null)
        {
            GameRunData.Instance.currentRoom++;
            if (GameRunData.Instance.currentRoom >= GameRunData.Instance.totalRooms)
                SceneNav.GoTo(SceneNames.GameOver); // Victory!
            else
                SceneNav.GoTo(SceneNames.Rewards);
        }
    }

    IEnumerator MoveHeroesToRoom(int roomIndex)
    {
        Vector2 roomCenter = dungeonGenerator.GetRoomCenter(roomIndex);

        if (roomIndex == 0)
        {
            // First room: simple direct move (heroes spawn adjacent)
            Vector2 squadTarget = new Vector2(roomCenter.x - 1.2f, roomCenter.y);
            foreach (var hero in heroAgents)
            {
                if (!hero.IsAlive) continue;
                hero.MoveTo(squadTarget);
            }
        }
        else
        {
            // Subsequent rooms: 3-waypoint corridor pathing
            // Exit current room door → corridor center → entry next room door → formation assembly
            Vector2[] corridorWaypoints = dungeonGenerator.GetCorridorWaypoints(roomIndex - 1, roomIndex);
            Vector2 assemblyPoint = new Vector2(roomCenter.x - 1.2f, roomCenter.y);

            if (corridorWaypoints != null)
            {
                // Build full path: corridor waypoints + final assembly point in the new room
                Vector2[] fullPath = new Vector2[corridorWaypoints.Length + 1];
                corridorWaypoints.CopyTo(fullPath, 0);
                fullPath[fullPath.Length - 1] = assemblyPoint;

                foreach (var hero in heroAgents)
                {
                    if (!hero.IsAlive) continue;
                    hero.MoveAlongPath(fullPath);
                }
            }
            else
            {
                // Fallback: direct move if waypoints not available
                Vector2 squadTarget = new Vector2(roomCenter.x - 1.2f, roomCenter.y);
                foreach (var hero in heroAgents)
                {
                    if (!hero.IsAlive) continue;
                    hero.MoveTo(squadTarget);
                }
            }
        }

        // Wait until heroes arrive or reasonable timeout
        float timeout = 12f; // increased for longer corridor paths
        float elapsed = 0f;
        while (elapsed < timeout)
        {
            if (isPaused)
            {
                yield return null;
                continue;
            }

            bool allArrived = true;
            foreach (var hero in heroAgents)
            {
                if (!hero.IsAlive) continue;
                if (!hero.HasReachedTarget())
                {
                    allArrived = false;
                    break;
                }
            }

            if (allArrived) break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(0.2f);
    }

    void SpawnEnemiesInRoom(int roomIndex, DungeonRoom room)
    {
        ClearActiveTraps();
        bool isBoss = room.isBossRoom;
        var route = room.activeRoute;
        int enemyCount = isBoss ? 1 : (route != null ? route.enemyCount : Random.Range(3, 6));

        int floor = GameRunData.Instance?.currentRoom ?? 0;
        if (!isBoss && floor > 2 && route == null)
            enemyCount += Random.Range(0, 2);

        for (int i = 0; i < enemyCount; i++)
        {
            var go = new GameObject($"Enemy_{roomIndex}_{i}");
            go.transform.SetParent(entityContainer, false);

            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = 10;

            var enemy = go.AddComponent<EnemyCombatAgent>();
            bool isNecro = !isBoss && ((floor >= 2 && i == 1 && enemyCount > 3) || (route != null && route.title.Contains("WARLORD") && i == 1));
            enemy.Init(isBoss, this, roomIndex, isNecro ? EnemyArchetype.Necromancer : EnemyArchetype.StandardMelee);

            if (!isBoss)
            {
                bool isEliteUnit = (route != null && route.routeType == RoomRouteType.Elite && i == 0);
                float hpM = isEliteUnit ? route.enemyHpMultiplier : 1.0f;
                float dmgM = isEliteUnit ? route.enemyDmgMultiplier : 1.0f;

                // Second-half difficulty escalation (Floor 3+)
                if (floor >= 2)
                {
                    hpM *= 1.5f;
                    dmgM *= 2.0f;
                    enemy.moveSpeed *= 1.2f;
                }

                enemy.ApplyModifiers(hpM, dmgM, isEliteUnit);
            }

            // Position in room
            go.transform.position = (Vector3)dungeonGenerator.GetSpawnPosition(roomIndex, true, i, enemyCount);

            enemyAgents.Add(enemy);
        }

        // Spawn traps if route is trapped
        if (route != null && route.hasTraps)
        {
            SpawnTrapsInRoom(roomIndex, room);
        }
    }

    void ClearActiveTraps()
    {
        for (int i = activeTraps.Count - 1; i >= 0; i--)
        {
            if (activeTraps[i] != null) Destroy(activeTraps[i]);
        }
        activeTraps.Clear();
    }

    void SpawnTrapsInRoom(int roomIndex, DungeonRoom room)
    {
        int trapCount = Random.Range(3, 5);
        float halfW = room.bounds.width * 0.28f;
        float halfH = room.bounds.height * 0.28f;

        for (int i = 0; i < trapCount; i++)
        {
            var trapGo = new GameObject($"SpikeTrap_{roomIndex}_{i}");
            trapGo.transform.SetParent(entityContainer, false);

            var trap = trapGo.AddComponent<SpikeTrap>();
            float offsetX = Random.Range(-halfW, halfW);
            float offsetY = Random.Range(-halfH, halfH);
            trap.Init(room.center + new Vector2(offsetX, offsetY));

            activeTraps.Add(trapGo);
        }
    }

    IEnumerator WaitForCombatEnd()
    {
        while (true)
        {
            if (isPaused)
            {
                yield return null;
                continue;
            }

            // Check if all enemies in current room are dead
            bool allEnemiesDead = true;
            foreach (var e in enemyAgents)
            {
                if (e.RoomIndex == currentRoomIndex && e.IsAlive)
                {
                    allEnemiesDead = false;
                    break;
                }
            }

            bool allHeroesDead = heroAgents.TrueForAll(a => !a.IsAlive);

            if (allEnemiesDead || allHeroesDead)
                break;

            RefreshPortraits();
            yield return new WaitForSeconds(isDoubleSpeed ? 0.05f : 0.1f);
        }
    }

    // ---- Camera Tracking ----
    void LateUpdate()
    {
        if (CombatCameraController.Instance != null)
        {
            activeCombatantTransforms.Clear();
            foreach (var h in heroAgents)
            {
                if (h != null && h.IsAlive) activeCombatantTransforms.Add(h.transform);
            }
            if (isInCombatPhase)
            {
                foreach (var e in enemyAgents)
                {
                    if (e != null && e.IsAlive && e.RoomIndex == currentRoomIndex)
                        activeCombatantTransforms.Add(e.transform);
                }
            }
            CombatCameraController.Instance.SetTrackedTargets(activeCombatantTransforms);
            return;
        }

        // Smoothly follow the centroid of alive heroes across rooms and corridors
        if (heroAgents.Count > 0)
        {
            Vector2 heroCentroid = Vector2.zero;
            int count = 0;
            foreach (var h in heroAgents)
            {
                if (!h.IsAlive) continue;
                heroCentroid += (Vector2)h.transform.position;
                count++;
            }
            if (count > 0)
            {
                heroCentroid /= count;

                // In combat, bias camera smoothly toward active battle line
                Vector2 targetCenter = heroCentroid;
                if (isInCombatPhase)
                {
                    Vector2 enemyCentroid = Vector2.zero;
                    int enemyCount = 0;
                    foreach (var e in enemyAgents)
                    {
                        if (e.RoomIndex == currentRoomIndex && e.IsAlive)
                        {
                            enemyCentroid += (Vector2)e.transform.position;
                            enemyCount++;
                        }
                    }
                    if (enemyCount > 0)
                    {
                        enemyCentroid /= enemyCount;
                        targetCenter = Vector2.Lerp(heroCentroid, enemyCentroid, 0.35f);
                    }
                }

                cameraTargetPos = new Vector3(targetCenter.x, targetCenter.y, -10f);
            }
        }

        if (mainCamera != null)
        {
            mainCamera.transform.position = Vector3.Lerp(
                mainCamera.transform.position,
                cameraTargetPos,
                cameraFollowSpeed * Time.deltaTime
            );
        }
    }

    // ---- Interactive 3-Upgrade Selection & Staked Investments Between Rooms ----
    [System.Serializable]
    private struct ActiveGoldInvestment
    {
        public int stakeAmount;
        public int roomsRemaining;
        public int totalRoomsStaked;
        public float multiplier;
        public string planName;
    }

    private List<ActiveGoldInvestment> activeInvestments = new();
    private GameObject stakeTrackerRoot;
    private TextMeshProUGUI stakeTrackerText;

    private void EnsureStakeTrackerUI()
    {
        if (stakeTrackerRoot != null) return;
        var canvas = pauseButton != null ? pauseButton.GetComponentInParent<Canvas>() : FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        stakeTrackerRoot = new GameObject("StakeTrackerBadge", typeof(RectTransform));
        stakeTrackerRoot.transform.SetParent(canvas.transform, false);

        var rt = stakeTrackerRoot.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -50f);
        rt.sizeDelta = new Vector2(380f, 24f);

        var bg = stakeTrackerRoot.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.08f, 0.16f, 0.94f);

        var outline = stakeTrackerRoot.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.82f, 0.2f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, 1.5f);

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(stakeTrackerRoot.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        stakeTrackerText = textGo.AddComponent<TextMeshProUGUI>();
        stakeTrackerText.fontSize = 11;
        stakeTrackerText.fontStyle = FontStyles.Bold;
        stakeTrackerText.alignment = TextAlignmentOptions.Center;
        stakeTrackerText.color = new Color(1f, 0.9f, 0.3f);
        stakeTrackerText.raycastTarget = false;
    }

    private void UpdateStakeTrackerUI()
    {
        if (activeInvestments.Count == 0)
        {
            if (stakeTrackerRoot != null) stakeTrackerRoot.SetActive(false);
            return;
        }

        EnsureStakeTrackerUI();
        if (stakeTrackerRoot == null) return;
        stakeTrackerRoot.SetActive(true);

        var inv = activeInvestments[0];
        int payout = Mathf.RoundToInt(inv.stakeAmount * inv.multiplier);
        string countSuffix = activeInvestments.Count > 1 ? $" (+{activeInvestments.Count - 1} more)" : "";
        if (stakeTrackerText != null)
        {
            stakeTrackerText.text = $"<color=#FCD34D>[STAKE: {inv.planName}]</color> <color=#38BDF8>{inv.roomsRemaining} ROOM(S) LEFT</color> -> <color=#10B981>+{payout}G{countSuffix}</color>";
        }
    }

    private void ProcessActiveInvestments()
    {
        if (activeInvestments.Count == 0)
        {
            UpdateStakeTrackerUI();
            return;
        }

        for (int i = activeInvestments.Count - 1; i >= 0; i--)
        {
            var inv = activeInvestments[i];
            inv.roomsRemaining--;

            if (inv.roomsRemaining <= 0)
            {
                // Dynamic scaling with dungeon depth progress
                int depthBonus = (currentRoomIndex + 1) * 12;
                int payout = Mathf.RoundToInt(inv.stakeAmount * inv.multiplier) + depthBonus;
                GameRunData.Instance?.AddGold(payout);
                RefreshGold();
                activeInvestments.RemoveAt(i);
                UpdateStakeTrackerUI();

                ShowCyberpunkDialogue("FIXER // KIRA", $"Stake contract fulfilled! The vault delivered +{payout}G to our operational reserve!");
                ShowScreenAnnouncement($"★ STAKE MATURED: +{payout} GOLD COLLECTED ({inv.multiplier:0.0}x return)! ★", new Color(1f, 0.85f, 0.2f));
                CombatCameraController.Instance?.AddTrauma(0.25f);
            }
            else
            {
                activeInvestments[i] = inv;
                UpdateStakeTrackerUI();
                ShowScreenAnnouncement($"INVESTMENT: {inv.roomsRemaining} room(s) until payout ({Mathf.RoundToInt(inv.stakeAmount * inv.multiplier)}G)!", Color.cyan);
            }
        }
    }

    private void CashOutAllInvestments()
    {
        if (activeInvestments.Count == 0) return;
        for (int i = activeInvestments.Count - 1; i >= 0; i--)
        {
            var inv = activeInvestments[i];
            int payout = Mathf.RoundToInt(inv.stakeAmount * Mathf.Max(1.2f, inv.multiplier * 0.85f));
            GameRunData.Instance?.AddGold(payout);
            activeInvestments.RemoveAt(i);
        }
        RefreshGold();
        UpdateStakeTrackerUI();
    }

    private class RoomUpgradeOption
    {
        public string title;
        public string description;
        public string icon;
        public int cost = 0;
        public bool isInvestment = false;
        public int stakeAmount = 0;
        public int roomsLocked = 0;
        public float investmentMultiplier = 1.0f;
        public System.Action onSelect;
    }

    IEnumerator PresentRoomUpgradeChoice()
    {
        int revenue = Mathf.Max(45, lastRoomGoldEarned);

        // Tier 1: Accessible Choice (~0.9x room revenue)
        // Consumes ~90% of the gold gained from the cleared room
        int cost1 = Mathf.RoundToInt(revenue * 0.90f);

        // Tier 2: Expensive Choice (~2.2x room revenue)
        int cost2 = Mathf.RoundToInt(revenue * 2.20f);

        // Tier 3: Elite Legendary / Deep Staked Investment (~3.6x room revenue)
        int cost3 = Mathf.RoundToInt(revenue * 3.60f);

        // Tier 1 Pool (Affordable)
        var tier1Pool = new List<RoomUpgradeOption>
        {
            new RoomUpgradeOption
            {
                title = "FIELD RATIONS & STIM",
                description = "+25 Max HP & +30 HP heal immediately to squad.\n<color=#FCD34D>Accessible combat kit</color>",
                icon = "<color=#10B981><b>[ RATIONS ]</b></color>",
                cost = cost1,
                onSelect = () => {
                    foreach (var h in heroAgents)
                    {
                        if (h.IsAlive)
                        {
                            h.heroInstance.data.maxHP += 25;
                            h.ReceiveHeal(30);
                            h.heroInstance.upgrades.Add("Field Rations (+25 Max HP, +30 HP)");
                        }
                    }
                    RefreshPortraits();
                }
            },
            new RoomUpgradeOption
            {
                title = "COMBAT ADRENALINE",
                description = "+18% ATK & +15% Move Speed for all squad heroes.\n<color=#FCD34D>Standard tactical enhancement</color>",
                icon = "<color=#EF4444><b>[ STIM ]</b></color>",
                cost = cost1,
                onSelect = () => {
                    foreach (var h in heroAgents)
                    {
                        h.heroInstance.data.attackDamage = Mathf.RoundToInt(h.heroInstance.data.attackDamage * 1.18f);
                        h.heroInstance.data.moveSpeed *= 1.15f;
                        h.heroInstance.upgrades.Add("Combat Adrenaline (+18% ATK, +15% SPD)");
                    }
                    RefreshPortraits();
                }
            },
            new RoomUpgradeOption
            {
                title = "SHORT-TERM BOND",
                description = $"Stake <b>{cost1}G</b> for <b>1 room</b>.\nSurvive to claim <b>{Mathf.RoundToInt(cost1 * 1.75f)}G (1.75x)</b> payout!",
                icon = "<color=#FCD34D><b>[ STAKE ]</b></color>",
                cost = cost1,
                isInvestment = true,
                stakeAmount = cost1,
                roomsLocked = 1,
                investmentMultiplier = 1.75f,
                onSelect = () => {
                    activeInvestments.Add(new ActiveGoldInvestment
                    {
                        stakeAmount = cost1,
                        roomsRemaining = 1,
                        totalRoomsStaked = 1,
                        multiplier = 1.75f,
                        planName = "SHORT-TERM BOND"
                    });
                    ShowScreenAnnouncement($"STAKED {cost1}G! Matures in 1 room for {Mathf.RoundToInt(cost1 * 1.75f)}G!", Color.yellow);
                    UpdateStakeTrackerUI();
                }
            }
        };

        // Tier 2 Pool (Much more expensive)
        var tier2Pool = new List<RoomUpgradeOption>
        {
            new RoomUpgradeOption
            {
                title = "TITAN'S BULWARK",
                description = "+55 Max HP & Full Squad Heal & +20% Armor Defense.\n<color=#60A5FA>Heavy reinforced tactical plate</color>",
                icon = "<color=#3B82F6><b>[ BULWARK ]</b></color>",
                cost = cost2,
                onSelect = () => {
                    foreach (var h in heroAgents)
                    {
                        h.heroInstance.data.maxHP += 55;
                        h.heroInstance.currentHP = h.heroInstance.data.maxHP;
                        h.heroInstance.upgrades.Add("Titan's Bulwark (+55 Max HP, Full Heal)");
                    }
                    RefreshPortraits();
                }
            },
            new RoomUpgradeOption
            {
                title = "OVERCHARGED PYRE",
                description = "+40% Spell Power & +40 Max Energy to Mages.\n<color=#F97316>Arcane amplification matrix</color>",
                icon = "<color=#F97316><b>[ PYRE ]</b></color>",
                cost = cost2,
                onSelect = () => {
                    foreach (var h in heroAgents)
                    {
                        if (h.HeroClass == HeroClass.Mage)
                        {
                            h.heroInstance.data.attackDamage = Mathf.RoundToInt(h.heroInstance.data.attackDamage * 1.40f);
                            h.heroInstance.data.maxEnergy += 40;
                            h.heroInstance.upgrades.Add("Overcharged Pyre (+40% Spell Power, +40 Energy)");
                        }
                    }
                    RefreshPortraits();
                }
            },
            new RoomUpgradeOption
            {
                title = "ROYAL TREASURY STAKE",
                description = $"Stake <b>{cost2}G</b> for <b>2 rooms</b>.\nSurvive to claim <b>{Mathf.RoundToInt(cost2 * 2.40f)}G (2.4x)</b> payout!",
                icon = "<color=#FCD34D><b>[ STAKE ]</b></color>",
                cost = cost2,
                isInvestment = true,
                stakeAmount = cost2,
                roomsLocked = 2,
                investmentMultiplier = 2.40f,
                onSelect = () => {
                    activeInvestments.Add(new ActiveGoldInvestment
                    {
                        stakeAmount = cost2,
                        roomsRemaining = 2,
                        totalRoomsStaked = 2,
                        multiplier = 2.40f,
                        planName = "ROYAL TREASURY STAKE"
                    });
                    ShowScreenAnnouncement($"STAKED {cost2}G! Matures in 2 rooms for {Mathf.RoundToInt(cost2 * 2.40f)}G!", Color.yellow);
                    UpdateStakeTrackerUI();
                }
            }
        };

        // Tier 3 Pool (Elite Legendary / Deep Investment)
        var tier3Pool = new List<RoomUpgradeOption>
        {
            new RoomUpgradeOption
            {
                title = "WARLORD'S RELIC & CORE",
                description = "+50% Squad ATK & +60 Max HP to all heroes.\n<color=#A855F7>Apex dungeon conquering relic</color>",
                icon = "<color=#A855F7><b>[ WARLORD ]</b></color>",
                cost = cost3,
                onSelect = () => {
                    foreach (var h in heroAgents)
                    {
                        h.heroInstance.data.attackDamage = Mathf.RoundToInt(h.heroInstance.data.attackDamage * 1.50f);
                        h.heroInstance.data.maxHP += 60;
                        h.heroInstance.currentHP = h.heroInstance.data.maxHP;
                        h.heroInstance.upgrades.Add("Warlord's Relic (+50% ATK, +60 HP)");
                    }
                    RefreshPortraits();
                }
            },
            new RoomUpgradeOption
            {
                title = "CHRONO BERSERK",
                description = "+45% Attack Speed & Infinite Dodge Roll charges.\n<color=#EC4899>Hyper-velocity tactical overdrive</color>",
                icon = "<color=#EC4899><b>[ CHRONO ]</b></color>",
                cost = cost3,
                onSelect = () => {
                    foreach (var h in heroAgents)
                    {
                        h.heroInstance.data.attackSpeed *= 1.45f;
                        h.heroInstance.data.moveSpeed *= 1.30f;
                        h.heroInstance.upgrades.Add("Chrono Berserk (+45% ATK SPD)");
                    }
                    RefreshPortraits();
                }
            },
            new RoomUpgradeOption
            {
                title = "DEEP VAULT STAKE",
                description = $"Stake <b>{cost3}G</b> for <b>3 rooms</b>.\nSurvive to claim <b>{Mathf.RoundToInt(cost3 * 3.30f)}G (3.3x)</b> payout!",
                icon = "<color=#FCD34D><b>[ STAKE ]</b></color>",
                cost = cost3,
                isInvestment = true,
                stakeAmount = cost3,
                roomsLocked = 3,
                investmentMultiplier = 3.30f,
                onSelect = () => {
                    activeInvestments.Add(new ActiveGoldInvestment
                    {
                        stakeAmount = cost3,
                        roomsRemaining = 3,
                        totalRoomsStaked = 3,
                        multiplier = 3.30f,
                        planName = "DEEP VAULT STAKE"
                    });
                    ShowScreenAnnouncement($"STAKED {cost3}G! Matures in 3 rooms for {Mathf.RoundToInt(cost3 * 3.30f)}G!", Color.yellow);
                    UpdateStakeTrackerUI();
                }
            }
        };

        // Construct 3 distinct tiered choices matching user economics
        var chosenThree = new List<RoomUpgradeOption>
        {
            tier1Pool[Random.Range(0, tier1Pool.Count)],
            tier2Pool[Random.Range(0, tier2Pool.Count)],
            tier3Pool[Random.Range(0, tier3Pool.Count)]
        };

        bool choiceMade = false;

        // Ensure unified framed modal exists (supporting multi-purchase)
        EnsureRoomUpgradeModal(chosenThree, () => choiceMade = true);

        if (roomUpgradeModal) roomUpgradeModal.SetActive(true);

        // Wait for player to click Done / Proceed
        while (!choiceMade)
        {
            yield return null;
        }

        if (roomUpgradeModal) roomUpgradeModal.SetActive(false);
        yield return new WaitForSeconds(0.2f);
    }

    void EnsureRoomUpgradeModal(List<RoomUpgradeOption> options, System.Action onChosen)
    {
        var canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        if (screenAnnouncementBannerRoot != null)
        {
            screenAnnouncementBannerRoot.SetActive(false);
            if (screenAnnouncementCoroutine != null) StopCoroutine(screenAnnouncementCoroutine);
        }
        if (bossAlertBannerRoot != null)
        {
            bossAlertBannerRoot.SetActive(false);
            if (bossAlertCoroutine != null) StopCoroutine(bossAlertCoroutine);
        }

        if (roomUpgradeModal == null)
        {
            // Dimmed screen backdrop
            roomUpgradeModal = new GameObject("RoomUpgradeModal", typeof(RectTransform));
            roomUpgradeModal.transform.SetParent(canvas.transform, false);

            var rt = roomUpgradeModal.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bg = roomUpgradeModal.AddComponent<Image>();
            bg.color = new Color(0.01f, 0.02f, 0.04f, 0.85f);
        }
        else
        {
            roomUpgradeModal.transform.SetAsLastSibling();
        }

        // Clear existing children
        for (int i = roomUpgradeModal.transform.childCount - 1; i >= 0; i--)
        {
            Destroy(roomUpgradeModal.transform.GetChild(i).gameObject);
        }

        var theme = DungeonUITheme.Instance;

        // -------------------------------------------------------------
        // UNIFIED FRAMED WINDOW PANEL (Matching user sketch media_1794500423161_393aa551.jpg)
        // -------------------------------------------------------------
        var windowGo = new GameObject("WindowPanel", typeof(RectTransform));
        windowGo.transform.SetParent(roomUpgradeModal.transform, false);
        var winRt = windowGo.GetComponent<RectTransform>();
        winRt.anchorMin = new Vector2(0.5f, 0.5f);
        winRt.anchorMax = new Vector2(0.5f, 0.5f);
        winRt.pivot = new Vector2(0.5f, 0.5f);
        winRt.anchoredPosition = Vector2.zero;
        winRt.sizeDelta = new Vector2(960f, 550f);

        var winBg = windowGo.AddComponent<Image>();
        DungeonUITheme.ApplyPanel(winBg, theme != null ? theme.panelSlate : new Color(0.14f, 0.16f, 0.22f, 0.98f));

        // TOP HEADER BAR: Guider Icon (Left) + Title ("x x x x") + Gold ("GOLD XXX")
        var headerGo = new GameObject("HeaderBar", typeof(RectTransform));
        headerGo.transform.SetParent(windowGo.transform, false);
        var headerRt = headerGo.GetComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.anchoredPosition = new Vector2(0f, -10f);
        headerRt.sizeDelta = new Vector2(-20f, 84f);

        var headerBg = headerGo.AddComponent<Image>();
        DungeonUITheme.ApplyPanel(headerBg, theme != null ? theme.panelHeaderTint : new Color(0.09f, 0.11f, 0.16f, 0.98f));

        var badgeGo = new GameObject("GuiderBadge", typeof(RectTransform));
        badgeGo.transform.SetParent(headerGo.transform, false);
        var badgeRt = badgeGo.GetComponent<RectTransform>();
        badgeRt.anchorMin = new Vector2(0f, 0.5f);
        badgeRt.anchorMax = new Vector2(0f, 0.5f);
        badgeRt.pivot = new Vector2(0f, 0.5f);
        badgeRt.anchoredPosition = new Vector2(18f, 0f);
        badgeRt.sizeDelta = new Vector2(60f, 60f);

        var badgeBg = badgeGo.AddComponent<Image>();
        DungeonUITheme.ApplyCard(badgeBg, new Color(0.14f, 0.20f, 0.30f, 1f));

        var spriteGo = new GameObject("Avatar", typeof(RectTransform));
        spriteGo.transform.SetParent(badgeGo.transform, false);
        var sprRt = spriteGo.GetComponent<RectTransform>();
        sprRt.anchorMin = new Vector2(0.12f, 0.12f);
        sprRt.anchorMax = new Vector2(0.88f, 0.88f);
        sprRt.offsetMin = Vector2.zero;
        sprRt.offsetMax = Vector2.zero;
        var sprImg = spriteGo.AddComponent<Image>();
        Sprite kiraSpr = SpriteCache.LoadHeroOrBossSprite("doc") ?? SpriteCache.LoadHeroOrBossSprite("elf_f");
        if (kiraSpr != null)
        {
            sprImg.sprite = kiraSpr;
            sprImg.color = Color.white;
        }
        else
        {
            sprImg.color = theme != null ? theme.accentCyan : Color.cyan;
        }
        sprImg.preserveAspect = true;
        sprImg.raycastTarget = false;

        var titleTextGo = new GameObject("TitleText", typeof(RectTransform));
        titleTextGo.transform.SetParent(headerGo.transform, false);
        var titleRt = titleTextGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 0.5f);
        titleRt.anchorMax = new Vector2(1f, 0.5f);
        titleRt.pivot = new Vector2(0f, 0.5f);
        titleRt.anchoredPosition = new Vector2(92f, 0f);
        titleRt.sizeDelta = new Vector2(-300f, 64f);

        var titleTmp = titleTextGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "<color=#38BDF8><b>KIRA // TACTICAL ADVISOR</b></color>\n<size=21><color=#FCD34D><b>TACTICAL COMBAT SUPPLIES</b></color></size> <size=14><color=#CBD5E1>— Buy any supplies you can afford</color></size>";
        titleTmp.fontSize = 15;
        titleTmp.alignment = TextAlignmentOptions.Left;
        titleTmp.raycastTarget = false;

        var goldGo = new GameObject("GoldTag", typeof(RectTransform));
        goldGo.transform.SetParent(headerGo.transform, false);
        var goldRt = goldGo.GetComponent<RectTransform>();
        goldRt.anchorMin = new Vector2(1f, 0.5f);
        goldRt.anchorMax = new Vector2(1f, 0.5f);
        goldRt.pivot = new Vector2(1f, 0.5f);
        goldRt.anchoredPosition = new Vector2(-20f, 0f);
        goldRt.sizeDelta = new Vector2(210f, 44f);
        var goldTmp = goldGo.AddComponent<TextMeshProUGUI>();
        goldTmp.text = $"<b>GOLD: {GameRunData.Instance?.gold ?? 0}G</b>";
        goldTmp.fontSize = 18;
        goldTmp.fontStyle = FontStyles.Bold;
        goldTmp.color = new Color(1f, 0.85f, 0.2f);
        goldTmp.alignment = TextAlignmentOptions.Right;
        goldTmp.raycastTarget = false;

        // 3 Vertical Upgrade Option Cards (Cards layout matching user sketch)
        float cardWidth = 280f;
        float cardHeight = 335f;
        float spacing = 22f;
        float totalWidth = (cardWidth * 3) + (spacing * 2);
        float startX = -totalWidth * 0.5f + cardWidth * 0.5f;

        bool[] isSelected = new bool[options.Count];
        Image[] cardBgs = new Image[options.Count];
        Button[] cardBtns = new Button[options.Count];
        TextMeshProUGUI[] cardBtnLabels = new TextMeshProUGUI[options.Count];

        int GetSelectedTotalCost()
        {
            int sum = 0;
            for (int k = 0; k < options.Count && k < 3; k++)
            {
                if (isSelected[k]) sum += options[k].cost;
            }
            return sum;
        }

        TextMeshProUGUI proceedTmp = null;
        Button proceedBtnRef = null;

        void RefreshCardStates()
        {
            int currentG = GameRunData.Instance?.gold ?? 0;
            int totalSelected = GetSelectedTotalCost();
            int remainingG = currentG - totalSelected;

            if (goldTmp != null)
            {
                if (totalSelected > 0)
                    goldTmp.text = $"<b>GOLD: {currentG}G</b> <size=13><color=#F87171>(-{totalSelected}G)</color> -> <color=#34D399>{remainingG}G</color></size>";
                else
                    goldTmp.text = $"<b>GOLD: {currentG}G</b>";
            }

            for (int k = 0; k < options.Count && k < 3; k++)
            {
                if (cardBgs[k] == null || cardBtnLabels[k] == null || cardBtns[k] == null) continue;

                if (isSelected[k])
                {
                    cardBgs[k].color = new Color(0.28f, 0.42f, 0.54f, 1f);
                    cardBtnLabels[k].text = "<b>[ SELECTED ]</b>\n<size=12>Tap to Deselect</size>";
                    DungeonUITheme.ApplyButton(cardBtns[k], theme != null ? theme.btnPrimaryGreen : new Color(0.24f, 0.68f, 0.38f), true);
                }
                else
                {
                    cardBgs[k].color = theme != null ? theme.cardBaseTint : new Color(0.20f, 0.23f, 0.31f, 0.98f);
                    bool canAfford = remainingG >= options[k].cost;
                    string prefix = options[k].isInvestment ? "INVEST" : "SELECT";
                    cardBtnLabels[k].text = canAfford ? $"<b>{prefix} ({options[k].cost}G)</b>" : $"<b>LOCKED ({options[k].cost}G)</b>";
                    Color bTint = canAfford ? (theme != null ? theme.btnArcaneBlue : new Color(0.24f, 0.48f, 0.84f)) : (theme != null ? theme.btnDangerRed : new Color(0.76f, 0.24f, 0.26f));
                    DungeonUITheme.ApplyButton(cardBtns[k], bTint, false);
                }
            }

            if (proceedTmp != null && proceedBtnRef != null)
            {
                if (totalSelected > 0)
                {
                    proceedTmp.text = $"<b>ACTIVATE SELECTION ({totalSelected}G) & ADVANCE >></b>";
                    DungeonUITheme.ApplyButton(proceedBtnRef, theme != null ? theme.btnPrimaryGreen : new Color(0.24f, 0.68f, 0.38f), true);
                }
                else
                {
                    proceedTmp.text = "<b>PROCEED TO CORRIDOR (SAVE GOLD) >></b>";
                    DungeonUITheme.ApplyButton(proceedBtnRef, theme != null ? theme.btnArcaneBlue : new Color(0.24f, 0.48f, 0.84f), false);
                }
            }
        }

        for (int i = 0; i < options.Count && i < 3; i++)
        {
            int idx = i;
            var opt = options[i];

            var cardGo = new GameObject($"Card_{i}", typeof(RectTransform));
            cardGo.transform.SetParent(windowGo.transform, false);

            var cardRt = cardGo.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(cardWidth, cardHeight);
            cardRt.anchoredPosition = new Vector2(startX + i * (cardWidth + spacing), 10f);

            var cardImg = cardGo.AddComponent<Image>();
            DungeonUITheme.ApplyCard(cardImg, theme != null ? theme.cardBaseTint : null);
            cardBgs[idx] = cardImg;

            var cardTextGo = new GameObject("CardBody", typeof(RectTransform));
            cardTextGo.transform.SetParent(cardGo.transform, false);
            var cardTmp = cardTextGo.AddComponent<TextMeshProUGUI>();
            cardTmp.text = $"<size=32>{opt.icon}</size>\n\n<color=#FCD34D><b><size=18>{opt.title}</size></b></color>\n\n<size=14><color=#E2E8F0>{opt.description}</color></size>\n\n<color=#FBBF24><b>PRICE: {opt.cost} GOLD</b></color>";
            cardTmp.fontSize = 15;
            cardTmp.alignment = TextAlignmentOptions.Center;
            cardTmp.textWrappingMode = TextWrappingModes.Normal;
            cardTmp.overflowMode = TextOverflowModes.Overflow;
            cardTmp.raycastTarget = false;
            var textRt = cardTextGo.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.08f, 0.24f);
            textRt.anchorMax = new Vector2(0.92f, 0.92f);
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var btnGo = new GameObject("BuyBtn", typeof(RectTransform));
            btnGo.transform.SetParent(cardGo.transform, false);
            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.08f, 0.05f);
            btnRt.anchorMax = new Vector2(0.92f, 0.21f);
            btnRt.offsetMin = Vector2.zero;
            btnRt.offsetMax = Vector2.zero;

            btnGo.AddComponent<Image>();
            var btn = btnGo.AddComponent<Button>();
            DungeonUITheme.ApplyButton(btn, null, false);

            var btnTextGo = new GameObject("Label", typeof(RectTransform));
            btnTextGo.transform.SetParent(btnGo.transform, false);
            var btnTextRt = btnTextGo.GetComponent<RectTransform>();
            btnTextRt.anchorMin = Vector2.zero;
            btnTextRt.anchorMax = Vector2.one;
            btnTextRt.offsetMin = Vector2.zero;
            btnTextRt.offsetMax = Vector2.zero;

            var btnTmp = btnTextGo.AddComponent<TextMeshProUGUI>();
            btnTmp.fontSize = 15;
            btnTmp.fontStyle = FontStyles.Bold;
            btnTmp.alignment = TextAlignmentOptions.Center;
            btnTmp.color = Color.white;
            btnTmp.raycastTarget = false;

            cardBtns[idx] = btn;
            cardBtnLabels[idx] = btnTmp;

            var cardBtn = cardGo.AddComponent<Button>();

            void ToggleCardSelection()
            {
                if (isSelected[idx])
                {
                    isSelected[idx] = false;
                    RefreshCardStates();
                    return;
                }

                int currentG = GameRunData.Instance?.gold ?? 0;
                int currentTotal = GetSelectedTotalCost();
                if (currentTotal + opt.cost > currentG)
                {
                    int needed = (currentTotal + opt.cost) - currentG;
                    ShowCyberpunkDialogue("FIXER // KIRA", $"Not enough gold, Cleaner! Selecting this requires {opt.cost}G (Total: {currentTotal + opt.cost}G), but you only have {currentG}G.");
                    if (dialogueBoxRoot != null) dialogueBoxRoot.transform.SetAsLastSibling();
                    ShowScreenAnnouncement($"KIRA: 'Insufficient gold! Need +{needed}G more (You have {currentG}G)!'", Color.red);
                    CombatCameraController.Instance?.AddTrauma(0.18f);
                    return;
                }

                isSelected[idx] = true;
                RefreshCardStates();
            }

            btn.onClick.AddListener(ToggleCardSelection);
            cardBtn.onClick.AddListener(ToggleCardSelection);
        }

        var proceedGo = new GameObject("ProceedBtn", typeof(RectTransform));
        proceedGo.transform.SetParent(windowGo.transform, false);
        var proceedRt = proceedGo.GetComponent<RectTransform>();
        proceedRt.anchorMin = new Vector2(0.5f, 0f);
        proceedRt.anchorMax = new Vector2(0.5f, 0f);
        proceedRt.pivot = new Vector2(0.5f, 0.5f);
        proceedRt.anchoredPosition = new Vector2(0f, 44f);
        proceedRt.sizeDelta = new Vector2(620f, 54f);

        proceedGo.AddComponent<Image>();
        proceedBtnRef = proceedGo.AddComponent<Button>();
        DungeonUITheme.ApplyButton(proceedBtnRef, null, true);

        var proceedTextGo = new GameObject("ProceedLabel", typeof(RectTransform));
        proceedTextGo.transform.SetParent(proceedGo.transform, false);
        var proceedTextRt = proceedTextGo.GetComponent<RectTransform>();
        proceedTextRt.anchorMin = Vector2.zero;
        proceedTextRt.anchorMax = Vector2.one;
        proceedTextRt.offsetMin = Vector2.zero;
        proceedTextRt.offsetMax = Vector2.zero;

        proceedTmp = proceedTextGo.AddComponent<TextMeshProUGUI>();
        proceedTmp.text = "<b>PROCEED TO CORRIDOR (SAVE GOLD) >></b>";
        proceedTmp.fontSize = 15;
        proceedTmp.fontStyle = FontStyles.Bold;
        proceedTmp.alignment = TextAlignmentOptions.Center;
        proceedTmp.color = Color.white;
        proceedTmp.textWrappingMode = TextWrappingModes.NoWrap;
        proceedTmp.overflowMode = TextOverflowModes.Overflow;
        proceedTmp.raycastTarget = false;

        proceedBtnRef.onClick.AddListener(() =>
        {
            int totalSelected = GetSelectedTotalCost();
            int currentG = GameRunData.Instance?.gold ?? 0;

            if (totalSelected > currentG)
            {
                ShowCyberpunkDialogue("FIXER // KIRA", "You cannot confirm a purchase you cannot afford! Deselect items to continue.");
                return;
            }

            if (totalSelected > 0)
            {
                GameRunData.Instance?.SpendGold(totalSelected);
                RefreshGold();

                int activatedCount = 0;
                for (int k = 0; k < options.Count && k < 3; k++)
                {
                    if (isSelected[k])
                    {
                        options[k].onSelect?.Invoke();
                        activatedCount++;
                    }
                }
                ShowScreenAnnouncement($"CONFIRMED: {activatedCount} SUPPLIES ACTIVATED! ({GameRunData.Instance?.gold ?? 0}G left)", Color.yellow);
            }
            else
            {
                ShowScreenAnnouncement("GOLD CONSERVED FOR FUTURE COMBAT!", Color.white);
            }

            if (roomUpgradeModal != null)
                roomUpgradeModal.SetActive(false);

            onChosen?.Invoke();
        });

        RefreshCardStates();
    }

    // =========================================================================
    // LEVEL / BOSS CLEAR REWARD MODAL (Stylized window panel per user sketch)
    // =========================================================================
    private GameObject bossClearModal;

    IEnumerator PresentBossClearUpgradeChoice()
    {
        var pool = new List<RoomUpgradeOption>
        {
            new RoomUpgradeOption
            {
                title = "TITAN'S VIGOR",
                description = "+60 Max HP & Full Heal to all squad heroes",
                icon = "<color=#3B82F6><b>[ HP+ ]</b></color>",
                onSelect = () => {
                    foreach (var h in heroAgents)
                    {
                        h.heroInstance.data.maxHP += 60;
                        h.heroInstance.currentHP = h.heroInstance.data.maxHP;
                        h.heroInstance.upgrades.Add("Titan's Vigor (+60 Max HP)");
                    }
                    RefreshPortraits();
                }
            },
            new RoomUpgradeOption
            {
                title = "WARLORD'S FURY",
                description = "+40% Squad ATK Damage & +15% Critical Strike",
                icon = "<color=#EF4444><b>[ ATK+ ]</b></color>",
                onSelect = () => {
                    foreach (var h in heroAgents)
                    {
                        h.heroInstance.data.attackDamage = Mathf.RoundToInt(h.heroInstance.data.attackDamage * 1.4f);
                        h.heroInstance.upgrades.Add("Warlord's Fury (+40% ATK)");
                    }
                    RefreshPortraits();
                }
            },
            new RoomUpgradeOption
            {
                title = "ARCANE SURGE",
                description = "+50 Max Energy & +30% Skill Cooldown Recovery",
                icon = "<color=#8B5CF6><b>[ NRG+ ]</b></color>",
                onSelect = () => {
                    foreach (var h in heroAgents)
                    {
                        h.heroInstance.data.maxEnergy += 50;
                        h.heroInstance.currentEnergy = h.heroInstance.data.maxEnergy;
                        h.heroInstance.upgrades.Add("Arcane Surge (+50 Energy)");
                    }
                    RefreshPortraits();
                }
            },
            new RoomUpgradeOption
            {
                title = "ROYAL TREASURY",
                description = "+150 Gold immediately for mercenary hiring",
                icon = "<color=#FCD34D><b>[ GOLD+ ]</b></color>",
                onSelect = () => {
                    if (GameRunData.Instance != null) GameRunData.Instance.AddGold(150);
                    RefreshGold();
                }
            }
        };

        List<RoomUpgradeOption> chosenThree = null;
        void PickThree()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                int rIdx = Random.Range(i, pool.Count);
                var tmp = pool[i];
                pool[i] = pool[rIdx];
                pool[rIdx] = tmp;
            }
            chosenThree = pool.GetRange(0, Mathf.Min(3, pool.Count));
        }

        PickThree();
        bool choiceMade = false;
        upgradeSelectedCardIdx = 0;

        void BuildModal()
        {
            EnsureBossClearModal(chosenThree,
                () => choiceMade = true,
                () => {
                    PickThree();
                    upgradeSelectedCardIdx = 0;
                    BuildModal();
                });
        }

        BuildModal();
        if (bossClearModal) bossClearModal.SetActive(true);

        while (!choiceMade)
        {
            yield return null;
        }

        if (bossClearModal) bossClearModal.SetActive(false);
        yield return new WaitForSeconds(0.2f);
    }

    void EnsureBossClearModal(List<RoomUpgradeOption> options, System.Action onChosen, System.Action onReroll = null)
    {
        var canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        if (screenAnnouncementBannerRoot != null)
        {
            screenAnnouncementBannerRoot.SetActive(false);
            if (screenAnnouncementCoroutine != null) StopCoroutine(screenAnnouncementCoroutine);
        }
        if (bossAlertBannerRoot != null)
        {
            bossAlertBannerRoot.SetActive(false);
            if (bossAlertCoroutine != null) StopCoroutine(bossAlertCoroutine);
        }

        if (bossClearModal == null)
        {
            bossClearModal = new GameObject("BossClearModal", typeof(RectTransform));
            bossClearModal.transform.SetParent(canvas.transform, false);

            var rt = bossClearModal.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bg = bossClearModal.AddComponent<Image>();
            bg.color = new Color(0.01f, 0.02f, 0.04f, 0.85f);
        }

        for (int i = bossClearModal.transform.childCount - 1; i >= 0; i--)
        {
            Destroy(bossClearModal.transform.GetChild(i).gameObject);
        }

        // HUGE FRAMED WINDOW PANEL (Matching user sketch)
        var windowGo = new GameObject("WindowPanel", typeof(RectTransform));
        windowGo.transform.SetParent(bossClearModal.transform, false);
        var winRt = windowGo.GetComponent<RectTransform>();
        winRt.anchorMin = new Vector2(0.5f, 0.5f);
        winRt.anchorMax = new Vector2(0.5f, 0.5f);
        winRt.pivot = new Vector2(0.5f, 0.5f);
        winRt.anchoredPosition = Vector2.zero;
        winRt.sizeDelta = new Vector2(920f, 520f);

        var theme = DungeonUITheme.Instance;

        var winBg = windowGo.AddComponent<Image>();
        DungeonUITheme.ApplyPanel(winBg, theme != null ? theme.panelSlate : new Color(0.14f, 0.16f, 0.22f, 0.98f));

        // TOP HEADER BAR: Guider Icon (Left) + Titles (Center) + Defeated Boss Trophy (Right) + Gold (Far Right)
        var headerGo = new GameObject("HeaderBar", typeof(RectTransform));
        headerGo.transform.SetParent(windowGo.transform, false);
        var headerRt = headerGo.GetComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.anchoredPosition = Vector2.zero;
        headerRt.sizeDelta = new Vector2(0f, 82f);

        var headerBg = headerGo.AddComponent<Image>();
        DungeonUITheme.ApplyPanel(headerBg, theme != null ? theme.panelHeaderTint : new Color(0.09f, 0.11f, 0.16f, 0.98f));

        // 1. Mini Guider Badge (Kira)
        var badgeGo = new GameObject("GuiderBadge", typeof(RectTransform));
        badgeGo.transform.SetParent(headerGo.transform, false);
        var badgeRt = badgeGo.GetComponent<RectTransform>();
        badgeRt.anchorMin = new Vector2(0f, 0.5f);
        badgeRt.anchorMax = new Vector2(0f, 0.5f);
        badgeRt.pivot = new Vector2(0f, 0.5f);
        badgeRt.anchoredPosition = new Vector2(20f, 0f);
        badgeRt.sizeDelta = new Vector2(56f, 56f);

        var badgeBg = badgeGo.AddComponent<Image>();
        DungeonUITheme.ApplyCard(badgeBg, new Color(0.14f, 0.20f, 0.30f, 1f));

        var spriteGo = new GameObject("Avatar", typeof(RectTransform));
        spriteGo.transform.SetParent(badgeGo.transform, false);
        var sprRt = spriteGo.GetComponent<RectTransform>();
        sprRt.anchorMin = new Vector2(0.1f, 0.1f);
        sprRt.anchorMax = new Vector2(0.9f, 0.9f);
        sprRt.offsetMin = Vector2.zero;
        sprRt.offsetMax = Vector2.zero;
        var sprImg = spriteGo.AddComponent<Image>();
        sprImg.sprite = SpriteCache.LoadSprite("doc_idle_anim_f0") ?? SpriteCache.LoadSprite("elf_f_idle_anim_f0");
        sprImg.preserveAspect = true;
        sprImg.raycastTarget = false;

        // 2. Header Title Text (XXXXX in user sketch)
        var titleTextGo = new GameObject("TitleText", typeof(RectTransform));
        titleTextGo.transform.SetParent(headerGo.transform, false);
        var titleRt = titleTextGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 0.5f);
        titleRt.anchorMax = new Vector2(1f, 0.5f);
        titleRt.pivot = new Vector2(0f, 0.5f);
        titleRt.anchoredPosition = new Vector2(90f, 0f);
        titleRt.sizeDelta = new Vector2(-330f, 60f);

        var titleTmp = titleTextGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "<color=#38BDF8><b>KIRA // TACTICAL ADVISOR</b></color>\n<size=20><color=#FCD34D><b>LEVEL CLEARED! CHOOSE LEVEL UPGRADE</b></color></size> <size=13><color=#94A3B8>— Boss Defeated</color></size>";
        titleTmp.fontSize = 13;
        titleTmp.alignment = TextAlignmentOptions.Left;
        titleTmp.raycastTarget = false;

        // 2b. Defeated Boss Trophy Picture Badge
        var bossBadgeGo = new GameObject("BossTrophyBadge", typeof(RectTransform));
        bossBadgeGo.transform.SetParent(headerGo.transform, false);
        var bossBadgeRt = bossBadgeGo.GetComponent<RectTransform>();
        bossBadgeRt.anchorMin = new Vector2(1f, 0.5f);
        bossBadgeRt.anchorMax = new Vector2(1f, 0.5f);
        bossBadgeRt.pivot = new Vector2(1f, 0.5f);
        bossBadgeRt.anchoredPosition = new Vector2(-165f, 0f);
        bossBadgeRt.sizeDelta = new Vector2(56f, 56f);

        var bossBadgeBg = bossBadgeGo.AddComponent<Image>();
        DungeonUITheme.ApplyCard(bossBadgeBg, new Color(0.18f, 0.04f, 0.04f, 1f));

        var bossSprGo = new GameObject("BossAvatar", typeof(RectTransform));
        bossSprGo.transform.SetParent(bossBadgeGo.transform, false);
        var bSprRt = bossSprGo.GetComponent<RectTransform>();
        bSprRt.anchorMin = new Vector2(0.1f, 0.1f);
        bSprRt.anchorMax = new Vector2(0.9f, 0.9f);
        bSprRt.offsetMin = Vector2.zero;
        bSprRt.offsetMax = Vector2.zero;
        var bSprImg = bossSprGo.AddComponent<Image>();
        bSprImg.sprite = SpriteCache.LoadHeroOrBossSprite("big_demon") ?? SpriteCache.LoadSprite("big_demon_idle_anim_f0") ?? SpriteCache.LoadHeroOrBossSprite("skelet");
        bSprImg.preserveAspect = true;
        bSprImg.raycastTarget = false;

        var slainGo = new GameObject("SlainTag", typeof(RectTransform));
        slainGo.transform.SetParent(bossBadgeGo.transform, false);
        var slainRt = slainGo.GetComponent<RectTransform>();
        slainRt.anchorMin = new Vector2(0f, 0f);
        slainRt.anchorMax = new Vector2(1f, 0.35f);
        slainRt.offsetMin = Vector2.zero;
        slainRt.offsetMax = Vector2.zero;
        var slainTmp = slainGo.AddComponent<TextMeshProUGUI>();
        slainTmp.text = "<b><color=#EF4444>SLAIN</color></b>";
        slainTmp.fontSize = 9;
        slainTmp.alignment = TextAlignmentOptions.Center;
        slainTmp.raycastTarget = false;

        // 3. Gold Display
        int currentGold = GameRunData.Instance?.gold ?? 0;
        var goldGo = new GameObject("GoldTag", typeof(RectTransform));
        goldGo.transform.SetParent(headerGo.transform, false);
        var goldRt = goldGo.GetComponent<RectTransform>();
        goldRt.anchorMin = new Vector2(1f, 0.5f);
        goldRt.anchorMax = new Vector2(1f, 0.5f);
        goldRt.pivot = new Vector2(1f, 0.5f);
        goldRt.anchoredPosition = new Vector2(-24f, 0f);
        goldRt.sizeDelta = new Vector2(130f, 40f);
        var goldTmp = goldGo.AddComponent<TextMeshProUGUI>();
        goldTmp.text = $"<b>GOLD: {currentGold}G</b>";
        goldTmp.fontSize = 15;
        goldTmp.fontStyle = FontStyles.Bold;
        goldTmp.color = new Color(1f, 0.85f, 0.2f);
        goldTmp.alignment = TextAlignmentOptions.Right;
        goldTmp.raycastTarget = false;

        // 3 Cards Layout inside Window
        float cardWidth = 265f;
        float cardHeight = 310f;
        float spacing = 20f;
        float totalWidth = (cardWidth * 3) + (spacing * 2);
        float startX = -totalWidth * 0.5f + cardWidth * 0.5f;

        List<Image> cardBgs = new List<Image>();
        List<Outline> cardOutlines = new List<Outline>();
        List<TextMeshProUGUI> cardBadges = new List<TextMeshProUGUI>();

        for (int i = 0; i < options.Count && i < 3; i++)
        {
            int idx = i;
            var opt = options[i];
            bool isSelected = (idx == upgradeSelectedCardIdx);

            var cardGo = new GameObject($"Card_{i}", typeof(RectTransform));
            cardGo.transform.SetParent(windowGo.transform, false);

            var cardRt = cardGo.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(cardWidth, cardHeight);
            cardRt.anchoredPosition = new Vector2(startX + i * (cardWidth + spacing), 15f);

            var cardImg = cardGo.AddComponent<Image>();
            DungeonUITheme.ApplyCard(cardImg, isSelected ? new Color(0.24f, 0.38f, 0.54f, 1f) : null);
            cardBgs.Add(cardImg);

            var cardTextGo = new GameObject("CardBody", typeof(RectTransform));
            cardTextGo.transform.SetParent(cardGo.transform, false);
            var cardTmp = cardTextGo.AddComponent<TextMeshProUGUI>();
            cardTmp.text = $"<size=36>{opt.icon}</size>\n\n<color=#FCD34D><b><size=18>{opt.title}</size></b></color>\n\n<size=14><color=#E2E8F0>{opt.description}</color></size>";
            cardTmp.fontSize = 15;
            cardTmp.alignment = TextAlignmentOptions.Center;
            cardTmp.textWrappingMode = TextWrappingModes.Normal;
            cardTmp.overflowMode = TextOverflowModes.Overflow;
            var textRt = cardTextGo.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.08f, 0.22f);
            textRt.anchorMax = new Vector2(0.92f, 0.92f);
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var badgeTextGo = new GameObject("SelectBadge", typeof(RectTransform));
            badgeTextGo.transform.SetParent(cardGo.transform, false);
            var badgeTextRt = badgeTextGo.GetComponent<RectTransform>();
            badgeTextRt.anchorMin = new Vector2(0.08f, 0.05f);
            badgeTextRt.anchorMax = new Vector2(0.92f, 0.19f);
            badgeTextRt.offsetMin = Vector2.zero;
            badgeTextRt.offsetMax = Vector2.zero;
            var badgeTmp = badgeTextGo.AddComponent<TextMeshProUGUI>();
            badgeTmp.text = isSelected ? "<b><color=#38BDF8>[ SELECTED ]</color></b>" : "<color=#64748B>[ TAP TO SELECT ]</color>";
            badgeTmp.fontSize = 13;
            badgeTmp.alignment = TextAlignmentOptions.Center;
            cardBadges.Add(badgeTmp);

            var cardBtn = cardGo.AddComponent<Button>();
            cardBtn.onClick.AddListener(() =>
            {
                upgradeSelectedCardIdx = idx;
                for (int k = 0; k < cardBgs.Count; k++)
                {
                    bool sel = (k == upgradeSelectedCardIdx);
                    cardBgs[k].color = sel ? new Color(0.24f, 0.38f, 0.54f, 1f) : (theme != null ? theme.cardBaseTint : new Color(0.20f, 0.23f, 0.31f, 0.98f));
                    cardBadges[k].text = sel ? "<b><color=#38BDF8>[ SELECTED ]</color></b>" : "<color=#64748B>[ TAP TO SELECT ]</color>";
                }
            });
        }

        // Bottom Action Row: Confirm Button + Reroll Button (Right Side)
        var confirmGo = new GameObject("ConfirmBtn", typeof(RectTransform));
        confirmGo.transform.SetParent(windowGo.transform, false);
        var confirmRt = confirmGo.GetComponent<RectTransform>();
        confirmRt.anchorMin = new Vector2(0.5f, 0f);
        confirmRt.anchorMax = new Vector2(0.5f, 0f);
        confirmRt.pivot = new Vector2(0.5f, 0.5f);
        confirmRt.anchoredPosition = new Vector2(-60f, 38f);
        confirmRt.sizeDelta = new Vector2(260f, 48f);

        confirmGo.AddComponent<Image>();
        var confirmBtn = confirmGo.AddComponent<Button>();
        DungeonUITheme.ApplyButton(confirmBtn, theme != null ? theme.btnPrimaryGreen : new Color(0.24f, 0.68f, 0.38f), true);

        confirmBtn.onClick.AddListener(() =>
        {
            if (upgradeSelectedCardIdx >= 0 && upgradeSelectedCardIdx < options.Count)
            {
                options[upgradeSelectedCardIdx].onSelect?.Invoke();
                ShowScreenAnnouncement($"UPGRADE: {options[upgradeSelectedCardIdx].title}!", Color.yellow);
            }
            if (bossClearModal != null)
                bossClearModal.SetActive(false);

            onChosen?.Invoke();
        });

        var confirmTextGo = new GameObject("ConfirmLabel", typeof(RectTransform));
        confirmTextGo.transform.SetParent(confirmGo.transform, false);
        var confirmTextRt = confirmTextGo.GetComponent<RectTransform>();
        confirmTextRt.anchorMin = Vector2.zero;
        confirmTextRt.anchorMax = Vector2.one;
        confirmTextRt.offsetMin = Vector2.zero;
        confirmTextRt.offsetMax = Vector2.zero;
        var confirmTmp = confirmTextGo.AddComponent<TextMeshProUGUI>();
        confirmTmp.text = "<b>CONFIRM UPGRADE</b>";
        confirmTmp.fontSize = 15;
        confirmTmp.fontStyle = FontStyles.Bold;
        confirmTmp.alignment = TextAlignmentOptions.Center;
        confirmTmp.color = Color.white;
        confirmTmp.raycastTarget = false;

        const int REROLL_COST = 25;
        var rerollGo = new GameObject("RerollBtn", typeof(RectTransform));
        rerollGo.transform.SetParent(windowGo.transform, false);
        var rerollRt = rerollGo.GetComponent<RectTransform>();
        rerollRt.anchorMin = new Vector2(0.5f, 0f);
        rerollRt.anchorMax = new Vector2(0.5f, 0f);
        rerollRt.pivot = new Vector2(0.5f, 0.5f);
        rerollRt.anchoredPosition = new Vector2(260f, 38f);
        rerollRt.sizeDelta = new Vector2(200f, 48f);

        rerollGo.AddComponent<Image>();
        var rerollBtn = rerollGo.AddComponent<Button>();
        DungeonUITheme.ApplyButton(rerollBtn, theme != null ? theme.btnArcaneBlue : new Color(0.24f, 0.48f, 0.84f), false);

        rerollBtn.onClick.AddListener(() =>
        {
            if (GameRunData.Instance != null && GameRunData.Instance.CanAfford(REROLL_COST))
            {
                GameRunData.Instance.SpendGold(REROLL_COST);
                RefreshGold();
                onReroll?.Invoke();
            }
            else
            {
                ShowScreenAnnouncement($"Need {REROLL_COST} Gold to Reroll!", Color.red);
            }
        });

        var rerollTextGo = new GameObject("RerollLabel", typeof(RectTransform));
        rerollTextGo.transform.SetParent(rerollGo.transform, false);
        var rerollTextRt = rerollTextGo.GetComponent<RectTransform>();
        rerollTextRt.anchorMin = Vector2.zero;
        rerollTextRt.anchorMax = Vector2.one;
        rerollTextRt.offsetMin = Vector2.zero;
        rerollTextRt.offsetMax = Vector2.zero;
        var rerollTmp = rerollTextGo.AddComponent<TextMeshProUGUI>();
        rerollTmp.text = $"REROLL ({REROLL_COST}g)";
        rerollTmp.fontSize = 15;
        rerollTmp.fontStyle = FontStyles.Bold;
        rerollTmp.alignment = TextAlignmentOptions.Center;
        rerollTmp.color = Color.white;
        rerollTmp.raycastTarget = false;
    }

    // =========================================================================
    // REPOSITION ("REPO") MODAL
    // =========================================================================
    void SetupRepoButton()
    {
        try
        {
            if (repoButton == null)
            {
                var repoGo = GameObject.Find("RepoBtn") ?? GameObject.Find("RepoButton");
                if (repoGo != null)
                {
                    repoButton = repoGo.GetComponent<Button>();
                }
            }

            if (repoButton != null)
            {
                repoButton.gameObject.SetActive(true);
                var rt = repoButton.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(1f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.anchoredPosition = new Vector2(-215f, -45f);
                    rt.sizeDelta = new Vector2(65f, 45f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                }

                repoButton.onClick.RemoveAllListeners();
                repoButton.onClick.AddListener(OpenRepositionModal);

                // Disable any stray duplicate or non-working repo button in scene
                var allButtons = FindObjectsByType<Button>(FindObjectsInactive.Include);
                foreach (var b in allButtons)
                {
                    if (b != null && b != repoButton && (b.name.Equals("RepoBtn", System.StringComparison.OrdinalIgnoreCase) || b.name.Equals("RepoButton", System.StringComparison.OrdinalIgnoreCase)))
                    {
                        b.gameObject.SetActive(false);
                    }
                }
                return;
            }

            // Fallback: If repoButton is not found in scene, create one safely on the HUD
            Transform hudParent = pauseButton != null ? pauseButton.transform.parent : null;
            if (hudParent == null)
            {
                var canvas = FindAnyObjectByType<Canvas>();
                if (canvas != null) hudParent = canvas.transform;
            }

            if (hudParent != null)
            {
                var go = new GameObject("RepoBtn", typeof(RectTransform));
                go.transform.SetParent(hudParent, false);

                var rt = go.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(1f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.anchoredPosition = new Vector2(-215f, -45f);
                    rt.sizeDelta = new Vector2(65f, 45f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                }

                var img = go.AddComponent<Image>();
                img.color = new Color(0.12f, 0.35f, 0.75f, 0.95f);

                var outline = go.AddComponent<Outline>();
                outline.effectColor = new Color(0.38f, 0.72f, 1f, 0.9f);
                outline.effectDistance = new Vector2(1.5f, 1.5f);

                repoButton = go.AddComponent<Button>();

                var textGo = new GameObject("Label", typeof(RectTransform));
                textGo.transform.SetParent(go.transform, false);
                var textRt = textGo.GetComponent<RectTransform>();
                if (textRt != null)
                {
                    textRt.anchorMin = Vector2.zero;
                    textRt.anchorMax = Vector2.one;
                    textRt.sizeDelta = Vector2.zero;
                }

                var tmp = textGo.AddComponent<TextMeshProUGUI>();
                tmp.text = "<b>REPO</b>";
                tmp.fontSize = 15;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                tmp.raycastTarget = false;

                repoButton.onClick.RemoveAllListeners();
                repoButton.onClick.AddListener(OpenRepositionModal);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[DungeonCombatController] SetupRepoButton warning: {ex.Message}");
        }
    }

    public void OpenRepositionModal()
    {
        isPaused = true;
        Time.timeScale = 0f;

        if (repoModal == null)
        {
            BuildRepoModal();
        }

        // Copy current formation
        if (GameRunData.Instance != null)
        {
            repoFormation = (HeroInstance[])GameRunData.Instance.formation.Clone();
        }

        repoSelectedSlot = -1;
        if (repoStatsPanel != null) repoStatsPanel.SetActive(false);
        repoModal.SetActive(true);
        RefreshRepoGrid();
    }

    void BuildRepoModal()
    {
        var canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        repoModal = new GameObject("RepoModal", typeof(RectTransform));
        repoModal.transform.SetParent(canvas.transform, false);
        repoModal.transform.SetAsLastSibling();

        var rt = repoModal.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var bg = repoModal.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.05f, 0.08f, 0.92f);

        // Header Title
        var titleGo = new GameObject("RepoTitle", typeof(RectTransform));
        titleGo.transform.SetParent(repoModal.transform, false);
        var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "TACTICAL REPOSITION\n<size=15><color=#94A3B8>Tap two slots to swap hero positions in current room</color></size>";
        titleTmp.fontSize = 24;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.raycastTarget = false;
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.1f, 0.88f);
        titleRt.anchorMax = new Vector2(0.9f, 0.98f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;

        // 3x3 Grid Container (Center-Left)
        var gridRoot = new GameObject("Repo3x3Grid", typeof(RectTransform));
        gridRoot.transform.SetParent(repoModal.transform, false);
        var gridRt = gridRoot.GetComponent<RectTransform>();
        gridRt.anchorMin = new Vector2(0.5f, 0.5f);
        gridRt.anchorMax = new Vector2(0.5f, 0.5f);
        gridRt.anchoredPosition = new Vector2(-120f, -20f);
        gridRt.sizeDelta = new Vector2(480f, 480f);

        var gridBg = gridRoot.AddComponent<Image>();
        gridBg.color = new Color(0.08f, 0.1f, 0.14f, 0.9f);

        var gridLayout = gridRoot.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(140f, 140f);
        gridLayout.spacing = new Vector2(14f, 14f);
        gridLayout.childAlignment = TextAnchor.MiddleCenter;

        // Build 9 Grid Slots
        for (int i = 0; i < 9; i++)
        {
            int slotIdx = i;
            var slotGo = new GameObject($"RepoSlot_{i}", typeof(RectTransform));
            slotGo.transform.SetParent(gridRoot.transform, false);

            var slotImg = slotGo.AddComponent<Image>();
            slotImg.color = GridSlotUI.EmptyColor;

            var slotUI = slotGo.AddComponent<GridSlotUI>();
            slotUI.background = slotImg;

            var labelGo = new GameObject("HeroLabel", typeof(RectTransform));
            labelGo.transform.SetParent(slotGo.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0.05f, 0.45f);
            labelRt.anchorMax = new Vector2(0.95f, 0.95f);
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
            labelTmp.fontSize = 15;
            labelTmp.fontStyle = FontStyles.Bold;
            labelTmp.alignment = TextAlignmentOptions.Center;
            labelTmp.raycastTarget = false;
            slotUI.heroLabel = labelTmp;

            var hpGo = new GameObject("HPLabel", typeof(RectTransform));
            hpGo.transform.SetParent(slotGo.transform, false);
            var hpRt = hpGo.GetComponent<RectTransform>();
            hpRt.anchorMin = new Vector2(0.05f, 0.1f);
            hpRt.anchorMax = new Vector2(0.95f, 0.45f);
            hpRt.offsetMin = Vector2.zero;
            hpRt.offsetMax = Vector2.zero;

            var hpTmp = hpGo.AddComponent<TextMeshProUGUI>();
            hpTmp.fontSize = 13;
            hpTmp.alignment = TextAlignmentOptions.Center;
            hpTmp.raycastTarget = false;
            slotUI.hpLabel = hpTmp;

            slotUI.Setup(slotIdx, null, OnRepoSlotTapped, OnRepoSlotDrop);
            repoGridSlots[i] = slotUI;
        }

        // Right-Side Hero Details Window Option
        repoStatsPanel = new GameObject("RepoStatsPanel", typeof(RectTransform));
        repoStatsPanel.transform.SetParent(repoModal.transform, false);
        var statsRt = repoStatsPanel.GetComponent<RectTransform>();
        statsRt.anchorMin = new Vector2(0.72f, 0.22f);
        statsRt.anchorMax = new Vector2(0.96f, 0.85f);
        statsRt.offsetMin = Vector2.zero;
        statsRt.offsetMax = Vector2.zero;

        var statsBg = repoStatsPanel.AddComponent<Image>();
        statsBg.color = new Color(0.12f, 0.14f, 0.22f, 0.95f);

        var statsHeader = new GameObject("Header", typeof(RectTransform));
        statsHeader.transform.SetParent(repoStatsPanel.transform, false);
        var shTmp = statsHeader.AddComponent<TextMeshProUGUI>();
        shTmp.text = "HERO DOSSIER";
        shTmp.fontSize = 18;
        shTmp.fontStyle = FontStyles.Bold;
        shTmp.alignment = TextAlignmentOptions.Center;
        shTmp.color = new Color(1f, 0.85f, 0.2f);
        shTmp.raycastTarget = false;
        var shRt = statsHeader.GetComponent<RectTransform>();
        shRt.anchorMin = new Vector2(0f, 1f);
        shRt.anchorMax = new Vector2(1f, 1f);
        shRt.anchoredPosition = new Vector2(0f, -25f);
        shRt.sizeDelta = new Vector2(0f, 30f);

        repoStatsName = CreateRepoStatText(repoStatsPanel.transform, "Hero Name", -65f, 16, FontStyles.Bold);
        repoStatsClass = CreateRepoStatText(repoStatsPanel.transform, "Class: -", -95f, 14, FontStyles.Normal);
        repoStatsHP = CreateRepoStatText(repoStatsPanel.transform, "HP: -", -125f, 14, FontStyles.Normal);
        repoStatsEnergy = CreateRepoStatText(repoStatsPanel.transform, "Energy: -", -155f, 14, FontStyles.Normal);
        repoStatsATK = CreateRepoStatText(repoStatsPanel.transform, "ATK: -", -185f, 14, FontStyles.Normal);

        // Unassign Button inside right-side panel
        var unassignGo = new GameObject("UnassignBtn", typeof(RectTransform));
        unassignGo.transform.SetParent(repoStatsPanel.transform, false);
        var unassignRt = unassignGo.GetComponent<RectTransform>();
        unassignRt.anchorMin = new Vector2(0.1f, 0f);
        unassignRt.anchorMax = new Vector2(0.9f, 0f);
        unassignRt.anchoredPosition = new Vector2(0f, 35f);
        unassignRt.sizeDelta = new Vector2(0f, 40f);
        var unassignImg = unassignGo.AddComponent<Image>();
        unassignImg.color = new Color(0.6f, 0.2f, 0.2f, 0.9f);
        var unassignBtn = unassignGo.AddComponent<Button>();
        unassignBtn.onClick.AddListener(UnassignRepoSelected);

        var uLabel = new GameObject("Label", typeof(RectTransform));
        uLabel.transform.SetParent(unassignGo.transform, false);
        var uLabelRt = uLabel.GetComponent<RectTransform>();
        uLabelRt.anchorMin = Vector2.zero;
        uLabelRt.anchorMax = Vector2.one;
        uLabelRt.sizeDelta = Vector2.zero;
        var uTmp = uLabel.AddComponent<TextMeshProUGUI>();
        uTmp.text = "CLEAR SLOT";
        uTmp.fontSize = 14;
        uTmp.fontStyle = FontStyles.Bold;
        uTmp.alignment = TextAlignmentOptions.Center;
        uTmp.raycastTarget = false;

        // Bottom Action Buttons
        var confirmGo = new GameObject("ConfirmBtn", typeof(RectTransform));
        confirmGo.transform.SetParent(repoModal.transform, false);
        var confirmRt = confirmGo.GetComponent<RectTransform>();
        confirmRt.anchorMin = new Vector2(0.2f, 0f);
        confirmRt.anchorMax = new Vector2(0.45f, 0f);
        confirmRt.anchoredPosition = new Vector2(0f, 45f);
        confirmRt.sizeDelta = new Vector2(0f, 50f);
        var cImg = confirmGo.AddComponent<Image>();
        cImg.color = new Color(0.18f, 0.55f, 0.28f);
        var confirmBtn = confirmGo.AddComponent<Button>();
        confirmBtn.onClick.AddListener(ConfirmReposition);

        var cLabel = new GameObject("Label", typeof(RectTransform));
        cLabel.transform.SetParent(confirmGo.transform, false);
        var cLabelRt = cLabel.GetComponent<RectTransform>();
        cLabelRt.anchorMin = Vector2.zero;
        cLabelRt.anchorMax = Vector2.one;
        cLabelRt.sizeDelta = Vector2.zero;
        var cTmp = cLabel.AddComponent<TextMeshProUGUI>();
        cTmp.text = "APPLY FORMATION";
        cTmp.fontSize = 18;
        cTmp.fontStyle = FontStyles.Bold;
        cTmp.alignment = TextAlignmentOptions.Center;
        cTmp.raycastTarget = false;

        var cancelGo = new GameObject("CancelBtn", typeof(RectTransform));
        cancelGo.transform.SetParent(repoModal.transform, false);
        var cancelRt = cancelGo.GetComponent<RectTransform>();
        cancelRt.anchorMin = new Vector2(0.52f, 0f);
        cancelRt.anchorMax = new Vector2(0.72f, 0f);
        cancelRt.anchoredPosition = new Vector2(0f, 45f);
        cancelRt.sizeDelta = new Vector2(0f, 50f);
        var canImg = cancelGo.AddComponent<Image>();
        canImg.color = new Color(0.4f, 0.42f, 0.5f);
        var cancelBtn = cancelGo.AddComponent<Button>();
        cancelBtn.onClick.AddListener(CloseRepositionModal);

        var canLabel = new GameObject("Label", typeof(RectTransform));
        canLabel.transform.SetParent(cancelGo.transform, false);
        var canLabelRt = canLabel.GetComponent<RectTransform>();
        canLabelRt.anchorMin = Vector2.zero;
        canLabelRt.anchorMax = Vector2.one;
        canLabelRt.sizeDelta = Vector2.zero;
        var canTmp = canLabel.AddComponent<TextMeshProUGUI>();
        canTmp.text = "CANCEL";
        canTmp.fontSize = 18;
        canTmp.fontStyle = FontStyles.Bold;
        canTmp.alignment = TextAlignmentOptions.Center;
        canTmp.raycastTarget = false;
    }

    TextMeshProUGUI CreateRepoStatText(Transform parent, string defaultText, float posY, float fontSize, FontStyles style)
    {
        var go = new GameObject("Stat", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = defaultText;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(18f, posY);
        rt.sizeDelta = new Vector2(-36f, 25f);
        return tmp;
    }

    void RefreshRepoGrid()
    {
        for (int i = 0; i < 9; i++)
        {
            if (repoGridSlots[i] != null)
            {
                repoGridSlots[i].Setup(i, repoFormation[i], OnRepoSlotTapped, OnRepoSlotDrop);
            }
        }
    }

    void OnRepoSlotTapped(int slotIndex)
    {
        if (repoSelectedSlot == -1)
        {
            if (repoFormation[slotIndex] != null)
            {
                repoSelectedSlot = slotIndex;
                repoGridSlots[slotIndex].SetSelected(true);
                ShowRepoStats(repoFormation[slotIndex]);
            }
            else
            {
                if (repoStatsPanel != null) repoStatsPanel.SetActive(false);
            }
        }
        else if (repoSelectedSlot == slotIndex)
        {
            ClearRepoSelection();
            if (repoStatsPanel != null) repoStatsPanel.SetActive(false);
        }
        else
        {
            SwapRepoSlots(repoSelectedSlot, slotIndex);
            ClearRepoSelection();
        }
    }

    void OnRepoSlotDrop(int fromSlot, int toSlot)
    {
        SwapRepoSlots(fromSlot, toSlot);
        ClearRepoSelection();
    }

    void SwapRepoSlots(int from, int to)
    {
        var tmp = repoFormation[to];
        repoFormation[to] = repoFormation[from];
        repoFormation[from] = tmp;

        if (repoFormation[from] != null) repoFormation[from].gridIndex = from;
        if (repoFormation[to] != null) repoFormation[to].gridIndex = to;

        RefreshRepoGrid();
    }

    void ClearRepoSelection()
    {
        if (repoSelectedSlot >= 0 && repoSelectedSlot < repoGridSlots.Length)
        {
            if (repoGridSlots[repoSelectedSlot] != null)
            {
                repoGridSlots[repoSelectedSlot].SetSelected(false);
            }
        }
        repoSelectedSlot = -1;
    }

    void ShowRepoStats(HeroInstance hero)
    {
        if (repoStatsPanel != null) repoStatsPanel.SetActive(true);
        if (repoStatsName) repoStatsName.text = hero.displayName;
        if (repoStatsClass) repoStatsClass.text = $"Class: {hero.data.heroClass}";
        if (repoStatsHP) repoStatsHP.text = $"HP: {hero.currentHP}/{hero.data.maxHP}";
        if (repoStatsEnergy) repoStatsEnergy.text = $"Energy: {hero.currentEnergy}/{hero.data.maxEnergy}";
        if (repoStatsATK) repoStatsATK.text = $"ATK: {hero.data.attackDamage}";
    }

    void UnassignRepoSelected()
    {
        if (repoSelectedSlot < 0 || repoSelectedSlot >= 9) return;
        repoFormation[repoSelectedSlot] = null;
        RefreshRepoGrid();
        ClearRepoSelection();
        if (repoStatsPanel != null) repoStatsPanel.SetActive(false);
    }

    public void ConfirmReposition()
    {
        if (GameRunData.Instance != null)
        {
            GameRunData.Instance.formation = (HeroInstance[])repoFormation.Clone();
        }

        RepositionLivingHeroesInRoom();

        if (repoModal != null) repoModal.SetActive(false);
        isPaused = false;
        Time.timeScale = isDoubleSpeed ? 2f : 1f;

        ShowScreenAnnouncement("FORMATION UPDATED!", Color.cyan);
    }

    public void CloseRepositionModal()
    {
        if (repoModal != null) repoModal.SetActive(false);
        isPaused = false;
        Time.timeScale = isDoubleSpeed ? 2f : 1f;
    }

    void RepositionLivingHeroesInRoom()
    {
        if (GameRunData.Instance == null || heroAgents == null) return;
        Vector2 roomCenter = dungeonGenerator != null ? dungeonGenerator.GetRoomCenter(currentRoomIndex) : Vector2.zero;

        foreach (var agent in heroAgents)
        {
            if (agent == null || !agent.IsAlive || agent.heroInstance == null) continue;
            int newIdx = System.Array.IndexOf(GameRunData.Instance.formation, agent.heroInstance);
            if (newIdx >= 0)
            {
                agent.UpdateFormationSlot(newIdx);
                int col = newIdx % 3; // 0=back, 1=mid, 2=front
                int row = newIdx / 3; // 0=top, 1=mid, 2=bot
                float offsetX = (col - 1) * 0.75f - 1.2f;
                float offsetY = (1 - row) * 0.75f;
                Vector2 targetPos = roomCenter + new Vector2(offsetX, offsetY);
                agent.MoveTo(targetPos);
            }
        }
    }

    // ---- Boss Mechanics ----
    private bool bossSummonedWave75 = false;
    private bool bossSummonedWave50 = false;
    private bool bossSummonedWave25 = false;

    void SetupBossRoom()
    {
        if (bossHpRoot)
        {
            bossHpRoot.SetActive(true);
            EnsureBossHpIcon();
        }
        bossCurrentHP = bossMaxHP;
        bossHitCount = 0;
        bossSummonedWave75 = false;
        bossSummonedWave50 = false;
        bossSummonedWave25 = false;

        if (bossNameLabel) bossNameLabel.text = "SKELETON KING";
        if (bossHpBar)
        {
            bossHpBar.maxValue = bossMaxHP;
            bossHpBar.value = bossCurrentHP;
        }
        UpdateBossHitCounter();
    }

    private void EnsureBossHpIcon()
    {
        if (bossHpRoot == null) return;
        var existing = bossHpRoot.transform.Find("BossIcon");
        if (existing != null) return;

        var iconGo = new GameObject("BossIcon", typeof(RectTransform));
        iconGo.transform.SetParent(bossHpRoot.transform, false);
        var rt = iconGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-12f, 0f);
        rt.sizeDelta = new Vector2(58f, 58f);

        var bg = iconGo.AddComponent<Image>();
        bg.color = new Color(0.14f, 0.04f, 0.04f, 0.96f);

        var outline = iconGo.AddComponent<Outline>();
        outline.effectColor = new Color(0.95f, 0.2f, 0.2f, 0.95f);
        outline.effectDistance = new Vector2(2f, 2f);

        var sprGo = new GameObject("Avatar", typeof(RectTransform));
        sprGo.transform.SetParent(iconGo.transform, false);
        var sprRt = sprGo.GetComponent<RectTransform>();
        sprRt.anchorMin = new Vector2(0.08f, 0.08f);
        sprRt.anchorMax = new Vector2(0.92f, 0.92f);
        sprRt.offsetMin = Vector2.zero;
        sprRt.offsetMax = Vector2.zero;

        var img = sprGo.AddComponent<Image>();
        img.sprite = SpriteCache.LoadHeroOrBossSprite("big_demon") ?? SpriteCache.LoadSprite("big_demon_idle_anim_f0") ?? SpriteCache.LoadHeroOrBossSprite("skelet");
        img.preserveAspect = true;
        img.raycastTarget = false;
    }

    public void DamageBoss(int dmg)
    {
        bossCurrentHP = Mathf.Max(0, bossCurrentHP - dmg);
        if (bossHpBar) bossHpBar.value = bossCurrentHP;

        float hpRatio = (float)bossCurrentHP / bossMaxHP;

        if (!bossSummonedWave75 && hpRatio <= 0.75f && hpRatio > 0f)
        {
            bossSummonedWave75 = true;
            TriggerBossThresholdSummon("MINIONS, TO ARMS! ☠", 2, false);
        }
        else if (!bossSummonedWave50 && hpRatio <= 0.50f && hpRatio > 0f)
        {
            bossSummonedWave50 = true;
            TriggerBossThresholdSummon("SHADOWS HEED MY CALL! ☠", 3, true); // Summons with a Necromancer!
        }
        else if (!bossSummonedWave25 && hpRatio <= 0.25f && hpRatio > 0f)
        {
            bossSummonedWave25 = true;
            TriggerBossThresholdSummon("DEATH CANNOT BE DEFEATED! ⚡", 3, false);
        }

        if (bossCurrentHP <= 0)
        {
            ShowBossAlert("💀 SKELETON KING DEFEATED! 💀", new Color(1f, 0.85f, 0.2f));
            var bossAgent = enemyAgents.Find(e => e != null && e.IsBoss);
            if (bossAgent != null)
            {
                CombatCameraController.Instance?.TriggerTerminalClimax(bossAgent.transform.position);
                StartCoroutine(BossDefeatSequence(bossAgent));
            }
            foreach (var e in enemyAgents)
            {
                if (e != null && e.RoomIndex == currentRoomIndex && !e.IsBoss && e.gameObject.activeInHierarchy)
                    e.TakeDamage(999999);
            }
        }
        else
        {
            RegisterBossHit();
        }
    }

    private IEnumerator BossDefeatSequence(EnemyCombatAgent boss)
    {
        if (boss == null) yield break;
        var sr = boss.GetComponent<SpriteRenderer>();
        Vector3 origScale = boss.transform.localScale;

        float t = 0f;
        while (t < 1.0f)
        {
            t += Time.unscaledDeltaTime;
            if (sr != null)
            {
                float flash = Mathf.PingPong(t * 8f, 1f);
                sr.color = Color.Lerp(new Color(1f, 0.2f, 0.2f, 1f), new Color(1f, 0.85f, 0.2f, 1f), flash);
            }
            yield return null;
        }

        t = 0f;
        while (t < 0.6f)
        {
            t += Time.unscaledDeltaTime;
            float p = t / 0.6f;
            if (sr != null)
            {
                sr.color = new Color(1f, 0.2f, 0.2f, 1f - p);
            }
            boss.transform.localScale = Vector3.Lerp(origScale, origScale * 0.2f, p);
            yield return null;
        }

        boss.Kill();
    }

    void TriggerBossThresholdSummon(string announcement, int minionCount, bool spawnNecro)
    {
        ShowBossAlert(announcement, new Color(1f, 0.25f, 0.25f));
        CombatCameraController.Instance?.AddTrauma(0.40f);

        for (int i = 0; i < minionCount; i++)
        {
            var go = new GameObject($"BossWaveMinion_{i}");
            go.transform.SetParent(entityContainer, false);
            go.AddComponent<SpriteRenderer>().sortingOrder = 10;

            var e = go.AddComponent<EnemyCombatAgent>();
            bool isNecro = spawnNecro && (i == 0);
            e.Init(false, this, currentRoomIndex, isNecro ? EnemyArchetype.Necromancer : EnemyArchetype.StandardMelee);

            go.transform.position = (Vector3)dungeonGenerator.GetSpawnPosition(
                currentRoomIndex, true, i, minionCount);

            enemyAgents.Add(e);
        }
        UpdateBossHitCounter();
    }

    void RegisterBossHit()
    {
        bossHitCount++;
        UpdateBossHitCounter();
        if (bossHitCount >= BOSS_HIT_LIMIT)
        {
            bossHitCount = 0;
            TriggerShadowAwakening();
        }
    }

    void TriggerShadowAwakening()
    {
        for (int i = 0; i < 3; i++)
        {
            var go = new GameObject($"BossMinion_{i}");
            go.transform.SetParent(entityContainer, false);
            go.AddComponent<SpriteRenderer>().sortingOrder = 10;

            var e = go.AddComponent<EnemyCombatAgent>();
            e.Init(false, this, currentRoomIndex);

            go.transform.position = (Vector3)dungeonGenerator.GetSpawnPosition(
                currentRoomIndex, true, i, 3);

            enemyAgents.Add(e);
        }
        UpdateBossHitCounter();
    }

    void UpdateBossHitCounter()
    {
        if (bossHitCounter)
        {
            string skulls = "";
            for (int i = 0; i < BOSS_HIT_LIMIT; i++)
                skulls += i < bossHitCount ? "☠" : "○";
            bossHitCounter.text = "Summon: " + skulls;
        }
    }

    // ---- Pause & Speed ----
    void TogglePause()
    {
        isPaused = !isPaused;
        if (pausePanel)
        {
            pausePanel.SetActive(isPaused);
            if (isPaused) SetupPauseBanner();
        }
        Time.timeScale = isPaused ? 0f : (isDoubleSpeed ? 2f : 1f);
    }

    void SetupPauseBanner()
    {
        try
        {
            if (pausePanel == null) return;
            if (pausePanel.transform.Find("PauseGameBanner") != null) return;

            Sprite bannerSpr = LoadGameBannerSprite();
            if (bannerSpr == null) return;

            var bannerGo = new GameObject("PauseGameBanner", typeof(RectTransform));
            bannerGo.transform.SetParent(pausePanel.transform, false);
            bannerGo.transform.SetAsFirstSibling();

            var bannerRt = bannerGo.GetComponent<RectTransform>();
            bannerRt.anchorMin = new Vector2(0.5f, 0.78f);
            bannerRt.anchorMax = new Vector2(0.5f, 0.78f);
            bannerRt.anchoredPosition = new Vector2(0f, 0f);
            bannerRt.sizeDelta = new Vector2(280f, 100f);

            var bannerImg = bannerGo.AddComponent<Image>();
            bannerImg.sprite = bannerSpr;
            bannerImg.preserveAspect = true;

            var bannerOutline = bannerGo.AddComponent<Outline>();
            bannerOutline.effectColor = new Color(0.24f, 0.75f, 1f, 0.85f);
            bannerOutline.effectDistance = new Vector2(1.5f, 1.5f);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[DungeonCombatController] SetupPauseBanner: {ex.Message}");
        }
    }

    public static Sprite LoadGameBannerSprite()
    {
        try
        {
            Sprite spr = Resources.Load<Sprite>("Art/dungeon_cleaners_banner");
            if (spr != null) return spr;

            string path = System.IO.Path.Combine(Application.dataPath, "Sprites", "dungeon_cleaners_banner.png");
            if (System.IO.File.Exists(path))
            {
                byte[] bytes = System.IO.File.ReadAllBytes(path);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(bytes))
                {
                    tex.filterMode = FilterMode.Point;
                    return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[DungeonCombatController] LoadGameBannerSprite: {ex.Message}");
        }
        return null;
    }

    void ToggleSpeed()
    {
        isDoubleSpeed = !isDoubleSpeed;
        Time.timeScale = isPaused ? 0f : (isDoubleSpeed ? 2f : 1f);
        if (speedLabel) speedLabel.text = isDoubleSpeed ? "x1" : "x2";
    }

    // ---- Hero Detail ----
    void OpenHeroDetail(HeroInstance hero)
    {
        if (hero == null || hero.data == null) return;
        if (quickBuffBarRoot != null) quickBuffBarRoot.SetActive(false);
        if (portraitContainer != null) portraitContainer.gameObject.SetActive(false);
        if (heroDetailPanel)
        {
            heroDetailPanel.SetActive(true);
            heroDetailPanel.transform.SetAsLastSibling();
        }
        if (detailHeroName) detailHeroName.text = $"{hero.displayName} [{hero.data.heroClass}]";
        if (detailHP) detailHP.text = $"HP: {hero.currentHP}/{hero.data.maxHP}";
        if (detailEnergy) detailEnergy.text = $"Energy: {hero.currentEnergy}/{hero.data.maxEnergy}";
        string upgrades = hero.upgrades.Count > 0 ?
            "- " + string.Join("\n- ", hero.upgrades) : "(no upgrades acquired yet)";
        if (detailUpgrades) detailUpgrades.text = $"Upgrades:\n{upgrades}";
    }

    void CloseDetailPanel()
    {
        if (heroDetailPanel) heroDetailPanel.SetActive(false);
        if (quickBuffBarRoot != null) quickBuffBarRoot.SetActive(true);
        if (portraitContainer != null) portraitContainer.gameObject.SetActive(true);
    }

    // ---- Emergency Roll ----
    void TriggerEmergencyRoll(HeroInstance hero)
    {
        foreach (var agent in heroAgents)
        {
            if (agent.heroInstance == hero)
            {
                agent.TriggerDodgeRoll();
                break;
            }
        }
    }

    // ---- UI Refresh ----
    public void RebuildPortraits()
    {
        if (portraitContainer != null)
        {
            for (int i = portraitContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(portraitContainer.GetChild(i).gameObject);
            }
        }
        portraits.Clear();
        BuildPortraits();
    }

    public void RefreshPortraits()
    {
        foreach (var p in portraits) p.Refresh();
        UpdateQuickBuffBarUI();
    }

    void RefreshGold()
    {
        if (goldLabel && GameRunData.Instance != null)
            goldLabel.text = $"GOLD: {GameRunData.Instance.gold}";
        UpdateQuickBuffBarUI();
    }

    void UpdateFloorLabel()
    {
        int floorNum = (GameRunData.Instance?.currentRoom ?? 0) + 1;
        int totalFloors = GameRunData.Instance?.totalRooms ?? 7;
        if (floorLabel && GameRunData.Instance != null)
            floorLabel.text = $"FLOOR {floorNum}/{totalFloors}";
    }

    void UpdateRoomLabel()
    {
        if (roomLabel == null) return;
        var rt = roomLabel.rectTransform;
        if (rt != null)
        {
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -22f);
            rt.sizeDelta = new Vector2(750f, 44f);
            roomLabel.alignment = TextAlignmentOptions.Top;
            roomLabel.textWrappingMode = TextWrappingModes.NoWrap;
            roomLabel.overflowMode = TextOverflowModes.Overflow;
            roomLabel.fontSize = 14;
        }

        if (goldLabel != null)
        {
            var grt = goldLabel.rectTransform;
            if (grt != null)
            {
                grt.pivot = new Vector2(0.5f, 1f);
                grt.anchorMin = new Vector2(0.5f, 1f);
                grt.anchorMax = new Vector2(0.5f, 1f);
                grt.anchoredPosition = new Vector2(-420f, -22f);
                grt.sizeDelta = new Vector2(180f, 44f);
            }
        }

        var rooms = dungeonGenerator?.Rooms;
        int totalR = rooms?.Count ?? 5;
        int currentR = currentRoomIndex + 1;
        int floorNum = (GameRunData.Instance?.currentRoom ?? 0) + 1;
        int totalFloors = GameRunData.Instance?.totalRooms ?? 7;

        bool isBoss = rooms != null && currentRoomIndex < rooms.Count && rooms[currentRoomIndex].isBossRoom;
        string roomName = isBoss
            ? "Boss Chamber"
            : (rooms != null && currentRoomIndex < rooms.Count && rooms[currentRoomIndex].activeRoute != null
                ? rooms[currentRoomIndex].activeRoute.title
                : "Entrance Chamber");

        var track = new System.Text.StringBuilder();
        for (int i = 0; i < totalR; i++)
        {
            if (i == totalR - 1)
            {
                if (i == currentRoomIndex) track.Append("<color=#EF4444><b>[BOSS]</b></color>");
                else if (i < currentRoomIndex) track.Append("<color=#10B981>[*]</color>");
                else track.Append("<color=#64748B>[BOSS]</color>");
            }
            else
            {
                if (i < currentRoomIndex) track.Append("<color=#10B981>[x]</color> ");
                else if (i == currentRoomIndex) track.Append("<color=#FCD34D><b>[>]</b></color> ");
                else track.Append("<color=#64748B>[ ]</color> ");
            }
        }

        roomLabel.text = $"<size=15><b><color=#38BDF8>FLOOR {floorNum}/{totalFloors}</color></b>   <color=#64748B>|</color>   <b><color=#FCD34D>ROOM {currentR}/{totalR}</color></b>   <color=#CBD5E1>• {roomName}</color></size>\n<size=12><color=#94A3B8>PATH:</color> {track}</size>";
    }

    // =========================================================================
    // 2-ROAD ROUTE CHOICE MODAL
    // =========================================================================
    IEnumerator PresentRouteChoiceModal(DungeonRoom nextRoom)
    {
        if (nextRoom == null || nextRoom.isBossRoom)
            yield break;

        // Ensure room upgrade modal is completely dismissed first
        if (roomUpgradeModal != null)
        {
            roomUpgradeModal.SetActive(false);
        }

        while (roomUpgradeModal != null && roomUpgradeModal.activeInHierarchy)
        {
            yield return null;
        }

        // Focus camera on current room & exit gateways
        Vector2 roomCenter = dungeonGenerator != null ? dungeonGenerator.GetRoomCenter(currentRoomIndex) : (Vector2)mainCamera.transform.position;
        CombatCameraController.Instance?.FocusOnPoint(roomCenter, 7.5f);

        bool choiceMade = false;
        EnsureRouteChoiceModal(nextRoom, () => choiceMade = true);

        if (routeChoiceModal)
        {
            routeChoiceModal.transform.SetAsLastSibling();
            routeChoiceModal.SetActive(true);
        }

        while (!choiceMade)
        {
            yield return null;
        }

        if (routeChoiceModal) routeChoiceModal.SetActive(false);
        CombatCameraController.Instance?.ResumeMultiTargetTracking();
        yield return new WaitForSeconds(0.2f);
    }

    void EnsureRouteChoiceModal(DungeonRoom nextRoom, System.Action onChosen)
    {
        try
        {
            var canvas = pauseButton != null ? pauseButton.GetComponentInParent<Canvas>() : FindAnyObjectByType<Canvas>();
            if (canvas == null || nextRoom == null || nextRoom.isBossRoom)
            {
                onChosen?.Invoke();
                return;
            }

            if (nextRoom.routeOptionA == null)
            {
                nextRoom.routeOptionA = new RouteOption
                {
                    pathName = "NORTH ROUTE",
                    title = "CRYPT PASSAGE",
                    enemyPreview = "3-4 Skeletons",
                    hazardNotice = "Clear Chamber (No Traps)",
                    rewardPreview = "+45 Gold",
                    enemyCount = 3,
                    goldMultiplier = 1.0f,
                    cardAccentColor = new Color(0.22f, 0.48f, 0.75f)
                };
            }
            if (nextRoom.routeOptionB == null)
            {
                nextRoom.routeOptionB = new RouteOption
                {
                    pathName = "DIRECT PASSAGE",
                    title = "WARLORD'S VAULT",
                    enemyPreview = "1 Skeletal Warlord (2x HP)",
                    hazardNotice = "Heavy Striking Champion",
                    rewardPreview = "+110 Gold (+120%)",
                    enemyCount = 2,
                    goldMultiplier = 2.0f,
                    cardAccentColor = new Color(0.65f, 0.22f, 0.85f)
                };
            }

            if (routeChoiceModal == null)
            {
                routeChoiceModal = new GameObject("RouteChoiceModal", typeof(RectTransform));
                routeChoiceModal.transform.SetParent(canvas.transform, false);

                var rt = routeChoiceModal.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                var bg = routeChoiceModal.AddComponent<Image>();
                bg.color = new Color(0.02f, 0.03f, 0.06f, 0.85f);
                bg.raycastTarget = true;
            }
            else
            {
                routeChoiceModal.transform.SetAsLastSibling();
            }

            // Clear existing children
            for (int i = routeChoiceModal.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(routeChoiceModal.transform.GetChild(i).gameObject);
            }

            // Header Title
            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(routeChoiceModal.transform, false);
            var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            titleTmp.text = $"<size=24><color=#FCD34D><b>TACTICAL PATH RECONNAISSANCE</b></color></size>\n<size=14><color=#94A3B8>Branching passage ahead — select navigation path into Room {nextRoom.index + 1}</color></size>";
            titleTmp.fontSize = 24;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.raycastTarget = false;
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.1f, 0.82f);
            titleRt.anchorMax = new Vector2(0.9f, 0.98f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;

            // 2 Cards Layout (Left vs Right)
            float cardWidth = 330f;
            float cardHeight = 280f;
            float spacing = 40f;

            RouteOption[] options = new RouteOption[] { nextRoom.routeOptionA, nextRoom.routeOptionB };
            float[] xPositions = new float[] { -(cardWidth * 0.5f + spacing * 0.5f), (cardWidth * 0.5f + spacing * 0.5f) };

            for (int i = 0; i < 2; i++)
            {
                var opt = options[i];
                if (opt == null) continue;
                Color accent = (opt.cardAccentColor.a > 0.01f) ? opt.cardAccentColor : (i == 0 ? new Color(0.22f, 0.48f, 0.75f) : new Color(0.65f, 0.22f, 0.85f));

                var cardGo = new GameObject($"RouteCard_{i}", typeof(RectTransform));
                cardGo.transform.SetParent(routeChoiceModal.transform, false);

                var cardRt = cardGo.GetComponent<RectTransform>();
                cardRt.anchorMin = new Vector2(0.5f, 0.5f);
                cardRt.anchorMax = new Vector2(0.5f, 0.5f);
                cardRt.sizeDelta = new Vector2(cardWidth, cardHeight);
                cardRt.anchoredPosition = new Vector2(xPositions[i], -35f);

                var cardImg = cardGo.AddComponent<Image>();
                cardImg.color = new Color(0.06f, 0.08f, 0.14f, 0.95f);

                var cardOutline = cardGo.AddComponent<Outline>();
                cardOutline.effectColor = new Color(accent.r, accent.g, accent.b, 0.9f);
                cardOutline.effectDistance = new Vector2(2f, 2f);

                // Colored Header banner
                var headerGo = new GameObject("HeaderBanner", typeof(RectTransform));
                headerGo.transform.SetParent(cardGo.transform, false);
                var headerRt = headerGo.GetComponent<RectTransform>();
                headerRt.anchorMin = new Vector2(0f, 0.84f);
                headerRt.anchorMax = new Vector2(1f, 1f);
                headerRt.offsetMin = Vector2.zero;
                headerRt.offsetMax = Vector2.zero;
                var headerImg = headerGo.AddComponent<Image>();
                headerImg.color = accent;

                var headerLabelGo = new GameObject("HeaderLabel", typeof(RectTransform));
                headerLabelGo.transform.SetParent(headerGo.transform, false);
                var headerLabelRt = headerLabelGo.GetComponent<RectTransform>();
                headerLabelRt.anchorMin = Vector2.zero;
                headerLabelRt.anchorMax = Vector2.one;
                headerLabelRt.offsetMin = Vector2.zero;
                headerLabelRt.offsetMax = Vector2.zero;
                var headerTmp = headerLabelGo.AddComponent<TextMeshProUGUI>();
                headerTmp.text = $"<b>{opt.pathName ?? (i == 0 ? "NORTH ROUTE" : "DIRECT PASSAGE")}</b>";
                headerTmp.fontSize = 15;
                headerTmp.alignment = TextAlignmentOptions.Center;
                headerTmp.color = Color.white;
                headerTmp.raycastTarget = false;

                // Card Body Text
                var bodyGo = new GameObject("BodyText", typeof(RectTransform));
                bodyGo.transform.SetParent(cardGo.transform, false);
                var bodyRt = bodyGo.GetComponent<RectTransform>();
                bodyRt.anchorMin = new Vector2(0.08f, 0.22f);
                bodyRt.anchorMax = new Vector2(0.92f, 0.82f);
                bodyRt.offsetMin = Vector2.zero;
                bodyRt.offsetMax = Vector2.zero;

                var bodyTmp = bodyGo.AddComponent<TextMeshProUGUI>();
                bodyTmp.text = $"<size=19><color=#FCD34D><b>{opt.title ?? "CHAMBER"}</b></color></size>\n\n" +
                               $"<size=14><color=#E2E8F0>Encounter: <b>{opt.enemyPreview ?? "Hostiles"}</b></color>\n\n" +
                               $"<color=#F87171>Hazards: <b>{opt.hazardNotice ?? "None"}</b></color>\n\n" +
                               $"<color=#34D399>Rewards: <b>{opt.rewardPreview ?? "+50 Gold"}</b></color></size>";
                bodyTmp.fontSize = 14;
                bodyTmp.alignment = TextAlignmentOptions.Center;
                bodyTmp.raycastTarget = false;

                // Select Button
                var btnGo = new GameObject("SelectBtn", typeof(RectTransform));
                btnGo.transform.SetParent(cardGo.transform, false);
                var btnRt = btnGo.GetComponent<RectTransform>();
                btnRt.anchorMin = new Vector2(0.12f, 0.05f);
                btnRt.anchorMax = new Vector2(0.88f, 0.19f);
                btnRt.offsetMin = Vector2.zero;
                btnRt.offsetMax = Vector2.zero;

                var btnImg = btnGo.AddComponent<Image>();
                btnImg.color = accent;

                var btn = btnGo.AddComponent<Button>();
                var btnTextGo = new GameObject("BtnText", typeof(RectTransform));
                btnTextGo.transform.SetParent(btnGo.transform, false);
                var btnTextRt = btnTextGo.GetComponent<RectTransform>();
                btnTextRt.anchorMin = Vector2.zero;
                btnTextRt.anchorMax = Vector2.one;
                btnTextRt.offsetMin = Vector2.zero;
                btnTextRt.offsetMax = Vector2.zero;

                var btnTmp = btnTextGo.AddComponent<TextMeshProUGUI>();
                btnTmp.text = $"ADVANCE: {opt.pathName ?? "ROUTE"} >>";
                btnTmp.fontSize = 13;
                btnTmp.fontStyle = FontStyles.Bold;
                btnTmp.alignment = TextAlignmentOptions.Center;
                btnTmp.color = Color.white;
                btnTmp.raycastTarget = false;

                var cardBtn = cardGo.AddComponent<Button>();

                var selectedOpt = opt;
                void ChooseThisRoute()
                {
                    nextRoom.activeRoute = selectedOpt;
                    ShowScreenAnnouncement($"ROUTE CONFIRMED: {selectedOpt.title}!", Color.yellow);
                    if (routeChoiceModal != null)
                        routeChoiceModal.SetActive(false);

                    onChosen?.Invoke();
                }

                btn.onClick.AddListener(ChooseThisRoute);
                cardBtn.onClick.AddListener(ChooseThisRoute);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[EnsureRouteChoiceModal] Safe Fallback Exception: {ex}");
            if (routeChoiceModal != null)
                routeChoiceModal.SetActive(false);
            onChosen?.Invoke();
        }
    }

    // =========================================================================
    // IN-COMBAT QUICK-BUFFS BAR (POWER, SPEED, REVIVE)
    // =========================================================================
    public IEnumerator SlideInQuickBuffBar()
    {
        if (quickBuffBarRoot == null) yield break;
        quickBuffBarRoot.SetActive(true);

        var rt = quickBuffBarRoot.GetComponent<RectTransform>();
        Vector2 targetPos = new Vector2(16f, 185f);
        Vector2 startPos = new Vector2(-360f, 185f);
        rt.anchoredPosition = startPos;

        float elapsed = 0f;
        float dur = 0.32f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, elapsed / dur);
            rt.anchoredPosition = Vector2.Lerp(startPos, targetPos, p);
            yield return null;
        }
        rt.anchoredPosition = targetPos;
    }

    private IEnumerator CardScalePunch(RectTransform rt)
    {
        if (rt == null) yield break;
        Vector3 orig = Vector3.one;
        float t = 0f;
        while (t < 0.08f)
        {
            t += Time.unscaledDeltaTime;
            rt.localScale = Vector3.Lerp(orig, new Vector3(1.18f, 1.18f, 1f), t / 0.08f);
            yield return null;
        }
        t = 0f;
        while (t < 0.12f)
        {
            t += Time.unscaledDeltaTime;
            rt.localScale = Vector3.Lerp(new Vector3(1.18f, 1.18f, 1f), orig, t / 0.12f);
            yield return null;
        }
        rt.localScale = orig;
    }

    void SetupQuickBuffBar()
    {
        if (quickBuffBarRoot != null) return;

        Canvas canvas = pauseButton != null ? pauseButton.GetComponentInParent<Canvas>() : FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        quickBuffBarRoot = new GameObject("QuickBuffBar", typeof(RectTransform));
        quickBuffBarRoot.transform.SetParent(canvas.transform, false);

        var rootRt = quickBuffBarRoot.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0f, 0f);
        rootRt.anchorMax = new Vector2(0f, 0f);
        rootRt.pivot = new Vector2(0f, 0f);
        rootRt.anchoredPosition = new Vector2(16f, 185f);
        rootRt.sizeDelta = new Vector2(344f, 76f);

        // Container background with cyber styling
        var bg = quickBuffBarRoot.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.08f, 0.14f, 0.94f);

        var barOutline = quickBuffBarRoot.AddComponent<Outline>();
        barOutline.effectColor = new Color(0.2f, 0.7f, 1f, 0.45f);
        barOutline.effectDistance = new Vector2(1.5f, 1.5f);

        // Title banner
        var titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(quickBuffBarRoot.transform, false);
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -4f);
        titleRt.sizeDelta = new Vector2(-16f, 16f);

        var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "<b>⚡ TACTICAL QUICK-BUFFS</b>";
        titleTmp.fontSize = 11;
        titleTmp.alignment = TextAlignmentOptions.Left;
        titleTmp.color = new Color(0.98f, 0.85f, 0.35f, 1f);
        titleTmp.raycastTarget = false;

        // Create 3 mini cards
        float cardWidth = 102f;
        float cardHeight = 48f;
        float spacing = 8f;
        float startX = 8f;
        float yPos = 8f;

        // Card 1: POWER (40g)
        powerBuffBtn = CreateQuickBuffCard(
            quickBuffBarRoot.transform,
            new Vector2(startX, yPos),
            new Vector2(cardWidth, cardHeight),
            "<b>⚡ POWER</b>\n<size=9>+35% ATK</size>",
            $"{GameRunData.POWER_BUFF_COST}g",
            new Color(0.65f, 0.22f, 0.18f, 1f),
            out powerBuffCostText,
            out powerBuffBg,
            OnPowerBuffClicked
        );

        // Card 2: SPEED (50g)
        speedBuffBtn = CreateQuickBuffCard(
            quickBuffBarRoot.transform,
            new Vector2(startX + cardWidth + spacing, yPos),
            new Vector2(cardWidth, cardHeight),
            "<b>⏩ SPEED</b>\n<size=9>+40% SPD</size>",
            $"{GameRunData.SPEED_BUFF_COST}g",
            new Color(0.18f, 0.42f, 0.72f, 1f),
            out speedBuffCostText,
            out speedBuffBg,
            OnSpeedBuffClicked
        );

        // Card 3: REVIVE (80g)
        reviveBuffBtn = CreateQuickBuffCard(
            quickBuffBarRoot.transform,
            new Vector2(startX + (cardWidth + spacing) * 2, yPos),
            new Vector2(cardWidth, cardHeight),
            "<b>💖 REVIVE</b>\n<size=9>50% HP</size>",
            $"{GameRunData.REVIVE_BUFF_COST}g",
            new Color(0.14f, 0.55f, 0.28f, 1f),
            out reviveBuffCostText,
            out reviveBuffBg,
            OnReviveBuffClicked
        );

        UpdateQuickBuffBarUI();
    }

    Button CreateQuickBuffCard(
        Transform parent,
        Vector2 position,
        Vector2 size,
        string mainText,
        string defaultCostText,
        Color activeColor,
        out TextMeshProUGUI costTextOut,
        out Image bgImageOut,
        UnityEngine.Events.UnityAction onClickAction)
    {
        var cardGo = new GameObject("BuffCard", typeof(RectTransform));
        cardGo.transform.SetParent(parent, false);

        var rt = cardGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        var img = cardGo.AddComponent<Image>();
        img.color = activeColor;
        bgImageOut = img;

        var cardOutline = cardGo.AddComponent<Outline>();
        cardOutline.effectColor = new Color(activeColor.r * 1.35f, activeColor.g * 1.35f, activeColor.b * 1.35f, 0.85f);
        cardOutline.effectDistance = new Vector2(1.5f, 1.5f);

        var btn = cardGo.AddComponent<Button>();
        btn.onClick.AddListener(onClickAction);

        // Main text (title + description)
        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(cardGo.transform, false);
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0.05f, 0.32f);
        labelRt.anchorMax = new Vector2(0.95f, 0.95f);
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
        labelTmp.text = mainText;
        labelTmp.fontSize = 11;
        labelTmp.alignment = TextAlignmentOptions.Center;
        labelTmp.color = Color.white;
        labelTmp.raycastTarget = false;

        // Cost badge text
        var costGo = new GameObject("CostBadge", typeof(RectTransform));
        costGo.transform.SetParent(cardGo.transform, false);
        var costRt = costGo.GetComponent<RectTransform>();
        costRt.anchorMin = new Vector2(0.05f, 0.05f);
        costRt.anchorMax = new Vector2(0.95f, 0.35f);
        costRt.offsetMin = Vector2.zero;
        costRt.offsetMax = Vector2.zero;

        var costTmp = costGo.AddComponent<TextMeshProUGUI>();
        costTmp.text = $"<color=#FCD34D><b>{defaultCostText}</b></color>";
        costTmp.fontSize = 11;
        costTmp.alignment = TextAlignmentOptions.Center;
        costTmp.raycastTarget = false;
        costTextOut = costTmp;

        return btn;
    }

    void OnPowerBuffClicked()
    {
        var run = GameRunData.Instance;
        if (run == null) return;

        if (powerBuffBtn != null)
            StartCoroutine(CardScalePunch(powerBuffBtn.GetComponent<RectTransform>()));

        if (run.isPowerBuffActive)
        {
            ShowScreenAnnouncement("⚡ Power Buff Already Active This Room!", Color.yellow);
            return;
        }

        if (!run.CanAfford(GameRunData.POWER_BUFF_COST))
        {
            ShowScreenAnnouncement($"Need {GameRunData.POWER_BUFF_COST} Gold!", Color.red);
            return;
        }

        run.SpendGold(GameRunData.POWER_BUFF_COST);
        run.isPowerBuffActive = true;
        run.hasBoughtPowerThisRoom = true;
        RefreshGold();

        ShowScreenAnnouncement("⚡ SQUAD POWER SURGE! +35% ATK", new Color(1f, 0.85f, 0.2f));
        CombatCameraController.Instance?.AddTrauma(0.25f);

        foreach (var h in heroAgents)
        {
            if (h != null && h.IsAlive)
                CombatFeedback.Instance?.ShowText(h.transform.position, "⚡ +35% ATK!", new Color(1f, 0.85f, 0.2f), 2.5f);
        }
    }

    void OnSpeedBuffClicked()
    {
        var run = GameRunData.Instance;
        if (run == null) return;

        if (speedBuffBtn != null)
            StartCoroutine(CardScalePunch(speedBuffBtn.GetComponent<RectTransform>()));

        if (!run.CanAfford(GameRunData.SPEED_BUFF_COST))
        {
            ShowScreenAnnouncement($"Need {GameRunData.SPEED_BUFF_COST} Gold!", Color.red);
            return;
        }

        run.SpendGold(GameRunData.SPEED_BUFF_COST);
        run.speedBuffRoomsRemaining = 3; // +40% speed for 3 rooms
        run.hasBoughtSpeedThisRoom = true;
        RefreshGold();

        ShowScreenAnnouncement("⏩ SPEED BLITZ! +40% SPD (3 Rooms)", new Color(0.2f, 0.8f, 1f));
        CombatCameraController.Instance?.AddTrauma(0.20f);

        foreach (var h in heroAgents)
        {
            if (h != null && h.IsAlive)
                CombatFeedback.Instance?.ShowText(h.transform.position, "⏩ +40% SPD!", new Color(0.2f, 0.8f, 1f), 2.5f);
        }
    }

    void OnReviveBuffClicked()
    {
        var run = GameRunData.Instance;
        if (run == null) return;

        if (reviveBuffBtn != null)
            StartCoroutine(CardScalePunch(reviveBuffBtn.GetComponent<RectTransform>()));

        HeroCombatAgent fallenHero = heroAgents.Find(a => a != null && !a.IsAlive);
        if (fallenHero == null)
        {
            ShowScreenAnnouncement("All Squad Heroes Are Alive!", Color.green);
            return;
        }

        if (!run.CanAfford(GameRunData.REVIVE_BUFF_COST))
        {
            ShowScreenAnnouncement($"Need {GameRunData.REVIVE_BUFF_COST} Gold!", Color.red);
            return;
        }

        run.SpendGold(GameRunData.REVIVE_BUFF_COST);
        run.hasBoughtReviveThisRoom = true;

        Vector2 roomCenter = dungeonGenerator != null ? dungeonGenerator.GetRoomCenter(currentRoomIndex) : (Vector2)mainCamera.transform.position;
        fallenHero.Revive(roomCenter + new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(-0.4f, 0.4f)));

        RefreshGold();
        RefreshPortraits();

        ShowScreenAnnouncement($"💖 {fallenHero.heroInstance?.displayName ?? "HERO"} REVIVED AT 50% HP!", new Color(0.2f, 1f, 0.4f));
        CombatCameraController.Instance?.AddTrauma(0.35f);
    }

    void UpdateQuickBuffBarUI()
    {
        var run = GameRunData.Instance;
        if (run == null || quickBuffBarRoot == null) return;

        // 1. Power Card
        if (run.isPowerBuffActive)
        {
            if (powerBuffBg) powerBuffBg.color = new Color(0.2f, 0.6f, 0.25f, 1f); // Active green
            if (powerBuffCostText) powerBuffCostText.text = "<color=#A7F3D0><b>ACTIVE</b></color>";
        }
        else
        {
            bool canAffordPower = run.CanAfford(GameRunData.POWER_BUFF_COST);
            if (powerBuffBg) powerBuffBg.color = canAffordPower ? new Color(0.7f, 0.25f, 0.18f, 1f) : new Color(0.25f, 0.25f, 0.28f, 0.8f);
            if (powerBuffCostText) powerBuffCostText.text = canAffordPower ? $"<color=#FCD34D><b>{GameRunData.POWER_BUFF_COST}g</b></color>" : $"<color=#94A3B8>{GameRunData.POWER_BUFF_COST}g</color>";
        }

        // 2. Speed Card
        if (run.speedBuffRoomsRemaining > 0)
        {
            if (speedBuffBg) speedBuffBg.color = new Color(0.15f, 0.5f, 0.75f, 1f); // Active blue
            if (speedBuffCostText) speedBuffCostText.text = $"<color=#BAE6FD><b>ACTIVE ({run.speedBuffRoomsRemaining}r)</b></color>";
        }
        else
        {
            bool canAffordSpeed = run.CanAfford(GameRunData.SPEED_BUFF_COST);
            if (speedBuffBg) speedBuffBg.color = canAffordSpeed ? new Color(0.18f, 0.42f, 0.72f, 1f) : new Color(0.25f, 0.25f, 0.28f, 0.8f);
            if (speedBuffCostText) speedBuffCostText.text = canAffordSpeed ? $"<color=#FCD34D><b>{GameRunData.SPEED_BUFF_COST}g</b></color>" : $"<color=#94A3B8>{GameRunData.SPEED_BUFF_COST}g</color>";
        }

        // 3. Revive Card
        bool hasDeadHero = heroAgents != null && heroAgents.Exists(a => a != null && !a.IsAlive);
        if (!hasDeadHero)
        {
            if (reviveBuffBg) reviveBuffBg.color = new Color(0.22f, 0.24f, 0.28f, 0.7f);
            if (reviveBuffCostText) reviveBuffCostText.text = "<color=#64748B>FULL HP</color>";
        }
        else
        {
            bool canAffordRevive = run.CanAfford(GameRunData.REVIVE_BUFF_COST);
            if (reviveBuffBg) reviveBuffBg.color = canAffordRevive ? new Color(0.15f, 0.65f, 0.32f, 1f) : new Color(0.25f, 0.25f, 0.28f, 0.8f);
            if (reviveBuffCostText) reviveBuffCostText.text = canAffordRevive ? $"<color=#FCD34D><b>{GameRunData.REVIVE_BUFF_COST}g</b></color>" : $"<color=#EF4444><b>{GameRunData.REVIVE_BUFF_COST}g</b></color>";
        }
    }

    // =========================================================================
    // CYBERPUNK NARRATIVE GUIDE DIALOGUE (LEFT-DOCKED COMM FEED)
    // =========================================================================
    public void ShowCyberpunkDialogue(string speaker, string message, string focusCallout = "")
    {
        EnsureDialogueUI();
        if (dialogueBoxRoot == null) return;

        if (dialogueSpeakerText) dialogueSpeakerText.text = $"<color=#00F0FF><b>[ {speaker} ]</b></color>";
        if (dialogueBodyText) dialogueBodyText.text = $"\"{message}\"";

        dialogueBoxRoot.SetActive(true);

        if (dialogueDismissCoroutine != null) StopCoroutine(dialogueDismissCoroutine);
        dialogueDismissCoroutine = StartCoroutine(AutoDismissDialogue(6f));

        if (!string.IsNullOrEmpty(focusCallout))
        {
            StartCoroutine(PulseFocusCallout(focusCallout));
        }
    }

    private IEnumerator AutoDismissDialogue(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (dialogueBoxRoot != null) dialogueBoxRoot.SetActive(false);
    }

    IEnumerator PulseFocusCallout(string target)
    {
        Transform calloutTarget = null;
        if (target == "quickbuff" && quickBuffBarRoot != null)
            calloutTarget = quickBuffBarRoot.transform;
        else if (target == "repo" && repoButton != null)
            calloutTarget = repoButton.transform;

        if (calloutTarget == null) yield break;

        Vector3 originalScale = calloutTarget.localScale;
        for (int i = 0; i < 4; i++)
        {
            calloutTarget.localScale = originalScale * 1.15f;
            yield return new WaitForSeconds(0.25f);
            calloutTarget.localScale = originalScale;
            yield return new WaitForSeconds(0.25f);
        }
        calloutTarget.localScale = originalScale;
    }

    void EnsureDialogueUI()
    {
        if (dialogueBoxRoot != null) return;

        var canvas = pauseButton != null ? pauseButton.GetComponentInParent<Canvas>() : FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        dialogueBoxRoot = new GameObject("CyberpunkDialogueBox", typeof(RectTransform));
        dialogueBoxRoot.transform.SetParent(canvas.transform, false);

        var rt = dialogueBoxRoot.GetComponent<RectTransform>();
        // Docked cleanly at Top-Left, never blocking center combat or Boss HP
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(24f, -75f);
        rt.sizeDelta = new Vector2(520f, 120f);

        // Dark slate backdrop with pixel-art theme
        var bg = dialogueBoxRoot.AddComponent<Image>();
        DungeonUITheme.ApplyPanel(bg, new Color(0.08f, 0.10f, 0.16f, 0.98f));
        bg.raycastTarget = false;

        var outline = dialogueBoxRoot.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0.94f, 1f, 0.85f);
        outline.effectDistance = new Vector2(2f, 2f);

        // Comms icon badge
        var iconGo = new GameObject("CommsIcon", typeof(RectTransform));
        iconGo.transform.SetParent(dialogueBoxRoot.transform, false);
        var iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0f, 0.5f);
        iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0f, 0.5f);
        iconRt.anchoredPosition = new Vector2(14f, 0f);
        iconRt.sizeDelta = new Vector2(68f, 68f);

        var iconBg = iconGo.AddComponent<Image>();
        DungeonUITheme.ApplyCard(iconBg, new Color(0.14f, 0.20f, 0.32f, 1f));

        var avatarGo = new GameObject("Avatar", typeof(RectTransform));
        avatarGo.transform.SetParent(iconGo.transform, false);
        var avatarRt = avatarGo.GetComponent<RectTransform>();
        avatarRt.anchorMin = new Vector2(0.08f, 0.08f);
        avatarRt.anchorMax = new Vector2(0.92f, 0.92f);
        avatarRt.offsetMin = Vector2.zero;
        avatarRt.offsetMax = Vector2.zero;
        var avatarImg = avatarGo.AddComponent<Image>();
        avatarImg.sprite = SpriteCache.LoadHeroOrBossSprite("doc") ?? SpriteCache.LoadHeroOrBossSprite("elf_f");
        avatarImg.preserveAspect = true;
        avatarImg.raycastTarget = false;

        // Speaker Header
        var spkGo = new GameObject("Speaker", typeof(RectTransform));
        spkGo.transform.SetParent(dialogueBoxRoot.transform, false);
        var spkRt = spkGo.GetComponent<RectTransform>();
        spkRt.anchorMin = new Vector2(0.18f, 0.65f);
        spkRt.anchorMax = new Vector2(0.88f, 0.95f);
        spkRt.offsetMin = Vector2.zero;
        spkRt.offsetMax = Vector2.zero;

        dialogueSpeakerText = spkGo.AddComponent<TextMeshProUGUI>();
        dialogueSpeakerText.fontSize = 17;
        dialogueSpeakerText.fontStyle = FontStyles.Bold;
        dialogueSpeakerText.color = new Color(0f, 0.94f, 1f);
        dialogueSpeakerText.raycastTarget = false;

        // Message Body
        var bodyGo = new GameObject("Message", typeof(RectTransform));
        bodyGo.transform.SetParent(dialogueBoxRoot.transform, false);
        var bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0.18f, 0.08f);
        bodyRt.anchorMax = new Vector2(0.88f, 0.65f);
        bodyRt.offsetMin = Vector2.zero;
        bodyRt.offsetMax = Vector2.zero;

        dialogueBodyText = bodyGo.AddComponent<TextMeshProUGUI>();
        dialogueBodyText.fontSize = 15;
        dialogueBodyText.color = new Color(0.9f, 0.95f, 1f);
        dialogueBodyText.raycastTarget = false;

        // Dismiss Button
        var btnGo = new GameObject("DismissBtn", typeof(RectTransform));
        btnGo.transform.SetParent(dialogueBoxRoot.transform, false);
        var btnRt = btnGo.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(1f, 1f);
        btnRt.anchorMax = new Vector2(1f, 1f);
        btnRt.pivot = new Vector2(1f, 1f);
        btnRt.anchoredPosition = new Vector2(-8f, -8f);
        btnRt.sizeDelta = new Vector2(32f, 32f);

        btnGo.AddComponent<Image>();
        var btn = btnGo.AddComponent<Button>();
        DungeonUITheme.ApplyButton(btn, new Color(0.32f, 0.38f, 0.50f, 0.95f), false);
        btn.onClick.AddListener(() => dialogueBoxRoot.SetActive(false));

        var btnTextGo = new GameObject("BtnText", typeof(RectTransform));
        btnTextGo.transform.SetParent(btnGo.transform, false);
        var btnTextRt = btnTextGo.GetComponent<RectTransform>();
        btnTextRt.anchorMin = Vector2.zero;
        btnTextRt.anchorMax = Vector2.one;
        btnTextRt.offsetMin = Vector2.zero;
        btnTextRt.offsetMax = Vector2.zero;

        var btnTmp = btnTextGo.AddComponent<TextMeshProUGUI>();
        btnTmp.text = "✕";
        btnTmp.fontSize = 16;
        btnTmp.alignment = TextAlignmentOptions.Center;
        btnTmp.color = Color.white;
        btnTmp.raycastTarget = false;
    }

    // =========================================================================
    // DEDICATED NON-OVERLAPPING HUD ANNOUNCEMENT & BOSS ALERT BANNERS
    // =========================================================================
    public void ShowBossAlert(string message, Color accentColor)
    {
        EnsureBossAlertUI();
        if (bossAlertBannerRoot == null) return;

        if (bossAlertCoroutine != null) StopCoroutine(bossAlertCoroutine);
        bossAlertCoroutine = StartCoroutine(AnimateBossAlertBanner(message, accentColor));
    }

    private void EnsureBossAlertUI()
    {
        if (bossAlertBannerRoot != null) return;
        var canvas = pauseButton != null ? pauseButton.GetComponentInParent<Canvas>() : FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        bossAlertBannerRoot = new GameObject("BossAlertBanner", typeof(RectTransform));
        bossAlertBannerRoot.transform.SetParent(canvas.transform, false);

        var rt = bossAlertBannerRoot.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -78f);
        rt.sizeDelta = new Vector2(620f, 54f);

        var bg = bossAlertBannerRoot.AddComponent<Image>();
        DungeonUITheme.ApplyPanel(bg, new Color(0.20f, 0.08f, 0.10f, 0.96f));
        bg.raycastTarget = false;

        var outline = bossAlertBannerRoot.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.2f, 0.2f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, 1.5f);

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(bossAlertBannerRoot.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        bossAlertText = textGo.AddComponent<TextMeshProUGUI>();
        bossAlertText.fontSize = 20;
        bossAlertText.fontStyle = FontStyles.Bold;
        bossAlertText.alignment = TextAlignmentOptions.Center;
        bossAlertText.color = Color.white;
        bossAlertText.raycastTarget = false;

        bossAlertBannerRoot.SetActive(false);
    }

    private IEnumerator AnimateBossAlertBanner(string message, Color accentColor)
    {
        bossAlertBannerRoot.SetActive(true);
        if (bossAlertText != null)
        {
            bossAlertText.text = message;
            bossAlertText.color = accentColor;
        }

        var rt = bossAlertBannerRoot.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(0f, -78f);
        rt.localScale = new Vector3(0.85f, 0.85f, 1f);

        float elapsed = 0f;
        while (elapsed < 0.18f)
        {
            elapsed += Time.unscaledDeltaTime;
            rt.localScale = Vector3.Lerp(new Vector3(0.85f, 0.85f, 1f), Vector3.one, elapsed / 0.18f);
            yield return null;
        }
        rt.localScale = Vector3.one;

        yield return new WaitForSecondsRealtime(2.0f);

        elapsed = 0f;
        while (elapsed < 0.25f)
        {
            elapsed += Time.unscaledDeltaTime;
            rt.localScale = Vector3.Lerp(Vector3.one, new Vector3(1.05f, 0.1f, 1f), elapsed / 0.25f);
            yield return null;
        }

        bossAlertBannerRoot.SetActive(false);
        rt.localScale = Vector3.one;
    }

    public void ShowScreenAnnouncement(string message, Color color)
    {
        // Don't overlay any active modal
        if ((roomUpgradeModal != null && roomUpgradeModal.activeInHierarchy) ||
            (bossClearModal != null && bossClearModal.activeInHierarchy) ||
            (routeChoiceModal != null && routeChoiceModal.activeInHierarchy) ||
            (mercenaryMarketModal != null && mercenaryMarketModal.activeInHierarchy) ||
            (pausePanel != null && pausePanel.activeInHierarchy))
        {
            return;
        }

        EnsureScreenAnnouncementUI();
        if (screenAnnouncementBannerRoot == null) return;

        if (screenAnnouncementCoroutine != null) StopCoroutine(screenAnnouncementCoroutine);
        screenAnnouncementCoroutine = StartCoroutine(AnimateScreenAnnouncementBanner(message, color));
    }

    private void EnsureScreenAnnouncementUI()
    {
        if (screenAnnouncementBannerRoot != null) return;
        var canvas = pauseButton != null ? pauseButton.GetComponentInParent<Canvas>() : FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        screenAnnouncementBannerRoot = new GameObject("ScreenAnnouncementBanner", typeof(RectTransform));
        screenAnnouncementBannerRoot.transform.SetParent(canvas.transform, false);

        var rt = screenAnnouncementBannerRoot.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-15f, -95f);
        rt.sizeDelta = new Vector2(580f, 56f);

        var bg = screenAnnouncementBannerRoot.AddComponent<Image>();
        DungeonUITheme.ApplyPanel(bg, new Color(0.04f, 0.07f, 0.14f, 0.96f));
        bg.raycastTarget = false;

        var outline = screenAnnouncementBannerRoot.AddComponent<Outline>();
        outline.effectColor = new Color(0.2f, 0.8f, 1f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, 1.5f);

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(screenAnnouncementBannerRoot.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        screenAnnouncementText = textGo.AddComponent<TextMeshProUGUI>();
        screenAnnouncementText.fontSize = 19;
        screenAnnouncementText.fontStyle = FontStyles.Bold;
        screenAnnouncementText.alignment = TextAlignmentOptions.Center;
        screenAnnouncementText.color = Color.white;
        screenAnnouncementText.raycastTarget = false;

        screenAnnouncementBannerRoot.SetActive(false);
    }

    private IEnumerator AnimateScreenAnnouncementBanner(string message, Color color)
    {
        screenAnnouncementBannerRoot.SetActive(true);
        if (screenAnnouncementText != null)
        {
            screenAnnouncementText.text = message;
            screenAnnouncementText.color = color;
        }

        var rt = screenAnnouncementBannerRoot.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(-15f, -95f);

        rt.localScale = new Vector3(0.9f, 0.9f, 1f);
        float elapsed = 0f;
        while (elapsed < 0.18f)
        {
            elapsed += Time.unscaledDeltaTime;
            rt.localScale = Vector3.Lerp(new Vector3(0.9f, 0.9f, 1f), Vector3.one, elapsed / 0.18f);
            yield return null;
        }
        rt.localScale = Vector3.one;

        yield return new WaitForSecondsRealtime(2.0f);

        elapsed = 0f;
        while (elapsed < 0.25f)
        {
            elapsed += Time.unscaledDeltaTime;
            rt.localScale = Vector3.Lerp(Vector3.one, new Vector3(1.05f, 0.1f, 1f), elapsed / 0.25f);
            yield return null;
        }

        screenAnnouncementBannerRoot.SetActive(false);
        rt.localScale = Vector3.one;
    }

    // =========================================================================
    // POST-BOSS MERCENARY EXCHANGE MARKET ("HOW MANY DUDES" 2-COLUMN UI)
    // =========================================================================
    private class MercenaryRecruit
    {
        public string name;
        public string spritePrefix;
        public HeroClass heroClass;
        public int maxHP;
        public int attackDamage;
        public int maxEnergy;
        public float moveSpeed;
        public float attackSpeed;
        public int hireCost;
        public string specialty;
        public Color classColor;
    }

    private List<MercenaryRecruit> marketCandidates = new();
    private int marketSelectedRecruitIdx = -1;
    private int marketSelectedSquadIdx = -1;

    IEnumerator PresentMercenaryMarketModal()
    {
        bool marketDone = false;

        GenerateMercenaryMarketCandidates();
        marketSelectedRecruitIdx = -1;
        marketSelectedSquadIdx = -1;

        BuildMercenaryMarketModal(() => marketDone = true);

        if (mercenaryMarketModal) mercenaryMarketModal.SetActive(true);

        ShowCyberpunkDialogue("FIXER // KIRA", "Boss eliminated! The black market exchange is open. Swap out wounded or fatigued operatives for fresh combat mercenaries!");

        while (!marketDone)
        {
            yield return null;
        }

        if (mercenaryMarketModal) mercenaryMarketModal.SetActive(false);
        yield return new WaitForSeconds(0.3f);
    }

    void GenerateMercenaryMarketCandidates()
    {
        marketCandidates.Clear();

        var templates = new (string name, string sprite, HeroClass cls, int hp, int atk, int en, int cost, string spec, Color col)[]
        {
            ("Doc Hazard", "doc", HeroClass.Healer, 110, 16, 90, 60, "Combat Medic // Bio-Injector Burst", new Color(0.2f, 0.9f, 0.5f)),
            ("Angel Seraph", "angel", HeroClass.Healer, 95, 18, 95, 70, "Celestial Ward // Holy Restoration", new Color(0.95f, 0.95f, 0.55f)),
            ("Dwarf Ironclad", "dwarf_m", HeroClass.Knight, 175, 22, 50, 75, "Rune Bulwark // Heavy Shield Slam", new Color(0.95f, 0.5f, 0.2f)),
            ("Shieldmaiden Hilde", "dwarf_f", HeroClass.Knight, 165, 20, 55, 70, "Aegis Guard // Ground Shockwave", new Color(0.95f, 0.6f, 0.3f)),
            ("Pumpkin Jack", "pumpkin_dude", HeroClass.Knight, 155, 24, 60, 65, "Spooky Cleave // Fear Terrify", new Color(1f, 0.6f, 0.15f)),
            ("Wogol Brawler", "wogol", HeroClass.Knight, 160, 21, 50, 60, "Feral Claws // Frenzy Strike", new Color(0.85f, 0.35f, 0.25f)),
            ("Orc Warmaster", "orc_warrior", HeroClass.Knight, 180, 25, 45, 80, "Heavy Battleaxe // Bloodrush", new Color(0.4f, 0.85f, 0.3f)),
            ("Shaman Thrall", "orc_shaman", HeroClass.Mage, 90, 28, 85, 70, "Lightning Totem // Chain Shock", new Color(0.35f, 0.75f, 0.95f)),
            ("Lizard Venommancer", "lizard_f", HeroClass.Mage, 85, 27, 85, 65, "Toxic Spore // Acid Torrent", new Color(0.45f, 0.9f, 0.35f)),
            ("Lizard Slasher", "lizard_m", HeroClass.Knight, 155, 22, 55, 65, "Tail Sweep // Poison Fang", new Color(0.35f, 0.85f, 0.55f)),
            ("Elf Spellweaver", "elf_m", HeroClass.Mage, 80, 30, 95, 75, "Arcane Beam // Chrono Shift", new Color(0.55f, 0.65f, 1f)),
            ("Masked Berserker", "masked_orc", HeroClass.Knight, 170, 23, 50, 70, "Wild Flurry // Unbreakable Will", new Color(0.75f, 0.35f, 0.45f))
        };

        var pool = new List<(string name, string sprite, HeroClass cls, int hp, int atk, int en, int cost, string spec, Color col)>(templates);
        for (int i = 0; i < pool.Count; i++)
        {
            int r = Random.Range(i, pool.Count);
            var tmp = pool[i];
            pool[i] = pool[r];
            pool[r] = tmp;
        }

        for (int i = 0; i < 3; i++)
        {
            var t = pool[i];
            marketCandidates.Add(new MercenaryRecruit
            {
                name = t.name,
                spritePrefix = t.sprite,
                heroClass = t.cls,
                maxHP = t.hp,
                attackDamage = t.atk,
                maxEnergy = t.en,
                moveSpeed = t.cls == HeroClass.Knight ? 3.8f : 3.4f,
                attackSpeed = t.cls == HeroClass.Knight ? 1.2f : 1.0f,
                hireCost = t.cost,
                specialty = t.spec,
                classColor = t.col
            });
        }
    }

    void BuildMercenaryMarketModal(System.Action onFinished)
    {
        var canvas = pauseButton != null ? pauseButton.GetComponentInParent<Canvas>() : FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        if (mercenaryMarketModal == null)
        {
            mercenaryMarketModal = new GameObject("MercenaryMarketModal", typeof(RectTransform));
            mercenaryMarketModal.transform.SetParent(canvas.transform, false);

            var rt = mercenaryMarketModal.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bg = mercenaryMarketModal.AddComponent<Image>();
            bg.color = new Color(0.03f, 0.04f, 0.08f, 0.95f);
        }

        // Clear existing children
        for (int i = mercenaryMarketModal.transform.childCount - 1; i >= 0; i--)
        {
            Destroy(mercenaryMarketModal.transform.GetChild(i).gameObject);
        }

        // Stylized Game Art Banner
        Sprite marketBannerSpr = LoadGameBannerSprite();
        if (marketBannerSpr != null)
        {
            var bannerGo = new GameObject("MarketArtBanner", typeof(RectTransform));
            bannerGo.transform.SetParent(mercenaryMarketModal.transform, false);
            var bannerRt = bannerGo.GetComponent<RectTransform>();
            bannerRt.anchorMin = new Vector2(0.5f, 0.94f);
            bannerRt.anchorMax = new Vector2(0.5f, 0.94f);
            bannerRt.anchoredPosition = new Vector2(0f, 0f);
            bannerRt.sizeDelta = new Vector2(250f, 60f);

            var bannerImg = bannerGo.AddComponent<Image>();
            bannerImg.sprite = marketBannerSpr;
            bannerImg.preserveAspect = true;

            var bannerOutline = bannerGo.AddComponent<Outline>();
            bannerOutline.effectColor = new Color(0.24f, 0.75f, 1f, 0.85f);
            bannerOutline.effectDistance = new Vector2(1.5f, 1.5f);
        }

        // Header Title
        var titleGo = new GameObject("HeaderTitle");
        titleGo.transform.SetParent(mercenaryMarketModal.transform, false);
        var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text = $"<size=22><color=#FCD34D><b>POST-BOSS MERCENARY EXCHANGE</b></color></size>\n<size=13><color=#94A3B8>Swap exhausted squad heroes for fresh black-market combat mercenaries</color></size>";
        titleTmp.fontSize = 22;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.raycastTarget = false;
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.1f, 0.84f);
        titleRt.anchorMax = new Vector2(0.9f, 0.91f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;

        // Top Gold & Reroll Dice Bar
        int currentGold = GameRunData.Instance?.gold ?? 0;
        int rerollCost = Mathf.Max(20, Mathf.RoundToInt(currentGold * 0.25f));

        var barGo = new GameObject("TopActionRow", typeof(RectTransform));
        barGo.transform.SetParent(mercenaryMarketModal.transform, false);
        var barRt = barGo.GetComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0.1f, 0.77f);
        barRt.anchorMax = new Vector2(0.9f, 0.83f);
        barRt.offsetMin = Vector2.zero;
        barRt.offsetMax = Vector2.zero;

        var goldTmp = barGo.AddComponent<TextMeshProUGUI>();
        goldTmp.text = $"<b>GOLD: {currentGold}G</b>";
        goldTmp.fontSize = 18;
        goldTmp.color = new Color(1f, 0.85f, 0.2f);
        goldTmp.alignment = TextAlignmentOptions.Left;
        goldTmp.raycastTarget = false;

        // Reroll Dice Button
        var rerollGo = new GameObject("RerollDiceBtn", typeof(RectTransform));
        rerollGo.transform.SetParent(barGo.transform, false);
        var rerollRt = rerollGo.GetComponent<RectTransform>();
        rerollRt.anchorMin = new Vector2(1f, 0f);
        rerollRt.anchorMax = new Vector2(1f, 1f);
        rerollRt.pivot = new Vector2(1f, 0.5f);
        rerollRt.anchoredPosition = Vector2.zero;
        rerollRt.sizeDelta = new Vector2(210f, 34f);

        var rerollImg = rerollGo.AddComponent<Image>();
        rerollImg.color = new Color(0.24f, 0.35f, 0.65f, 0.95f);
        var rerollOutline = rerollGo.AddComponent<Outline>();
        rerollOutline.effectColor = new Color(0.4f, 0.7f, 1f, 0.8f);
        rerollOutline.effectDistance = new Vector2(1.5f, 1.5f);

        var rerollBtn = rerollGo.AddComponent<Button>();
        rerollBtn.onClick.AddListener(() =>
        {
            if (GameRunData.Instance != null && GameRunData.Instance.CanAfford(rerollCost))
            {
                GameRunData.Instance.SpendGold(rerollCost);
                RefreshGold();
                GenerateMercenaryMarketCandidates();
                marketSelectedRecruitIdx = -1;
                BuildMercenaryMarketModal(onFinished);
            }
            else
            {
                ShowScreenAnnouncement($"Need {rerollCost} Gold to Reroll!", Color.red);
            }
        });

        var rerollTextGo = new GameObject("RerollText", typeof(RectTransform));
        rerollTextGo.transform.SetParent(rerollGo.transform, false);
        var rerollTextRt = rerollTextGo.GetComponent<RectTransform>();
        rerollTextRt.anchorMin = Vector2.zero;
        rerollTextRt.anchorMax = Vector2.one;
        rerollTextRt.offsetMin = Vector2.zero;
        rerollTextRt.offsetMax = Vector2.zero;
        var rerollTmp = rerollTextGo.AddComponent<TextMeshProUGUI>();
        rerollTmp.text = $"REROLL DICE ({rerollCost}g)";
        rerollTmp.fontSize = 14;
        rerollTmp.fontStyle = FontStyles.Bold;
        rerollTmp.alignment = TextAlignmentOptions.Center;
        rerollTmp.color = Color.white;
        rerollTmp.raycastTarget = false;

        // Left Column: RECRUITS (width 360, height 360)
        float colWidth = 360f;
        float leftX = -200f;
        float rightX = 200f;

        var recruitHeaderGo = new GameObject("RecruitHeader", typeof(RectTransform));
        recruitHeaderGo.transform.SetParent(mercenaryMarketModal.transform, false);
        var recruitHeaderRt = recruitHeaderGo.GetComponent<RectTransform>();
        recruitHeaderRt.anchorMin = new Vector2(0.5f, 0.5f);
        recruitHeaderRt.anchorMax = new Vector2(0.5f, 0.5f);
        recruitHeaderRt.anchoredPosition = new Vector2(leftX, 140f);
        recruitHeaderRt.sizeDelta = new Vector2(colWidth, 30f);
        var recruitHeaderTmp = recruitHeaderGo.AddComponent<TextMeshProUGUI>();
        recruitHeaderTmp.text = "<color=#38BDF8><b>AVAILABLE RECRUITS (MARKET)</b></color>";
        recruitHeaderTmp.fontSize = 16;
        recruitHeaderTmp.alignment = TextAlignmentOptions.Center;
        recruitHeaderTmp.raycastTarget = false;

        for (int i = 0; i < marketCandidates.Count; i++)
        {
            var c = marketCandidates[i];
            int idx = i;
            var cGo = new GameObject($"RecruitCard_{i}", typeof(RectTransform));
            cGo.transform.SetParent(mercenaryMarketModal.transform, false);
            var cRt = cGo.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0.5f, 0.5f);
            cRt.anchorMax = new Vector2(0.5f, 0.5f);
            cRt.anchoredPosition = new Vector2(leftX, 70f - i * 82f);
            cRt.sizeDelta = new Vector2(colWidth, 76f);

            var cImg = cGo.AddComponent<Image>();
            bool isSel = (marketSelectedRecruitIdx == idx);
            cImg.color = isSel ? new Color(0.12f, 0.35f, 0.5f, 1f) : new Color(0.08f, 0.11f, 0.18f, 0.95f);

            var cardOutline = cGo.AddComponent<Outline>();
            cardOutline.effectColor = isSel ? new Color(0.2f, 0.9f, 1f, 0.95f) : new Color(c.classColor.r, c.classColor.g, c.classColor.b, 0.45f);
            cardOutline.effectDistance = new Vector2(1.5f, 1.5f);

            var cBtn = cGo.AddComponent<Button>();
            cBtn.onClick.AddListener(() =>
            {
                marketSelectedRecruitIdx = idx;
                BuildMercenaryMarketModal(onFinished);
            });

            // Left Thumbnail Container (Avatar)
            var thumbGo = new GameObject("AvatarThumb", typeof(RectTransform));
            thumbGo.transform.SetParent(cGo.transform, false);
            var thumbRt = thumbGo.GetComponent<RectTransform>();
            thumbRt.anchorMin = new Vector2(0f, 0.5f);
            thumbRt.anchorMax = new Vector2(0f, 0.5f);
            thumbRt.pivot = new Vector2(0f, 0.5f);
            thumbRt.anchoredPosition = new Vector2(8f, 0f);
            thumbRt.sizeDelta = new Vector2(56f, 56f);

            var thumbBg = thumbGo.AddComponent<Image>();
            thumbBg.color = new Color(0.04f, 0.05f, 0.09f, 0.95f);
            var thumbOutline = thumbGo.AddComponent<Outline>();
            thumbOutline.effectColor = new Color(c.classColor.r, c.classColor.g, c.classColor.b, 0.9f);
            thumbOutline.effectDistance = new Vector2(1.5f, 1.5f);

            var sprChild = new GameObject("Sprite", typeof(RectTransform));
            sprChild.transform.SetParent(thumbGo.transform, false);
            var sprRt = sprChild.GetComponent<RectTransform>();
            sprRt.anchorMin = new Vector2(0.1f, 0.1f);
            sprRt.anchorMax = new Vector2(0.9f, 0.9f);
            sprRt.offsetMin = Vector2.zero;
            sprRt.offsetMax = Vector2.zero;
            var sprImg = sprChild.AddComponent<Image>();
            sprImg.preserveAspect = true;
            sprImg.raycastTarget = false;
            var candidateSprite = SpriteCache.LoadHeroOrBossSprite(c.spritePrefix);
            if (candidateSprite != null)
            {
                sprImg.sprite = candidateSprite;
                sprImg.color = Color.white;
            }

            // Class Badge on Thumbnail
            var badgeGo = new GameObject("ClassBadge", typeof(RectTransform));
            badgeGo.transform.SetParent(thumbGo.transform, false);
            var badgeRt = badgeGo.GetComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(1f, 0f);
            badgeRt.anchorMax = new Vector2(1f, 0f);
            badgeRt.pivot = new Vector2(1f, 0f);
            badgeRt.anchoredPosition = new Vector2(2f, -2f);
            badgeRt.sizeDelta = new Vector2(20f, 20f);
            var bBg = badgeGo.AddComponent<Image>();
            bBg.color = new Color(0.05f, 0.07f, 0.12f, 0.95f);
            var bTextGo = new GameObject("Text", typeof(RectTransform));
            bTextGo.transform.SetParent(badgeGo.transform, false);
            var bTextRt = bTextGo.GetComponent<RectTransform>();
            bTextRt.anchorMin = Vector2.zero;
            bTextRt.anchorMax = Vector2.one;
            bTextRt.offsetMin = Vector2.zero;
            bTextRt.offsetMax = Vector2.zero;
            var bTmp = bTextGo.AddComponent<TextMeshProUGUI>();
            bTmp.text = c.heroClass switch { HeroClass.Knight => "⚔", HeroClass.Mage => "✦", HeroClass.Healer => "✚", _ => "●" };
            bTmp.fontSize = 12;
            bTmp.alignment = TextAlignmentOptions.Center;
            bTmp.color = c.classColor;
            bTmp.raycastTarget = false;

            // Details Text Container
            var cTxtGo = new GameObject("Text", typeof(RectTransform));
            cTxtGo.transform.SetParent(cGo.transform, false);
            var cTxtRt = cTxtGo.GetComponent<RectTransform>();
            cTxtRt.anchorMin = new Vector2(0.20f, 0.05f);
            cTxtRt.anchorMax = new Vector2(0.97f, 0.95f);
            cTxtRt.offsetMin = Vector2.zero;
            cTxtRt.offsetMax = Vector2.zero;

            var cTmp = cTxtGo.AddComponent<TextMeshProUGUI>();
            string selPrefix = isSel ? "▶ " : "";
            cTmp.text = $"<b>{selPrefix}<color=#FCD34D>{c.name}</color></b> <size=11><color=#94A3B8>[{c.heroClass}]</color></size>  •  <color=#FCD34D><b>{c.hireCost}g</b></color>\n" +
                        $"<size=11>HP: {c.maxHP} | ATK: {c.attackDamage} | EN: {c.maxEnergy}</size>\n" +
                        $"<size=10><color=#34D399>{c.specialty}</color></size>";
            cTmp.fontSize = 12;
            cTmp.raycastTarget = false;
        }

        // Right Column: SQUAD (width 360)
        var squadHeaderGo = new GameObject("SquadHeader", typeof(RectTransform));
        squadHeaderGo.transform.SetParent(mercenaryMarketModal.transform, false);
        var squadHeaderRt = squadHeaderGo.GetComponent<RectTransform>();
        squadHeaderRt.anchorMin = new Vector2(0.5f, 0.5f);
        squadHeaderRt.anchorMax = new Vector2(0.5f, 0.5f);
        squadHeaderRt.anchoredPosition = new Vector2(rightX, 140f);
        squadHeaderRt.sizeDelta = new Vector2(colWidth, 30f);
        var squadHeaderTmp = squadHeaderGo.AddComponent<TextMeshProUGUI>();
        squadHeaderTmp.text = "<color=#F43F5E><b>ACTIVE SQUAD (SELECT TO REPLACE)</b></color>";
        squadHeaderTmp.fontSize = 16;
        squadHeaderTmp.alignment = TextAlignmentOptions.Center;
        squadHeaderTmp.raycastTarget = false;

        var squad = GameRunData.Instance?.squad ?? new List<HeroInstance>();
        for (int j = 0; j < squad.Count; j++)
        {
            var hero = squad[j];
            int sIdx = j;
            var sGo = new GameObject($"SquadCard_{j}", typeof(RectTransform));
            sGo.transform.SetParent(mercenaryMarketModal.transform, false);
            var sRt = sGo.GetComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0.5f, 0.5f);
            sRt.anchorMax = new Vector2(0.5f, 0.5f);
            sRt.anchoredPosition = new Vector2(rightX, 85f - j * 54f);
            sRt.sizeDelta = new Vector2(colWidth, 50f);

            var sImg = sGo.AddComponent<Image>();
            bool isSel = (marketSelectedSquadIdx == sIdx);
            sImg.color = isSel ? new Color(0.45f, 0.15f, 0.2f, 1f) : new Color(0.10f, 0.12f, 0.18f, 0.95f);

            var sOutline = sGo.AddComponent<Outline>();
            sOutline.effectColor = isSel ? new Color(1f, 0.3f, 0.3f, 0.95f) : new Color(0.3f, 0.4f, 0.5f, 0.4f);
            sOutline.effectDistance = new Vector2(1.5f, 1.5f);

            var sBtn = sGo.AddComponent<Button>();
            sBtn.onClick.AddListener(() =>
            {
                marketSelectedSquadIdx = sIdx;
                BuildMercenaryMarketModal(onFinished);
            });

            // Hero Avatar Thumbnail
            var sThumbGo = new GameObject("AvatarThumb", typeof(RectTransform));
            sThumbGo.transform.SetParent(sGo.transform, false);
            var sThumbRt = sThumbGo.GetComponent<RectTransform>();
            sThumbRt.anchorMin = new Vector2(0f, 0.5f);
            sThumbRt.anchorMax = new Vector2(0f, 0.5f);
            sThumbRt.pivot = new Vector2(0f, 0.5f);
            sThumbRt.anchoredPosition = new Vector2(6f, 0f);
            sThumbRt.sizeDelta = new Vector2(40f, 40f);

            var sThumbBg = sThumbGo.AddComponent<Image>();
            sThumbBg.color = new Color(0.04f, 0.05f, 0.09f, 0.95f);
            var sThumbOutline = sThumbGo.AddComponent<Outline>();
            sThumbOutline.effectColor = new Color(hero.data.classColor.r, hero.data.classColor.g, hero.data.classColor.b, 0.85f);
            sThumbOutline.effectDistance = new Vector2(1f, 1f);

            var sSprChild = new GameObject("Sprite", typeof(RectTransform));
            sSprChild.transform.SetParent(sThumbGo.transform, false);
            var sSprRt = sSprChild.GetComponent<RectTransform>();
            sSprRt.anchorMin = new Vector2(0.1f, 0.1f);
            sSprRt.anchorMax = new Vector2(0.9f, 0.9f);
            sSprRt.offsetMin = Vector2.zero;
            sSprRt.offsetMax = Vector2.zero;
            var sSprImg = sSprChild.AddComponent<Image>();
            sSprImg.preserveAspect = true;
            sSprImg.raycastTarget = false;
            string heroPrefix = SpriteMapping.GetHeroSpritePrefix(hero.data.heroName, hero.data.spritePrefix);
            var heroSprite = SpriteCache.LoadHeroOrBossSprite(heroPrefix);
            if (heroSprite != null)
            {
                sSprImg.sprite = heroSprite;
                sSprImg.color = Color.white;
            }

            var sTxtGo = new GameObject("Text", typeof(RectTransform));
            sTxtGo.transform.SetParent(sGo.transform, false);
            var sTxtRt = sTxtGo.GetComponent<RectTransform>();
            sTxtRt.anchorMin = new Vector2(0.16f, 0.05f);
            sTxtRt.anchorMax = new Vector2(0.97f, 0.95f);
            sTxtRt.offsetMin = Vector2.zero;
            sTxtRt.offsetMax = Vector2.zero;

            var sTmp = sTxtGo.AddComponent<TextMeshProUGUI>();
            string selPrefix = isSel ? "▶ " : "";
            sTmp.text = $"<b>{selPrefix}{hero.displayName}</b> <size=11><color=#94A3B8>[{hero.data.heroClass}]</color></size>\n" +
                        $"<size=11>HP: {hero.currentHP}/{hero.data.maxHP} | EN: {hero.currentEnergy}/{hero.data.maxEnergy}</size>";
            sTmp.fontSize = 12;
            sTmp.raycastTarget = false;
        }

        // Bottom Action Row: SWAP & CONTINUE
        bool canSwap = marketSelectedRecruitIdx >= 0 && marketSelectedRecruitIdx < marketCandidates.Count &&
                       marketSelectedSquadIdx >= 0 && marketSelectedSquadIdx < squad.Count;

        var swapGo = new GameObject("SwapBtn", typeof(RectTransform));
        swapGo.transform.SetParent(mercenaryMarketModal.transform, false);
        var swapRt = swapGo.GetComponent<RectTransform>();
        swapRt.anchorMin = new Vector2(0.5f, 0f);
        swapRt.anchorMax = new Vector2(0.5f, 0f);
        swapRt.anchoredPosition = new Vector2(-120f, 45f);
        swapRt.sizeDelta = new Vector2(210f, 50f);

        var swapImg = swapGo.AddComponent<Image>();
        swapImg.color = canSwap ? new Color(0.16f, 0.65f, 0.35f, 1f) : new Color(0.25f, 0.25f, 0.28f, 0.7f);

        var swapOutline = swapGo.AddComponent<Outline>();
        swapOutline.effectColor = canSwap ? new Color(0.4f, 1f, 0.6f, 0.9f) : new Color(0.3f, 0.3f, 0.3f, 0.5f);
        swapOutline.effectDistance = new Vector2(1.5f, 1.5f);

        var swapBtn = swapGo.AddComponent<Button>();
        swapBtn.interactable = canSwap;
        swapBtn.onClick.AddListener(() =>
        {
            if (!canSwap) return;
            var recruit = marketCandidates[marketSelectedRecruitIdx];
            var run = GameRunData.Instance;
            if (run == null) return;

            if (!run.CanAfford(recruit.hireCost))
            {
                ShowScreenAnnouncement($"Need {recruit.hireCost} Gold to Hire!", Color.red);
                return;
            }

            run.SpendGold(recruit.hireCost);

            // Create new HeroData & HeroInstance with distinct sprite prefix
            var heroData = ScriptableObject.CreateInstance<HeroData>();
            heroData.heroName = recruit.name;
            heroData.heroClass = recruit.heroClass;
            heroData.maxHP = recruit.maxHP;
            heroData.attackDamage = recruit.attackDamage;
            heroData.maxEnergy = recruit.maxEnergy;
            heroData.moveSpeed = recruit.moveSpeed;
            heroData.attackSpeed = recruit.attackSpeed;
            heroData.classColor = recruit.classColor;
            heroData.spritePrefix = recruit.spritePrefix;

            SpriteMapping.RegisterHero(recruit.name, recruit.spritePrefix);

            var newInstance = new HeroInstance(heroData);
            var oldHero = run.squad[marketSelectedSquadIdx];

            // Replace in squad
            run.squad[marketSelectedSquadIdx] = newInstance;

            // Replace in formation (preserve slot!)
            for (int k = 0; k < 9; k++)
            {
                if (run.formation[k] == oldHero)
                {
                    run.formation[k] = newInstance;
                    newInstance.gridIndex = k;
                }
            }

            // Replace in-world combat agent so the new character sprite appears immediately in the dungeon!
            HeroCombatAgent oldAgent = heroAgents.Find(a => a != null && a.heroInstance == oldHero);
            Vector3 spawnPos = oldAgent != null ? oldAgent.transform.position : (Vector3)dungeonGenerator.GetRoomCenter(currentRoomIndex);
            if (oldAgent != null)
            {
                heroAgents.Remove(oldAgent);
                Destroy(oldAgent.gameObject);
            }

            var newHeroGo = new GameObject($"Hero_{newInstance.displayName}");
            newHeroGo.transform.SetParent(entityContainer, false);
            var sr = newHeroGo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 10;
            var newAgent = newHeroGo.AddComponent<HeroCombatAgent>();
            newAgent.Init(newInstance, newInstance.gridIndex, this);
            newHeroGo.transform.position = spawnPos;
            heroAgents.Add(newAgent);

            RefreshGold();
            RebuildPortraits();

            ShowScreenAnnouncement($"HIRED {recruit.name}! DEPLOYED TO SQUAD!", Color.cyan);
            CombatCameraController.Instance?.AddTrauma(0.35f);

            marketCandidates.RemoveAt(marketSelectedRecruitIdx);
            marketSelectedRecruitIdx = -1;
            marketSelectedSquadIdx = -1;

            BuildMercenaryMarketModal(onFinished);
        });

        var swapTextGo = new GameObject("SwapText", typeof(RectTransform));
        swapTextGo.transform.SetParent(swapGo.transform, false);
        var swapTextRt = swapTextGo.GetComponent<RectTransform>();
        swapTextRt.anchorMin = Vector2.zero;
        swapTextRt.anchorMax = Vector2.one;
        swapTextRt.offsetMin = Vector2.zero;
        swapTextRt.offsetMax = Vector2.zero;
        var swapTmp = swapTextGo.AddComponent<TextMeshProUGUI>();
        swapTmp.text = "<b>SWAP HERO -></b>";
        swapTmp.fontSize = 15;
        swapTmp.fontStyle = FontStyles.Bold;
        swapTmp.alignment = TextAlignmentOptions.Center;
        swapTmp.color = Color.white;
        swapTmp.raycastTarget = false;

        // Continue Button
        var contGo = new GameObject("ContinueBtn", typeof(RectTransform));
        contGo.transform.SetParent(mercenaryMarketModal.transform, false);
        var contRt = contGo.GetComponent<RectTransform>();
        contRt.anchorMin = new Vector2(0.5f, 0f);
        contRt.anchorMax = new Vector2(0.5f, 0f);
        contRt.anchoredPosition = new Vector2(130f, 45f);
        contRt.sizeDelta = new Vector2(230f, 50f);

        var contImg = contGo.AddComponent<Image>();
        contImg.color = new Color(0.22f, 0.45f, 0.85f, 1f);

        var contOutline = contGo.AddComponent<Outline>();
        contOutline.effectColor = new Color(0.4f, 0.7f, 1f, 0.9f);
        contOutline.effectDistance = new Vector2(1.5f, 1.5f);

        var contBtn = contGo.AddComponent<Button>();
        contBtn.onClick.AddListener(() => onFinished?.Invoke());

        var contTextGo = new GameObject("ContText", typeof(RectTransform));
        contTextGo.transform.SetParent(contGo.transform, false);
        var contTextRt = contTextGo.GetComponent<RectTransform>();
        contTextRt.anchorMin = Vector2.zero;
        contTextRt.anchorMax = Vector2.one;
        contTextRt.offsetMin = Vector2.zero;
        contTextRt.offsetMax = Vector2.zero;
        var contTmp = contTextGo.AddComponent<TextMeshProUGUI>();
        contTmp.text = "<b>CONTINUE RUN >></b>";
        contTmp.fontSize = 15;
        contTmp.fontStyle = FontStyles.Bold;
        contTmp.alignment = TextAlignmentOptions.Center;
        contTmp.color = Color.white;
        contTmp.raycastTarget = false;
    }
}
