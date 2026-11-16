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
    const float HERO_CARD_W = 220f;
    const float HERO_CARD_H = 270f;
    const float TEAM_SLOT_SIZE = 160f;
    const float TEAM_SLOT_HEIGHT = 118f;   // 5 slots x 118 + 4 x 12 spacing = 638px — fits the panel without clipping the 5th slot behind ConfirmBtn

    void ApplyCardSize(GameObject go, float w, float h)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.minWidth = w; le.minHeight = h;
        le.preferredWidth = w; le.preferredHeight = h;
        var rt = go.GetComponent<RectTransform>();
        if (rt != null) rt.sizeDelta = new Vector2(w, h);
    }

    void Start()
    {
        if (GameRunData.Instance != null && GameRunData.Instance.availableHeroes.Count == 0)
        {
            GameRunData.Instance.LoadDefaultHeroes();
        }

        confirmButton.interactable = true; // stays clickable to fire Kira locked-feedback
        backButton.onClick.AddListener(() => SceneNav.GoTo(SceneNames.MainMenu));
        confirmButton.onClick.AddListener(OnConfirm);

        BuildRosterCards();
        BuildTeamSlots();
        UpdateGuide();
    }

    private UnityEngine.Coroutine lockedFeedbackRoutine;

    private System.Collections.IEnumerator LockedFeedbackRoutine()
    {
        var btnImg = confirmButton.GetComponent<Image>();
        var label = confirmButton.GetComponentInChildren<TextMeshProUGUI>();
        var btnRt = confirmButton.transform as RectTransform;
        Color origCol = btnImg != null ? btnImg.color : Color.white;
        string origText = label != null ? label.text : string.Empty;
        Vector2 origPos = btnRt != null ? btnRt.anchoredPosition : Vector2.zero;

        if (btnImg != null) btnImg.color = new Color(0.45f, 0.47f, 0.55f, 1f);
        if (label != null) label.text = $"Pick {SQUAD_SIZE - selectedHeroes.Count} more";

        // Horizontal nudge (0.15s right, 0.15s back) to signal "not yet"
        if (btnRt != null)
        {
            float half = 0.15f, dist = 18f, t = 0f;
            while (t < half) { t += Time.unscaledDeltaTime; btnRt.anchoredPosition = origPos + new Vector2(Mathf.Sin(Mathf.Min(1f, t / half) * Mathf.PI) * dist, 0f); yield return null; }
            t = 0f;
            while (t < half) { t += Time.unscaledDeltaTime; btnRt.anchoredPosition = origPos - new Vector2(Mathf.Sin(Mathf.Min(1f, t / half) * Mathf.PI) * dist, 0f); yield return null; }
            btnRt.anchoredPosition = origPos;
        }

        yield return new WaitForSecondsRealtime(2f);

        if (btnImg != null) btnImg.color = origCol;
        if (label != null) label.text = origText;

        RefreshConfirmButton();
        UpdateGuide();
        lockedFeedbackRoutine = null;
    }

    void BuildRosterCards()
    {
        if (GameRunData.Instance == null) return;

        foreach (var heroData in GameRunData.Instance.availableHeroes)
        {
            var go = Instantiate(heroCardPrefab, rosterContainer);
            ApplyCardSize(go, HERO_CARD_W, HERO_CARD_H);
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
            ApplyCardSize(go, TEAM_SLOT_SIZE, TEAM_SLOT_HEIGHT);
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
        // Keep clickable (feedback fires on locked tap); visual locked state via grey tint
        var img = confirmButton.GetComponent<Image>();
        bool ready = selectedHeroes.Count == SQUAD_SIZE;
        if (img != null) img.color = ready ? new Color(0.24f, 0.68f, 0.38f, 1f) : new Color(0.32f, 0.34f, 0.40f, 1f);
    }

    void UpdateGuide()
    {
        if (guideText != null)
            guideText.text = $"Pick your squad of 5 heroes ({selectedHeroes.Count}/{SQUAD_SIZE})";
    }

    void OnConfirm()
    {
        if (GameRunData.Instance == null) return;

        if (selectedHeroes.Count != SQUAD_SIZE)
        {
            if (lockedFeedbackRoutine != null) StopCoroutine(lockedFeedbackRoutine);
            lockedFeedbackRoutine = StartCoroutine(LockedFeedbackRoutine());
            return;
        }

        GameRunData.Instance.squad.Clear();
        GameRunData.Instance.squad.AddRange(selectedHeroes);
        SceneNav.GoTo(SceneNames.Formation);
    }
}