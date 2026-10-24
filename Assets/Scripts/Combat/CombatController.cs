using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Main combat HUD controller — autobattle loop, portrait panel,
/// healer priority, reposition, speed toggle, pause.
/// </summary>
public class CombatController : MonoBehaviour, ICombatController
{
    [Header("Arena")]
    public Transform arenaContainer;        // parent for hero/enemy combat agents
    public GameObject heroCombatantPrefab;
    public GameObject enemyPrefab;
    public TextMeshProUGUI roomLabel;

    [Header("Top Bar")]
    public TextMeshProUGUI goldLabel;
    public Button pauseButton;
    public Button speedButton;
    public TextMeshProUGUI speedLabel;
    public GameObject pausePanel;
    public Button resumeButton;
    public Button quitButton;
    public GameObject minimapRoot;

    [Header("Bottom Portrait Panel")]
    public Transform portraitContainer;     // parent for 5 portrait UIs
    public GameObject portraitPrefab;

    [Header("Right Edge Buttons")]
    public Button repoButton;
    public GameObject repoPanel;            // Reposition overlay
    public Transform repoPanelGrid;         // 3x3 grid inside reposition panel
    public Button repoConfirmBtn;
    public Button repoCancelBtn;

    [Header("Healer Priority Panel")]
    public GameObject healerPriorityPanel;
    public Transform healerTargetContainer;
    public GameObject healerTargetCardPrefab;
    public Button healerConfirmBtn;

    [Header("Hero Detail Panel")]
    public GameObject heroDetailPanel;
    public TextMeshProUGUI detailHeroName;
    public TextMeshProUGUI detailHP;
    public TextMeshProUGUI detailEnergy;
    public TextMeshProUGUI detailUpgrades;
    public Button detailBackBtn;

    [Header("Boss HP")]
    public GameObject bossHpRoot;
    public Slider bossHpBar;
    public TextMeshProUGUI bossNameLabel;
    public TextMeshProUGUI bossHitCounter;

    // ---- Private State ----
    private List<HeroCombatAgent> heroAgents = new();
    private List<EnemyCombatAgent> enemyAgents = new();
    private List<PortraitUI> portraits = new();

    private bool isPaused = false;
    private bool isDoubleSpeed = false;
    private bool repoUsed = false;
    private HeroInstance pendingHealerTarget;
    private int selectedHealerTargetIndex = -1;

    // Boss fight state
    private int bossMaxHP = 500;
    private int bossCurrentHP;
    private int bossHitCount = 0;
    private const int BOSS_HIT_LIMIT = 3;

    void Start()
    {
        GameRunData.Instance?.EnsureSquadAndFormation();
        SetupButtons();
        BuildPortraits();
        SpawnArenaEntities();
        StartCoroutine(CombatLoop());
        RefreshGold();
        UpdateRoomLabel();
    }

    void SetupButtons()
    {
        pauseButton.onClick.AddListener(TogglePause);
        resumeButton.onClick.AddListener(TogglePause);
        quitButton.onClick.AddListener(() => SceneNav.GoTo(SceneNames.MainMenu));
        speedButton.onClick.AddListener(ToggleSpeed);
        repoButton.onClick.AddListener(OpenReposition);
        repoConfirmBtn.onClick.AddListener(ConfirmReposition);
        repoCancelBtn.onClick.AddListener(CloseReposition);
        healerConfirmBtn.onClick.AddListener(ConfirmHealerPriority);
        detailBackBtn.onClick.AddListener(CloseDetailPanel);

        pausePanel.SetActive(false);
        repoPanel.SetActive(false);
        healerPriorityPanel.SetActive(false);
        heroDetailPanel.SetActive(false);

        bool isBoss = GameRunData.Instance?.IsBossRoom ?? false;
        bossHpRoot.SetActive(isBoss);
        if (isBoss)
        {
            bossCurrentHP = bossMaxHP;
            bossNameLabel.text = "SKELETON KING";
            if (bossHpBar) { bossHpBar.maxValue = bossMaxHP; bossHpBar.value = bossCurrentHP; }
            UpdateBossHitCounter();
        }
    }

    // ---- Portrait Panel ----
    void BuildPortraits()
    {
        if (GameRunData.Instance == null) return;
        foreach (var hero in GameRunData.Instance.squad)
        {
            var go = Instantiate(portraitPrefab, portraitContainer);
            var p = go.GetComponent<PortraitUI>();
            p.Setup(hero, OnPortraitTapped);
            portraits.Add(p);
        }
    }

    void OnPortraitTapped(HeroInstance hero)
    {
        if (!hero.isAlive) return;

        if (hero.data.heroClass == HeroClass.Healer)
            OpenHealerPriority();
        else if (hero.isFatigued)
            TriggerEmergencyRoll(hero);
        else
            OpenHeroDetail(hero);
    }

    // ---- Arena Spawn ----
    void SpawnArenaEntities()
    {
        if (GameRunData.Instance == null) return;

        var formation = GameRunData.Instance.formation;
        for (int i = 0; i < 9; i++)
        {
            if (formation[i] == null) continue;
            var go = Instantiate(heroCombatantPrefab, arenaContainer);
            var agent = go.GetComponent<HeroCombatAgent>();
            agent.Init(formation[i], i, this);
            if (formation[i].data.heroClass == HeroClass.Healer)
            {
                var healer = go.GetComponent<HealerCombatAgent>() ?? go.AddComponent<HealerCombatAgent>();
                healer.Init(this);
            }
            heroAgents.Add(agent);
        }

        // Spawn a wave of enemies
        bool isBoss = GameRunData.Instance.IsBossRoom;
        int enemyCount = isBoss ? 1 : Random.Range(3, 7);
        for (int i = 0; i < enemyCount; i++)
        {
            var go = Instantiate(enemyPrefab, arenaContainer);
            var e = go.GetComponent<EnemyCombatAgent>();
            e.Init(isBoss, this);
            enemyAgents.Add(e);
        }
    }

    // ---- Combat Loop ----
    IEnumerator CombatLoop()
    {
        while (true)
        {
            if (isPaused) { yield return null; continue; }

            // Check win/lose
            bool heroesWiped = heroAgents.TrueForAll(a => !a.IsAlive);
            bool enemiesWiped = enemyAgents.TrueForAll(a => !a.IsAlive);

            if (heroesWiped)
            {
                yield return new WaitForSeconds(1f);
                SceneNav.GoTo(SceneNames.GameOver);
                yield break;
            }

            if (enemiesWiped)
            {
                yield return new WaitForSeconds(0.5f);
                GameRunData.Instance.currentRoom++;
                if (GameRunData.Instance.currentRoom >= GameRunData.Instance.totalRooms)
                    SceneNav.GoTo(SceneNames.GameOver); // Victory
                else
                    SceneNav.GoTo(SceneNames.Rewards);
                yield break;
            }

            RefreshPortraits();
            yield return new WaitForSeconds(isDoubleSpeed ? 0.1f : 0.2f);
        }
    }

    // ---- Pause ----
    void TogglePause()
    {
        isPaused = !isPaused;
        pausePanel.SetActive(isPaused);
        Time.timeScale = isPaused ? 0f : (isDoubleSpeed ? 2f : 1f);
    }

    // ---- Speed Toggle ----
    void ToggleSpeed()
    {
        isDoubleSpeed = !isDoubleSpeed;
        Time.timeScale = isPaused ? 0f : (isDoubleSpeed ? 2f : 1f);
        speedLabel.text = isDoubleSpeed ? "x1" : "x2";
    }

    // ---- Reposition ----
    void OpenReposition()
    {
        if (repoUsed) return;
        isPaused = true;
        Time.timeScale = 0f;
        repoPanel.SetActive(true);

        // Build the repo grid
        BuildRepoPanelGrid();
    }

    void BuildRepoPanelGrid()
    {
        foreach (Transform t in repoPanelGrid) Destroy(t.gameObject);

        var formation = GameRunData.Instance.formation;
        for (int i = 0; i < 9; i++)
        {
            var go = new GameObject($"RepoSlot_{i}", typeof(RectTransform));
            go.transform.SetParent(repoPanelGrid, false);

            var bg = go.AddComponent<Image>();
            bg.color = formation[i] == null ?
                new Color(0.1f, 0.1f, 0.15f) :
                (formation[i].isFatigued ? new Color(0.8f, 0.2f, 0.1f) : new Color(0.2f, 0.5f, 0.8f));

            var lbl = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            lbl.transform.SetParent(go.transform, false);
            lbl.GetComponent<TextMeshProUGUI>().text =
                formation[i] == null ? "" : formation[i].displayName;
        }
    }

    void ConfirmReposition()
    {
        repoUsed = true;
        repoButton.interactable = false;
        CloseReposition();
    }

    void CloseReposition()
    {
        repoPanel.SetActive(false);
        isPaused = false;
        Time.timeScale = isDoubleSpeed ? 2f : 1f;
    }

    // ---- Healer Priority ----
    void OpenHealerPriority()
    {
        isPaused = true;
        Time.timeScale = 0f;
        healerPriorityPanel.SetActive(true);
        selectedHealerTargetIndex = GameRunData.Instance?.healerPriorityIndex ?? -1;

        foreach (Transform t in healerTargetContainer) Destroy(t.gameObject);

        var squad = GameRunData.Instance?.squad;
        if (squad == null) return;

        for (int i = 0; i < squad.Count; i++)
        {
            var hero = squad[i];
            if (hero.data.heroClass == HeroClass.Healer) continue;

            int idx = i;
            var go = Instantiate(healerTargetCardPrefab, healerTargetContainer);
            var card = go.GetComponent<HeroCardUI>();
            card.Setup(hero, (_) => SelectHealerTarget(idx));

            // Highlight if currently selected
            if (i == selectedHealerTargetIndex)
                card.cardBackground.color = Color.yellow;
        }
    }

    void SelectHealerTarget(int index)
    {
        selectedHealerTargetIndex = index;
        // Refresh colors
        int ci = 0;
        foreach (Transform t in healerTargetContainer)
        {
            var img = t.GetComponent<Image>();
            if (img) img.color = (ci == index) ? Color.yellow : Color.white;
            ci++;
        }
    }

    void ConfirmHealerPriority()
    {
        if (GameRunData.Instance != null)
            GameRunData.Instance.healerPriorityIndex = selectedHealerTargetIndex;
        healerPriorityPanel.SetActive(false);
        isPaused = false;
        Time.timeScale = isDoubleSpeed ? 2f : 1f;
    }

    // ---- Hero Detail ----
    void OpenHeroDetail(HeroInstance hero)
    {
        heroDetailPanel.SetActive(true);
        detailHeroName.text = hero.displayName;
        detailHP.text = $"HP: {hero.currentHP}/{hero.data.maxHP}";
        detailEnergy.text = $"Energy: {hero.currentEnergy}/{hero.data.maxEnergy}";
        string upgrades = hero.upgrades.Count > 0 ?
            string.Join("\n", hero.upgrades) : "(no upgrades yet)";
        detailUpgrades.text = $"Upgrades:\n{upgrades}";
    }

    void CloseDetailPanel()
    {
        heroDetailPanel.SetActive(false);
    }

    // ---- Emergency Roll ----
    void TriggerEmergencyRoll(HeroInstance hero)
    {
        // Find the hero's combat agent and trigger dodge
        foreach (var agent in heroAgents)
        {
            if (agent.heroInstance == hero)
            {
                agent.TriggerDodgeRoll();
                break;
            }
        }
    }

    // ---- Boss ----
    public void RegisterBossHit()
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
        // Spawn skeleton minions
        for (int i = 0; i < 4; i++)
        {
            var go = Instantiate(enemyPrefab, arenaContainer);
            var e = go.GetComponent<EnemyCombatAgent>();
            e.Init(false, this);
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
                skulls += i < bossHitCount ? "[X]" : "[ ]";
            bossHitCounter.text = "Minion Summon: " + skulls;
        }
    }

    public void DamageBoss(int dmg)
    {
        bossCurrentHP = Mathf.Max(0, bossCurrentHP - dmg);
        if (bossHpBar) bossHpBar.value = bossCurrentHP;
        if (bossCurrentHP <= 0)
        {
            // Boss dead, mark all enemies dead
            foreach (var e in enemyAgents) e.Kill();
        }
        else
        {
            RegisterBossHit();
        }
    }

    // ---- Helpers ----
    public void RefreshPortraits()
    {
        foreach (var p in portraits) p.Refresh();
    }

    void RefreshGold()
    {
        if (goldLabel && GameRunData.Instance != null)
            goldLabel.text = $"GOLD: {GameRunData.Instance.gold}";
    }

    void UpdateRoomLabel()
    {
        if (roomLabel && GameRunData.Instance != null)
        {
            bool isBoss = GameRunData.Instance.IsBossRoom;
            roomLabel.text = isBoss ?
                $"FLOOR {GameRunData.Instance.currentRoom + 1} - BOSS" :
                $"Room {GameRunData.Instance.currentRoom + 1}/{GameRunData.Instance.totalRooms}";
        }
    }

    public List<HeroCombatAgent> HeroAgents => heroAgents;
    public List<EnemyCombatAgent> EnemyAgents => enemyAgents;
    public bool IsPaused => isPaused;
    public bool IsInCombatPhase => true;
}