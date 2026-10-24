using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Formation Grid screen — 3x3 drag-drop placement before battle.
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

    private GridSlotUI[] gridSlots = new GridSlotUI[9];
    private List<HeroInstance> benchHeroes = new();
    private HeroInstance[] formation = new HeroInstance[9];

    void Start()
    {
        Time.timeScale = 1f;

        if (GameRunData.Instance != null && GameRunData.Instance.squad.Count == 0)
        {
            GameRunData.Instance.EnsureSquadAndFormation();
        }

        battleButton.interactable = false;
        backButton.onClick.AddListener(() => SceneNav.GoTo(SceneNames.SquadBuilder));
        battleButton.onClick.AddListener(OnBattle);

        if (roomLabel && GameRunData.Instance != null)
            roomLabel.text = $"Room {GameRunData.Instance.currentRoom + 1}/{GameRunData.Instance.totalRooms}";

        statsPanel.SetActive(false);
        BuildGrid();
        LoadBench();
        RestoreOrAutoDeploy();
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
            int[] defaultSlots = new int[] { 0, 6, 4, 2, 8 };
            for (int s = 0; s < GameRunData.Instance.squad.Count && s < defaultSlots.Length; s++)
            {
                PlaceHero(GameRunData.Instance.squad[s], defaultSlots[s]);
            }
        }

        foreach (Transform child in benchContainer)
        {
            var card = child.GetComponent<HeroCardUI>();
            if (card != null && card.heroInstance != null)
            {
                bool isPlaced = System.Array.IndexOf(formation, card.heroInstance) >= 0;
                card.gameObject.SetActive(!isPlaced);
            }
        }

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

    // Tap a bench card → place in first empty slot
    void OnBenchCardTapped(HeroCardUI card)
    {
        for (int i = 0; i < 9; i++)
        {
            if (formation[i] == null)
            {
                PlaceHero(card.heroInstance, i);
                card.gameObject.SetActive(false);
                break;
            }
        }
        CheckBattleReady();
    }

    void PlaceHero(HeroInstance hero, int slotIndex)
    {
        formation[slotIndex] = hero;
        hero.gridIndex = slotIndex;
        gridSlots[slotIndex].Setup(slotIndex, hero, OnGridSlotTapped, OnHeroDrop);
    }

    void OnGridSlotTapped(int slotIndex)
    {
        var hero = formation[slotIndex];
        if (hero == null)
        {
            statsPanel.SetActive(false);
            return;
        }
        ShowStats(hero);
    }

    void OnHeroDrop(int fromSlot, int toSlot)
    {
        // Swap
        var tmp = formation[toSlot];
        formation[toSlot] = formation[fromSlot];
        formation[fromSlot] = tmp;

        gridSlots[fromSlot].Setup(fromSlot, formation[fromSlot], OnGridSlotTapped, OnHeroDrop);
        gridSlots[toSlot].Setup(toSlot, formation[toSlot], OnGridSlotTapped, OnHeroDrop);
        CheckBattleReady();
    }

    void ShowStats(HeroInstance hero)
    {
        statsPanel.SetActive(true);
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
        battleButton.interactable = filled == GameRunData.Instance?.squad.Count;
    }

    void OnBattle()
    {
        if (GameRunData.Instance == null) return;
        GameRunData.Instance.formation = (HeroInstance[])formation.Clone();
        SceneNav.GoTo(SceneNames.Combat);
    }
}