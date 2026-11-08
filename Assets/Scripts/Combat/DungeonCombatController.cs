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

        // Ensure all squad heroes are revived and full HP on run start
        if (GameRunData.Instance != null)
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
        }

        SetupButtons();
        BuildPortraits();

        // Generate dungeon floor
        int floorNumber = GameRunData.Instance?.currentRoom ?? 0;
        dungeonGenerator.GenerateFloor(floorNumber);

        // Position camera immediately at first room center (locking Z to -10)
        Vector2 firstCenter = dungeonGenerator.GetRoomCenter(0);
        cameraTargetPos = new Vector3(firstCenter.x, firstCenter.y, -10f);
        if (mainCamera != null)
        {
            mainCamera.transform.position = cameraTargetPos;
        }

        // Spawn heroes at first room
        SpawnHeroes();

        // Start the dungeon loop
        StartCoroutine(DungeonLoop());

        RefreshGold();
        UpdateFloorLabel();
    }

    void SetupButtons()
    {
        if (pauseButton) pauseButton.onClick.AddListener(TogglePause);
        if (resumeButton) resumeButton.onClick.AddListener(TogglePause);
        if (quitButton) quitButton.onClick.AddListener(() => SceneNav.GoTo(SceneNames.MainMenu));
        if (speedButton) speedButton.onClick.AddListener(ToggleSpeed);
        if (detailBackBtn) detailBackBtn.onClick.AddListener(CloseDetailPanel);

        SetupRepoButton();

        if (pausePanel) pausePanel.SetActive(false);
        if (heroDetailPanel) heroDetailPanel.SetActive(false);
        if (bossHpRoot) bossHpRoot.SetActive(false);
        if (roomUpgradeModal) roomUpgradeModal.SetActive(false);
        if (repoModal) repoModal.SetActive(false);
    }

    // ---- Portrait Panel ----
    void BuildPortraits()
    {
        if (GameRunData.Instance == null || portraitPrefab == null || portraitContainer == null) return;
        foreach (var hero in GameRunData.Instance.squad)
        {
            var go = Instantiate(portraitPrefab, portraitContainer);
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
        if (!hero.isAlive) return;

        if (hero.isFatigued)
        {
            TriggerEmergencyRoll(hero);
        }
        else
        {
            OpenHeroDetail(hero);
        }
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

            // 1. Move heroes to room
            yield return StartCoroutine(MoveHeroesToRoom(r));

            // 2. Wait a beat before spawning enemies
            yield return new WaitForSeconds(0.4f);

            // 3. Spawn enemies in this room
            SpawnEnemiesInRoom(r, room);

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

            // 9. Interactive 3-Upgrade Choice between rooms (if not the boss room)
            if (!room.isBossRoom && r < rooms.Count - 1)
            {
                yield return StartCoroutine(PresentRoomUpgradeChoice());
            }

            // Short breather before corridor march
            yield return new WaitForSeconds(0.5f);
        }

        // All rooms cleared — advance floor
        yield return new WaitForSeconds(1f);

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
        bool isBoss = room.isBossRoom;
        int enemyCount = isBoss ? 1 : Random.Range(3, 6);

        int floor = GameRunData.Instance?.currentRoom ?? 0;
        if (!isBoss && floor > 2)
            enemyCount += Random.Range(0, 2);

        for (int i = 0; i < enemyCount; i++)
        {
            var go = new GameObject($"Enemy_{roomIndex}_{i}");
            go.transform.SetParent(entityContainer, false);

            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = 10;

            var enemy = go.AddComponent<EnemyCombatAgent>();
            enemy.Init(isBoss, this, roomIndex);

            // Position in room
            go.transform.position = (Vector3)dungeonGenerator.GetSpawnPosition(roomIndex, true, i, enemyCount);

            enemyAgents.Add(enemy);
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

    // ---- Interactive 3-Upgrade Selection Between Rooms ----
    private class RoomUpgradeOption
    {
        public string title;
        public string description;
        public string icon;
        public System.Action onSelect;
    }

    IEnumerator PresentRoomUpgradeChoice()
    {
        // Build 3 random upgrade options
        var pool = new List<RoomUpgradeOption>
        {
            new RoomUpgradeOption
            {
                title = "SHARPENED BLADES",
                description = "+25% Melee damage for Knights",
                icon = "<color=#EF4444><b>[ ATK ]</b></color>",
                onSelect = () => {
                    foreach (var h in heroAgents)
                    {
                        if (h.HeroClass == HeroClass.Knight)
                        {
                            h.heroInstance.data.attackDamage = Mathf.RoundToInt(h.heroInstance.data.attackDamage * 1.25f);
                            h.heroInstance.upgrades.Add("Sharpened Blades (+25% ATK)");
                        }
                    }
                    RefreshPortraits();
                }
            },
            new RoomUpgradeOption
            {
                title = "ARCANE PYRE",
                description = "+30% Fireball spell power for Mages",
                icon = "<color=#F97316><b>[ FIRE ]</b></color>",
                onSelect = () => {
                    foreach (var h in heroAgents)
                    {
                        if (h.HeroClass == HeroClass.Mage)
                        {
                            h.heroInstance.data.attackDamage = Mathf.RoundToInt(h.heroInstance.data.attackDamage * 1.3f);
                            h.heroInstance.upgrades.Add("Arcane Pyre (+30% Spell Power)");
                        }
                    }
                    RefreshPortraits();
                }
            },
            new RoomUpgradeOption
            {
                title = "DIVINE BLESSING",
                description = "Heals all squad heroes +30 HP immediately",
                icon = "<color=#10B981><b>[ HEAL ]</b></color>",
                onSelect = () => {
                    foreach (var h in heroAgents)
                    {
                        if (h.IsAlive)
                        {
                            h.ReceiveHeal(30);
                            h.heroInstance.upgrades.Add("Divine Blessing (+30 HP)");
                        }
                    }
                    RefreshPortraits();
                }
            },
            new RoomUpgradeOption
            {
                title = "IRON BULWARK",
                description = "+35 Max HP to all heroes",
                icon = "<color=#3B82F6><b>[ DEF ]</b></color>",
                onSelect = () => {
                    foreach (var h in heroAgents)
                    {
                        h.heroInstance.data.maxHP += 35;
                        h.heroInstance.currentHP += 35;
                        h.heroInstance.upgrades.Add("Iron Bulwark (+35 Max HP)");
                    }
                    RefreshPortraits();
                }
            },
            new RoomUpgradeOption
            {
                title = "SWIFT STRIDES",
                description = "+20% Move Speed & Quick Recovery",
                icon = "<color=#8B5CF6><b>[ SPEED ]</b></color>",
                onSelect = () => {
                    foreach (var h in heroAgents)
                    {
                        h.heroInstance.data.moveSpeed *= 1.2f;
                        h.heroInstance.upgrades.Add("Swift Strides (+20% Speed)");
                    }
                    RefreshPortraits();
                }
            },
            new RoomUpgradeOption
            {
                title = "DUNGEON BOUNTY",
                description = "+60 Gold earned",
                icon = "<color=#FCD34D><b>[ GOLD ]</b></color>",
                onSelect = () => {
                    if (GameRunData.Instance != null) GameRunData.Instance.gold += 60;
                    RefreshGold();
                }
            }
        };

        // Shuffle and pick 3
        for (int i = 0; i < pool.Count; i++)
        {
            int rIdx = Random.Range(i, pool.Count);
            var tmp = pool[i];
            pool[i] = pool[rIdx];
            pool[rIdx] = tmp;
        }

        var chosenThree = pool.GetRange(0, 3);
        bool choiceMade = false;

        // Ensure modal exists
        EnsureRoomUpgradeModal(chosenThree, () => choiceMade = true);

        if (roomUpgradeModal) roomUpgradeModal.SetActive(true);

        // Wait for player to choose
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

        if (roomUpgradeModal == null)
        {
            // Build modal container
            roomUpgradeModal = new GameObject("RoomUpgradeModal");
            roomUpgradeModal.transform.SetParent(canvas.transform, false);

            var rt = roomUpgradeModal.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bg = roomUpgradeModal.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.03f, 0.06f, 0.58f); // Soft cinematic vignette
        }

        // Clear existing children
        for (int i = roomUpgradeModal.transform.childCount - 1; i >= 0; i--)
        {
            Destroy(roomUpgradeModal.transform.GetChild(i).gameObject);
        }

        // Title
        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(roomUpgradeModal.transform, false);
        var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "ROOM CLEARED!\n<size=18><color=#FCD34D>CHOOSE AN UPGRADE FOR YOUR SQUAD</color></size>";
        titleTmp.fontSize = 26;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.1f, 0.80f);
        titleRt.anchorMax = new Vector2(0.9f, 0.98f);
        titleRt.offsetMin = Vector2.zero;
        titleRt.offsetMax = Vector2.zero;

        // 3 Cards Layout
        float cardWidth = 240f;
        float cardHeight = 310f;
        float spacing = 20f;
        float totalWidth = (cardWidth * 3) + (spacing * 2);
        float startX = -totalWidth * 0.5f + cardWidth * 0.5f;

        for (int i = 0; i < 3; i++)
        {
            var opt = options[i];
            var cardGo = new GameObject($"Card_{i}");
            cardGo.transform.SetParent(roomUpgradeModal.transform, false);

            var cardRt = cardGo.AddComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(cardWidth, cardHeight);
            cardRt.anchoredPosition = new Vector2(startX + i * (cardWidth + spacing), 40f);

            var cardImg = cardGo.AddComponent<Image>();
            cardImg.color = new Color(0.14f, 0.16f, 0.24f, 0.98f);

            // Icon & Title
            var cardTextGo = new GameObject("CardText");
            cardTextGo.transform.SetParent(cardGo.transform, false);
            var cardTmp = cardTextGo.AddComponent<TextMeshProUGUI>();
            cardTmp.text = $"<size=42>{opt.icon}</size>\n\n<color=#FCD34D><b>{opt.title}</b></color>\n\n<size=14><color=#E2E8F0>{opt.description}</color></size>";
            cardTmp.fontSize = 17;
            cardTmp.alignment = TextAlignmentOptions.Center;
            var textRt = cardTextGo.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.08f, 0.28f);
            textRt.anchorMax = new Vector2(0.92f, 0.92f);
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            // Select Button
            var btnGo = new GameObject("SelectBtn");
            btnGo.transform.SetParent(cardGo.transform, false);
            var btnRt = btnGo.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.12f, 0.08f);
            btnRt.anchorMax = new Vector2(0.88f, 0.24f);
            btnRt.offsetMin = Vector2.zero;
            btnRt.offsetMax = Vector2.zero;

            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.2f, 0.65f, 0.35f, 1f);

            var btn = btnGo.AddComponent<Button>();
            var btnTextGo = new GameObject("BtnText");
            btnTextGo.transform.SetParent(btnGo.transform, false);
            var btnTmp = btnTextGo.AddComponent<TextMeshProUGUI>();
            btnTmp.text = "SELECT";
            btnTmp.fontSize = 16;
            btnTmp.fontStyle = FontStyles.Bold;
            btnTmp.alignment = TextAlignmentOptions.Center;
            btnTmp.color = Color.white;
            var btnTextRt = btnTextGo.GetComponent<RectTransform>();
            btnTextRt.anchorMin = Vector2.zero;
            btnTextRt.anchorMax = Vector2.one;
            btnTextRt.offsetMin = Vector2.zero;
            btnTextRt.offsetMax = Vector2.zero;

            // Button Callback
            btn.onClick.AddListener(() =>
            {
                opt.onSelect?.Invoke();
                if (mainCamera != null)
                {
                    CombatFeedback.Instance.ShowText(mainCamera.transform.position + Vector3.forward * 10f, $"UPGRADE: {opt.title}!", Color.yellow, 4.2f);
                }
                onChosen?.Invoke();
            });
        }
    }

    // =========================================================================
    // REPOSITION ("REPO") MODAL
    // =========================================================================
    void SetupRepoButton()
    {
        if (repoButton == null && pauseButton != null)
        {
            // Dynamically create a sleek REPO button next to speed button on HUD
            var parent = pauseButton.transform.parent;
            if (parent != null)
            {
                var go = new GameObject("RepoButton", typeof(RectTransform));
                go.transform.SetParent(parent, false);

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-205f, -45f);
                rt.sizeDelta = new Vector2(65f, 45f);

                var img = go.AddComponent<Image>();
                img.color = new Color(0.22f, 0.45f, 0.85f, 0.95f);

                repoButton = go.AddComponent<Button>();

                var textGo = new GameObject("Label", typeof(RectTransform));
                textGo.transform.SetParent(go.transform, false);
                var textRt = textGo.GetComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.sizeDelta = Vector2.zero;

                var tmp = textGo.AddComponent<TextMeshProUGUI>();
                tmp.text = "<b>REPO</b>";
                tmp.fontSize = 15;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                tmp.raycastTarget = false;
            }
        }

        if (repoButton != null)
        {
            repoButton.onClick.AddListener(OpenRepositionModal);
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

        if (mainCamera != null)
        {
            CombatFeedback.Instance.ShowText(mainCamera.transform.position + Vector3.forward * 10f, "FORMATION UPDATED!", Color.cyan, 3.5f);
        }
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
    void SetupBossRoom()
    {
        if (bossHpRoot) bossHpRoot.SetActive(true);
        bossCurrentHP = bossMaxHP;
        bossHitCount = 0;
        if (bossNameLabel) bossNameLabel.text = "SKELETON KING";
        if (bossHpBar)
        {
            bossHpBar.maxValue = bossMaxHP;
            bossHpBar.value = bossCurrentHP;
        }
        UpdateBossHitCounter();
    }

    public void DamageBoss(int dmg)
    {
        bossCurrentHP = Mathf.Max(0, bossCurrentHP - dmg);
        if (bossHpBar) bossHpBar.value = bossCurrentHP;
        if (bossCurrentHP <= 0)
        {
            foreach (var e in enemyAgents)
                if (e.RoomIndex == currentRoomIndex)
                    e.Kill();
        }
        else
        {
            RegisterBossHit();
        }
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
        if (pausePanel) pausePanel.SetActive(isPaused);
        Time.timeScale = isPaused ? 0f : (isDoubleSpeed ? 2f : 1f);
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
        if (heroDetailPanel) heroDetailPanel.SetActive(true);
        if (detailHeroName) detailHeroName.text = hero.displayName;
        if (detailHP) detailHP.text = $"HP: {hero.currentHP}/{hero.data.maxHP}";
        if (detailEnergy) detailEnergy.text = $"Energy: {hero.currentEnergy}/{hero.data.maxEnergy}";
        string upgrades = hero.upgrades.Count > 0 ?
            "• " + string.Join("\n• ", hero.upgrades) : "(no upgrades yet)";
        if (detailUpgrades) detailUpgrades.text = $"Upgrades:\n{upgrades}";
    }

    void CloseDetailPanel()
    {
        if (heroDetailPanel) heroDetailPanel.SetActive(false);
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
    public void RefreshPortraits()
    {
        foreach (var p in portraits) p.Refresh();
    }

    void RefreshGold()
    {
        if (goldLabel && GameRunData.Instance != null)
            goldLabel.text = $"GOLD: {GameRunData.Instance.gold}";
    }

    void UpdateFloorLabel()
    {
        if (floorLabel && GameRunData.Instance != null)
            floorLabel.text = $"Floor {GameRunData.Instance.currentRoom + 1}";
    }

    void UpdateRoomLabel()
    {
        if (roomLabel == null) return;
        var rooms = dungeonGenerator.Rooms;
        bool isBoss = rooms != null && currentRoomIndex < rooms.Count && rooms[currentRoomIndex].isBossRoom;
        roomLabel.text = isBoss ?
            $"BOSS ROOM" :
            $"Room {currentRoomIndex + 1}/{rooms?.Count ?? 0}";
    }
}
