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

    void Start()
    {
        Time.timeScale = 1f;

        if (GameRunData.Instance != null && GameRunData.Instance.squad.Count == 0)
        {
            GameRunData.Instance.EnsureSquadAndFormation();
        }

        if (battleButton != null)
        {
            battleButton.interactable = false;
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
        if (battleButton != null)
        {
            battleButton.interactable = filled == GameRunData.Instance?.squad.Count;
        }
    }

    void OnBattle()
    {
        if (GameRunData.Instance == null) return;
        GameRunData.Instance.formation = (HeroInstance[])formation.Clone();
        SceneNav.GoTo(SceneNames.Combat);
    }
}
