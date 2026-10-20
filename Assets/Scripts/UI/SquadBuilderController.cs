using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Manages Squad Builder screen — hero roster cards and team slot panel.
/// </summary>
public class SquadBuilderController : MonoBehaviour
{
    [Header("Roster")]
    public Transform rosterContainer;       // parent for hero cards in roster
    public GameObject heroCardPrefab;

    [Header("Team Slots")]
    public Transform teamSlotsContainer;    // parent for 5 team slot UIs
    public GameObject teamSlotPrefab;

    [Header("Buttons")]
    public Button confirmButton;
    public Button backButton;
    public TextMeshProUGUI guideText;

    private List<HeroInstance> selectedHeroes = new();
    private List<HeroCardUI> rosterCards = new();
    private List<TeamSlotUI> teamSlots = new();

    const int SQUAD_SIZE = 5;

    void Start()
    {
        if (GameRunData.Instance != null && GameRunData.Instance.availableHeroes.Count == 0)
        {
            GameRunData.Instance.LoadDefaultHeroes();
        }

        confirmButton.interactable = false;
        backButton.onClick.AddListener(() => SceneNav.GoTo(SceneNames.MainMenu));
        confirmButton.onClick.AddListener(OnConfirm);

        BuildRosterCards();
        BuildTeamSlots();
        UpdateGuide();
    }

    void BuildRosterCards()
    {
        if (GameRunData.Instance == null) return;

        foreach (var heroData in GameRunData.Instance.availableHeroes)
        {
            var go = Instantiate(heroCardPrefab, rosterContainer);
            var card = go.GetComponent<HeroCardUI>();
            var instance = new HeroInstance(heroData);
            card.Setup(instance, OnHeroCardTapped);
            rosterCards.Add(card);
        }
    }

    void BuildTeamSlots()
    {
        for (int i = 0; i < SQUAD_SIZE; i++)
        {
            var go = Instantiate(teamSlotPrefab, teamSlotsContainer);
            var slot = go.GetComponent<TeamSlotUI>();
            int index = i;
            slot.Setup(null, () => OnRemoveFromSlot(index));
            teamSlots.Add(slot);
        }
    }

    void OnHeroCardTapped(HeroCardUI card)
    {
        if (selectedHeroes.Count >= SQUAD_SIZE) return;

        selectedHeroes.Add(card.heroInstance);
        card.gameObject.SetActive(false); // disappear from roster

        // Fill next empty team slot
        for (int i = 0; i < teamSlots.Count; i++)
        {
            if (teamSlots[i].heroInstance == null)
            {
                teamSlots[i].Setup(card.heroInstance, () => OnRemoveFromSlot(i));
                break;
            }
        }

        RefreshConfirmButton();
        UpdateGuide();
    }

    void OnRemoveFromSlot(int slotIndex)
    {
        var hero = teamSlots[slotIndex].heroInstance;
        if (hero == null) return;

        selectedHeroes.Remove(hero);
        teamSlots[slotIndex].Setup(null, () => OnRemoveFromSlot(slotIndex));

        // Re-show the roster card
        foreach (var card in rosterCards)
        {
            if (card.heroInstance == hero)
            {
                card.gameObject.SetActive(true);
                break;
            }
        }

        RefreshConfirmButton();
        UpdateGuide();
    }

    void RefreshConfirmButton()
    {
        confirmButton.interactable = selectedHeroes.Count == SQUAD_SIZE;
    }

    void UpdateGuide()
    {
        if (guideText != null)
            guideText.text = $"Pick your squad of 5 heroes ({selectedHeroes.Count}/{SQUAD_SIZE})";
    }

    void OnConfirm()
    {
        if (GameRunData.Instance == null) return;
        GameRunData.Instance.squad.Clear();
        GameRunData.Instance.squad.AddRange(selectedHeroes);
        SceneNav.GoTo(SceneNames.Formation);
    }
}