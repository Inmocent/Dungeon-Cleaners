using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>Reward Screen — choose 2 of 3 rewards after clearing a room.</summary>
public class RewardController : MonoBehaviour
{
    [Header("Cards")]
    public Transform cardContainer;
    public GameObject rewardCardPrefab;

    [Header("Buttons")]
    public Button confirmButton;
    public Button backButton;

    [Header("Rest Modal")]
    public GameObject restModal;
    public Transform restHeroList;
    public GameObject restHeroRowPrefab;
    public Button restConfirmBtn;

    private List<RewardData> shownRewards = new();
    private List<RewardData> selectedRewards = new();
    private List<Toggle> heroRestToggles = new();
    private List<HeroInstance> restCandidates = new();

    void Start()
    {
        Time.timeScale = 1f;
        GameRunData.Instance?.EnsureSquadAndFormation();
        restModal.SetActive(false);
        confirmButton.interactable = false;
        backButton.onClick.AddListener(() => SceneNav.GoTo(SceneNames.Formation));
        confirmButton.onClick.AddListener(OnConfirm);
        restConfirmBtn.onClick.AddListener(OnConfirmRest);

        GenerateRewards();
    }

    void GenerateRewards()
    {
        // In draft: use 3 hardcoded reward types
        var restReward = ScriptableObject.CreateInstance<RewardData>();
        restReward.rewardName = "Camp Coffee";
        restReward.rewardType = RewardType.Rest;
        restReward.description = "Restore 50% Energy to 2 heroes.";

        var kit1 = ScriptableObject.CreateInstance<RewardData>();
        kit1.rewardName = "Tower Shield";
        kit1.rewardType = RewardType.UpgradeKit;
        kit1.description = "+2 DEF, blocks front damage.";
        kit1.targetClass = HeroClass.Knight;

        var kit2 = ScriptableObject.CreateInstance<RewardData>();
        kit2.rewardName = "Arcane Overload";
        kit2.rewardType = RewardType.UpgradeKit;
        kit2.description = "300% damage on next spell cast.";
        kit2.targetClass = HeroClass.Mage;

        shownRewards = new List<RewardData> { restReward, kit1, kit2 };

        foreach (var reward in shownRewards)
        {
            var go = Instantiate(rewardCardPrefab, cardContainer);
            var card = go.GetComponent<RewardCardUI>();
            card.Setup(reward, OnCardToggled);
        }
    }

    void OnCardToggled(RewardData reward, bool isSelected)
    {
        if (isSelected)
        {
            if (selectedRewards.Count >= 2) return; // max 2
            selectedRewards.Add(reward);
        }
        else
        {
            selectedRewards.Remove(reward);
        }
        confirmButton.interactable = selectedRewards.Count == 2;
    }

    void OnConfirm()
    {
        bool hasRest = false;
        foreach (var r in selectedRewards)
        {
            if (r.rewardType == RewardType.Rest)
            {
                hasRest = true;
            }
            else if (r.rewardType == RewardType.UpgradeKit)
            {
                ApplyUpgradeKit(r);
            }
        }

        if (hasRest)
            OpenRestModal();
        else
            FinishRewards();
    }

    void ApplyUpgradeKit(RewardData kit)
    {
        if (GameRunData.Instance == null) return;
        foreach (var hero in GameRunData.Instance.squad)
        {
            if (hero.data.heroClass == kit.targetClass)
            {
                hero.upgrades.Add(kit.rewardName);
                hero.currentHP = Mathf.Min(hero.currentHP + kit.hpBonus, hero.data.maxHP);
                hero.currentEnergy = Mathf.Min(hero.currentEnergy + kit.energyBonus, hero.data.maxEnergy);
                break;
            }
        }
    }

    void OpenRestModal()
    {
        restModal.SetActive(true);
        heroRestToggles.Clear();
        restCandidates.Clear();

        foreach (Transform t in restHeroList) Destroy(t.gameObject);

        if (GameRunData.Instance == null) return;
        foreach (var hero in GameRunData.Instance.squad)
        {
            restCandidates.Add(hero);
            var go = Instantiate(restHeroRowPrefab, restHeroList);

            // Setup label + toggle
            var texts = go.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length > 0)
                texts[0].text = $"{hero.displayName}   HP {hero.currentHP}/{hero.data.maxHP}   EN {hero.currentEnergy}/{hero.data.maxEnergy}";

            var toggle = go.GetComponentInChildren<Toggle>();
            if (toggle) heroRestToggles.Add(toggle);
        }
    }

    void OnConfirmRest()
    {
        int restCount = 0;
        for (int i = 0; i < heroRestToggles.Count && i < restCandidates.Count; i++)
        {
            if (heroRestToggles[i].isOn && restCount < 2)
            {
                restCandidates[i].RestoreEnergy(0.5f);
                restCount++;
            }
        }
        restModal.SetActive(false);
        FinishRewards();
    }

    void FinishRewards()
    {
        SceneNav.GoTo(SceneNames.Formation);
    }
}