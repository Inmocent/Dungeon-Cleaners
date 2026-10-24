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

        if (pausePanel) pausePanel.SetActive(false);
        if (heroDetailPanel) heroDetailPanel.SetActive(false);
        if (bossHpRoot) bossHpRoot.SetActive(false);
        if (roomUpgradeModal) roomUpgradeModal.SetActive(false);
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

        // Command all alive heroes to assemble in formation on the left side of the room
        Vector2 squadTarget = new Vector2(roomCenter.x - 1.2f, roomCenter.y);
        foreach (var hero in heroAgents)
        {
            if (!hero.IsAlive) continue;
            hero.MoveTo(squadTarget);
        }

        // Wait until heroes arrive or reasonable timeout
        float timeout = 8f;
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
                        if (h.HeroClass == HeroClass.Knight) h.heroInstance.data.attackDamage = Mathf.RoundToInt(h.heroInstance.data.attackDamage * 1.25f);
                }
            },
            new RoomUpgradeOption
            {
                title = "ARCANE PYRE",
                description = "+30% Fireball spell power for Mages",
                icon = "<color=#F97316><b>[ FIRE ]</b></color>",
                onSelect = () => {
                    foreach (var h in heroAgents)
                        if (h.HeroClass == HeroClass.Mage) h.heroInstance.data.attackDamage = Mathf.RoundToInt(h.heroInstance.data.attackDamage * 1.3f);
                }
            },
            new RoomUpgradeOption
            {
                title = "DIVINE BLESSING",
                description = "Heals all squad heroes +30 HP immediately",
                icon = "<color=#10B981><b>[ HEAL ]</b></color>",
                onSelect = () => {
                    foreach (var h in heroAgents)
                        if (h.IsAlive) h.ReceiveHeal(30);
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
                        h.heroInstance.data.moveSpeed *= 1.2f;
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
        var canvas = FindObjectOfType<Canvas>();
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
            string.Join("\n", hero.upgrades) : "(no upgrades yet)";
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
