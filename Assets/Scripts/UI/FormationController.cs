using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Formation Grid screen — 3x3 drag-drop and tap-to-swap placement before battle.
/// Provides enlarged mobile touch ergonomics, responsive back navigation, and right-side tactical panel.
/// </summary>
public class FormationController : MonoBehaviour
{
    [Header("Grid")]
    public Transform gridContainer;         // parent with 9 GridSlotUI children
    public GameObject gridSlotPrefab;

    [Header("Bench")]
    public Transform benchContainer;        // left sidebar bench cards
    public GameObject benchCardPrefab;

    [Header("Stats Panel")]
    public TextMeshProUGUI statsHeroName;
    public TextMeshProUGUI statsHP;
    public TextMeshProUGUI statsEnergy;
    public TextMeshProUGUI statsAttack;
    public TextMeshProUGUI statsClass;
    public GameObject statsPanel;

    [Header("Buttons")]
    public Button battleButton;
    public Button backButton;
    public TextMeshProUGUI roomLabel;

    [Header("Quick Action Buttons (Optional)")]
    public Button autoDeployButton;
    public Button clearGridButton;
    public Button unassignSelectedButton;

    private GridSlotUI[] gridSlots = new GridSlotUI[9];
    private List<HeroInstance> benchHeroes = new();
    private HeroInstance[] formation = new HeroInstance[9];

    // Tap-to-swap selection state
    private int selectedSlotIndex = -1;

    // Battle lock feedback pill
    private GameObject battleLockPill;
    private TextMeshProUGUI battleLockPillText;

    void Start()
    {
        Time.timeScale = 1f;

        if (GameRunData.Instance != null && GameRunData.Instance.squad.Count == 0)
        {
            GameRunData.Instance.EnsureSquadAndFormation();
        }

        if (battleButton != null)
        {
            battleButton.interactable = true; // remains tappable so locked feedback can fire
            battleButton.onClick.AddListener(OnBattle);
        }

        if (backButton != null)
        {
            backButton.onClick.AddListener(OnBack);
        }

        if (autoDeployButton != null) autoDeployButton.onClick.AddListener(AutoDeploy);
        if (clearGridButton != null) clearGridButton.onClick.AddListener(ClearGrid);
        if (unassignSelectedButton != null) unassignSelectedButton.onClick.AddListener(UnassignSelected);

        if (roomLabel != null && GameRunData.Instance != null)
        {
            roomLabel.text = $"Room {GameRunData.Instance.currentRoom + 1}/{GameRunData.Instance.totalRooms}";
        }

        if (statsPanel != null) statsPanel.SetActive(false);

        BuildGrid();
        LoadBench();
        RestoreOrAutoDeploy();
        CreateBattleLockPill();
    }

    void OnBack()
    {
        if (GameRunData.Instance != null && GameRunData.Instance.currentRoom > 0)
        {
            SceneNav.GoTo(SceneNames.Rewards);
        }
        else
        {
            SceneNav.GoTo(SceneNames.SquadBuilder);
        }
    }

    void RestoreOrAutoDeploy()
    {
        if (GameRunData.Instance == null) return;

        bool hadAny = false;
        for (int i = 0; i < 9; i++)
        {
            var h = GameRunData.Instance.formation[i];
            if (h != null && GameRunData.Instance.squad.Contains(h))
            {
                PlaceHero(h, i);
                hadAny = true;
            }
        }

        if (!hadAny && GameRunData.Instance.squad.Count > 0)
        {
            AutoDeploy();
        }

        RefreshBenchCardsVisibility();
        CheckBattleReady();
    }

    void BuildGrid()
    {
        for (int i = 0; i < 9; i++)
        {
            var go = Instantiate(gridSlotPrefab, gridContainer);
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.minWidth = 160f; le.minHeight = 160f;
            le.preferredWidth = 160f; le.preferredHeight = 160f;
            var rt = go.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(160f, 160f);
            var slot = go.GetComponent<GridSlotUI>();
            int idx = i;
            slot.Setup(idx, null, OnGridSlotTapped, OnHeroDrop);
            gridSlots[i] = slot;
        }
    }

    void LoadBench()
    {
        if (GameRunData.Instance == null) return;
        benchHeroes.Clear();

        foreach (var hero in GameRunData.Instance.squad)
        {
            benchHeroes.Add(hero);
            var go = Instantiate(benchCardPrefab, benchContainer);
            var card = go.GetComponent<HeroCardUI>();
            card.Setup(hero, (c) => OnBenchCardTapped(c));
        }
    }

    // Tap a bench card -> place into selected slot or first empty slot
    void OnBenchCardTapped(HeroCardUI card)
    {
        if (card == null || card.heroInstance == null) return;

        int targetSlot = -1;
        if (selectedSlotIndex >= 0 && selectedSlotIndex < 9)
        {
            targetSlot = selectedSlotIndex;
        }
        else
        {
            for (int i = 0; i < 9; i++)
            {
                if (formation[i] == null)
                {
                    targetSlot = i;
                    break;
                }
            }
        }

        if (targetSlot != -1)
        {
            PlaceHero(card.heroInstance, targetSlot);
            ClearSelection();
            RefreshBenchCardsVisibility();
            CheckBattleReady();
        }
    }

    void PlaceHero(HeroInstance hero, int slotIndex)
    {
        // If hero was previously in another slot, clear that slot
        int existingSlot = System.Array.IndexOf(formation, hero);
        if (existingSlot >= 0 && existingSlot != slotIndex)
        {
            formation[existingSlot] = null;
            gridSlots[existingSlot].Setup(existingSlot, null, OnGridSlotTapped, OnHeroDrop);
        }

        formation[slotIndex] = hero;
        hero.gridIndex = slotIndex;
        gridSlots[slotIndex].Setup(slotIndex, hero, OnGridSlotTapped, OnHeroDrop);
    }

    // Tap slot -> tap-to-select and tap-to-swap
    void OnGridSlotTapped(int slotIndex)
    {
        if (selectedSlotIndex == -1)
        {
            // Nothing selected yet: select this slot if it has a hero
            if (formation[slotIndex] != null)
            {
                selectedSlotIndex = slotIndex;
                gridSlots[slotIndex].SetSelected(true);
                ShowStats(formation[slotIndex]);
            }
            else
            {
                if (statsPanel != null) statsPanel.SetActive(false);
            }
        }
        else if (selectedSlotIndex == slotIndex)
        {
            // Tapped same slot: deselect
            ClearSelection();
            if (statsPanel != null) statsPanel.SetActive(false);
        }
        else
        {
            // Tapped different slot: SWAP slot A and slot B!
            SwapSlots(selectedSlotIndex, slotIndex);
            ClearSelection();
        }
    }

    void OnHeroDrop(int fromSlot, int toSlot)
    {
        SwapSlots(fromSlot, toSlot);
        ClearSelection();
    }

    void SwapSlots(int fromSlot, int toSlot)
    {
        var tmp = formation[toSlot];
        formation[toSlot] = formation[fromSlot];
        formation[fromSlot] = tmp;

        if (formation[fromSlot] != null) formation[fromSlot].gridIndex = fromSlot;
        if (formation[toSlot] != null) formation[toSlot].gridIndex = toSlot;

        gridSlots[fromSlot].Setup(fromSlot, formation[fromSlot], OnGridSlotTapped, OnHeroDrop);
        gridSlots[toSlot].Setup(toSlot, formation[toSlot], OnGridSlotTapped, OnHeroDrop);

        RefreshBenchCardsVisibility();
        CheckBattleReady();
    }

    void ClearSelection()
    {
        if (selectedSlotIndex >= 0 && selectedSlotIndex < gridSlots.Length)
        {
            if (gridSlots[selectedSlotIndex] != null)
            {
                gridSlots[selectedSlotIndex].SetSelected(false);
            }
        }
        selectedSlotIndex = -1;
    }

    public void UnassignSelected()
    {
        if (selectedSlotIndex < 0 || selectedSlotIndex >= 9) return;
        if (formation[selectedSlotIndex] == null) return;

        formation[selectedSlotIndex] = null;
        gridSlots[selectedSlotIndex].Setup(selectedSlotIndex, null, OnGridSlotTapped, OnHeroDrop);
        ClearSelection();
        if (statsPanel != null) statsPanel.SetActive(false);

        RefreshBenchCardsVisibility();
        CheckBattleReady();
    }

    public void AutoDeploy()
    {
        ClearGrid();
        if (GameRunData.Instance == null || GameRunData.Instance.squad.Count == 0) return;

        // Tactical placement: Knights front (2, 8, 5), Healer center (4), Mages back (0, 6, 1)
        int[] defaultSlots = new int[] { 2, 8, 4, 0, 6, 1, 3, 5, 7 };
        int slotIdx = 0;
        foreach (var hero in GameRunData.Instance.squad)
        {
            if (slotIdx < defaultSlots.Length)
            {
                PlaceHero(hero, defaultSlots[slotIdx]);
                slotIdx++;
            }
        }

        ClearSelection();
        RefreshBenchCardsVisibility();
        CheckBattleReady();
    }

    public void ClearGrid()
    {
        for (int i = 0; i < 9; i++)
        {
            formation[i] = null;
            if (gridSlots[i] != null)
            {
                gridSlots[i].Setup(i, null, OnGridSlotTapped, OnHeroDrop);
            }
        }
        ClearSelection();
        if (statsPanel != null) statsPanel.SetActive(false);
        RefreshBenchCardsVisibility();
        CheckBattleReady();
    }

    void RefreshBenchCardsVisibility()
    {
        if (benchContainer == null) return;
        foreach (Transform child in benchContainer)
        {
            var card = child.GetComponent<HeroCardUI>();
            if (card != null && card.heroInstance != null)
            {
                bool isPlaced = System.Array.IndexOf(formation, card.heroInstance) >= 0;
                card.gameObject.SetActive(!isPlaced);
            }
        }
    }

    void ShowStats(HeroInstance hero)
    {
        if (statsPanel != null) statsPanel.SetActive(true);
        if (statsHeroName) statsHeroName.text = hero.displayName;
        if (statsHP) statsHP.text = $"HP: {hero.currentHP}/{hero.data.maxHP}";
        if (statsEnergy) statsEnergy.text = $"Energy: {hero.currentEnergy}/{hero.data.maxEnergy}";
        if (statsAttack) statsAttack.text = $"ATK: {hero.data.attackDamage}";
        if (statsClass) statsClass.text = $"Class: {hero.data.heroClass}";
    }

    void CheckBattleReady()
    {
        int filled = 0;
        foreach (var h in formation) if (h != null) filled++;
        int needed = GameRunData.Instance?.squad.Count ?? 0;
        bool ready = filled == needed;

        if (battleButton != null)
        {
            // Never disable: a dead button swallows taps silently. Tint + feedback instead.
            var btnImg = battleButton.GetComponent<UnityEngine.UI.Image>();
            if (btnImg != null)
                btnImg.color = ready ? new Color(0.24f, 0.68f, 0.38f, 1f) : new Color(0.38f, 0.40f, 0.46f, 1f);
        }

        UpdateBattleLockPill(ready, filled, needed);
    }

    void CreateBattleLockPill()
    {
        if (battleButton == null) return;

        var parent = battleButton.transform.parent;
        if (parent == null) return;

        battleLockPill = new GameObject("BattleLockPill", typeof(RectTransform), typeof(CanvasGroup));
        battleLockPill.transform.SetParent(parent, false);

        var rt = battleLockPill.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(240f, 40f);

        var img = battleLockPill.AddComponent<Image>();
        img.color = new Color(0.78f, 0.24f, 0.26f, 0.9f);
        img.sprite = DungeonUITheme.Instance?.cardFrame;
        img.type = Image.Type.Sliced;

        battleLockPillText = new GameObject("PillText", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        battleLockPillText.transform.SetParent(battleLockPill.transform, false);
        var textRt = battleLockPillText.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        battleLockPillText.alignment = TextAlignmentOptions.Center;
        battleLockPillText.fontSize = 16f;
        battleLockPillText.fontStyle = FontStyles.Bold;
        DungeonUITheme.ApplyCtaText(battleLockPillText);

        var cg = battleLockPill.GetComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;
    }

    void UpdateBattleLockPill(bool ready, int filled, int needed)
    {
        if (battleLockPill == null || battleLockPillText == null) return;

        var cg = battleLockPill.GetComponent<CanvasGroup>();
        if (cg == null) return;

        if (!ready && needed > 0)
        {
            int missing = needed - filled;
            battleLockPillText.text = $"BATTLE LOCKED — Place {missing} more hero{(missing > 1 ? "s" : "")}";
            cg.alpha = 1f;
        }
        else
        {
            cg.alpha = 0f;
        }
    }

    private UnityEngine.Coroutine battleLockedFeedbackRoutine;

    void OnBattle()
    {
        if (GameRunData.Instance == null) return;

        int filled = 0;
        foreach (var h in formation) if (h != null) filled++;
        int needed = GameRunData.Instance.squad.Count;
        if (filled != needed || needed == 0)
        {
            if (battleLockedFeedbackRoutine != null) StopCoroutine(battleLockedFeedbackRoutine);
            battleLockedFeedbackRoutine = StartCoroutine(BattleLockedFeedbackRoutine(filled, needed));
            return;
        }

        GameRunData.Instance.formation = (HeroInstance[])formation.Clone();
        SceneNav.GoTo(SceneNames.Combat);
    }

    private System.Collections.IEnumerator BattleLockedFeedbackRoutine(int filled, int needed)
    {
        var btnImg = battleButton != null ? battleButton.GetComponent<UnityEngine.UI.Image>() : null;
        var label = battleButton != null ? battleButton.GetComponentInChildren<TextMeshProUGUI>() : null;
        var origCol = btnImg != null ? btnImg.color : Color.white;
        var origText = label != null ? label.text : string.Empty;

        int missing = Mathf.Max(0, needed - filled);
        if (btnImg != null) btnImg.color = new Color(0.55f, 0.55f, 0.58f, 1f);
        if (label != null) label.text = missing > 0 ? $"Place {missing} more" : "Place Heroes";

        // Horizontal nudge (0.15s right, 0.15s back) to signal "not yet"
        var btnRt = battleButton != null ? battleButton.transform as RectTransform : null;
        if (btnRt != null)
        {
            Vector2 origPos = btnRt.anchoredPosition;
            float half = 0.15f, dist = 18f, t = 0f;
            while (t < half) { t += Time.unscaledDeltaTime; btnRt.anchoredPosition = origPos + new Vector2(Mathf.Sin(Mathf.Min(1f, t / half) * Mathf.PI) * dist, 0f); yield return null; }
            t = 0f;
            while (t < half) { t += Time.unscaledDeltaTime; btnRt.anchoredPosition = origPos - new Vector2(Mathf.Sin(Mathf.Min(1f, t / half) * Mathf.PI) * dist, 0f); yield return null; }
            btnRt.anchoredPosition = origPos;
        }

        yield return new WaitForSecondsRealtime(2f);

        if (btnImg != null) btnImg.color = origCol;
        if (label != null) label.text = origText;
        CheckBattleReady();
        battleLockedFeedbackRoutine = null;
    }
}
