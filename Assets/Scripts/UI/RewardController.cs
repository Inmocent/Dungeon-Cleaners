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

    [Header("Mystery Reroll & Economy")]
    private Button rerollButton;
    private TextMeshProUGUI rerollText;
    private TextMeshProUGUI goldLabel;
    private const int REROLL_COST = 25;

    private List<RewardData> shownRewards = new();
    private List<RewardData> selectedRewards = new();
    private List<Toggle> heroRestToggles = new();
    private List<HeroInstance> restCandidates = new();

    void Start()
    {
        Time.timeScale = 1f;
        SpriteCache.PreloadAll();
        GameRunData.Instance?.EnsureSquadAndFormation();

        restModal.SetActive(false);
        confirmButton.interactable = false;
        backButton.onClick.AddListener(() => SceneNav.GoTo(SceneNames.Formation));
        confirmButton.onClick.AddListener(OnConfirm);
        restConfirmBtn.onClick.AddListener(OnConfirmRest);

        SetupStylizedWindowPanel();
        GenerateRewards();
    }

    void SetupStylizedWindowPanel()
    {
        Canvas canvas = GetComponentInParent<Canvas>() ?? FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        var theme = DungeonUITheme.Instance;

        var legacyTitle = canvas.transform.Find("Title");
        if (legacyTitle != null) legacyTitle.gameObject.SetActive(false);

        var windowGo = new GameObject("RewardWindowPanel", typeof(RectTransform));
        windowGo.transform.SetParent(canvas.transform, false);
        var winRt = windowGo.GetComponent<RectTransform>();
        winRt.anchorMin = new Vector2(0.5f, 0.5f);
        winRt.anchorMax = new Vector2(0.5f, 0.5f);
        winRt.pivot = new Vector2(0.5f, 0.5f);
        winRt.anchoredPosition = new Vector2(0f, 10f);
        winRt.sizeDelta = new Vector2(980f, 560f);

        var winBg = windowGo.AddComponent<Image>();
        DungeonUITheme.ApplyPanel(winBg, theme != null ? theme.panelSlate : new Color(0.14f, 0.16f, 0.22f, 0.98f));

        // Top Header Bar
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

        // 1. Mini Guider Kira Icon
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

        var sprGo = new GameObject("Avatar", typeof(RectTransform));
        sprGo.transform.SetParent(badgeGo.transform, false);
        var sprRt = sprGo.GetComponent<RectTransform>();
        sprRt.anchorMin = new Vector2(0.12f, 0.12f);
        sprRt.anchorMax = new Vector2(0.88f, 0.88f);
        sprRt.offsetMin = Vector2.zero;
        sprRt.offsetMax = Vector2.zero;
        var sprImg = sprGo.AddComponent<Image>();
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

        // 2. Header Text
        var titleGo = new GameObject("TitleText", typeof(RectTransform));
        titleGo.transform.SetParent(headerGo.transform, false);
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 0.5f);
        titleRt.anchorMax = new Vector2(1f, 0.5f);
        titleRt.pivot = new Vector2(0f, 0.5f);
        titleRt.anchoredPosition = new Vector2(92f, 0f);
        titleRt.sizeDelta = new Vector2(-300f, 64f);

        var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "<color=#38BDF8><b>KIRA // TACTICAL ADVISOR</b></color>\n<size=22><color=#FCD34D><b>LEVEL CLEARED! CHOOSE 2 REWARDS</b></color></size>";
        titleTmp.fontSize = 15;
        titleTmp.alignment = TextAlignmentOptions.Left;
        titleTmp.raycastTarget = false;

        // 3. Gold Display
        var goldGo = new GameObject("RewardGoldLabel", typeof(RectTransform));
        goldGo.transform.SetParent(headerGo.transform, false);
        var goldRt = goldGo.GetComponent<RectTransform>();
        goldRt.anchorMin = new Vector2(1f, 0.5f);
        goldRt.anchorMax = new Vector2(1f, 0.5f);
        goldRt.pivot = new Vector2(1f, 0.5f);
        goldRt.anchoredPosition = new Vector2(-20f, 0f);
        goldRt.sizeDelta = new Vector2(190f, 44f);

        goldLabel = goldGo.AddComponent<TextMeshProUGUI>();
        goldLabel.fontSize = 19;
        goldLabel.fontStyle = FontStyles.Bold;
        goldLabel.alignment = TextAlignmentOptions.Right;
        goldLabel.color = theme != null ? theme.accentGold : new Color(1f, 0.85f, 0.2f);
        goldLabel.raycastTarget = false;
        RefreshGoldLabel();

        if (cardContainer != null)
        {
            cardContainer.SetParent(windowGo.transform, false);
            var cardRt = cardContainer.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = new Vector2(0f, 8f);
            cardRt.sizeDelta = new Vector2(900f, 340f);

            var hlg = cardContainer.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                hlg.spacing = 28;
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childControlWidth = false;
                hlg.childControlHeight = false;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
            }
        }

        if (confirmButton != null)
        {
            confirmButton.transform.SetParent(windowGo.transform, false);
            var rt = confirmButton.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-115f, 46f);
            rt.sizeDelta = new Vector2(270f, 56f);

            DungeonUITheme.ApplyButton(confirmButton, theme != null ? theme.btnPrimaryGreen : new Color(0.24f, 0.68f, 0.38f), true);
        }

        var btnGo = new GameObject("RerollButton", typeof(RectTransform));
        btnGo.transform.SetParent(windowGo.transform, false);

        var rrt = btnGo.GetComponent<RectTransform>();
        rrt.anchorMin = new Vector2(0.5f, 0f);
        rrt.anchorMax = new Vector2(0.5f, 0f);
        rrt.pivot = new Vector2(0.5f, 0.5f);
        rrt.anchoredPosition = new Vector2(180f, 46f);
        rrt.sizeDelta = new Vector2(210f, 56f);

        btnGo.AddComponent<Image>();
        rerollButton = btnGo.AddComponent<Button>();
        DungeonUITheme.ApplyButton(rerollButton, theme != null ? theme.btnArcaneBlue : new Color(0.24f, 0.48f, 0.84f), false);
        rerollButton.onClick.AddListener(OnRerollClicked);

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(btnGo.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        rerollText = textGo.AddComponent<TextMeshProUGUI>();
        rerollText.text = $"REROLL ({REROLL_COST}g)";
        rerollText.fontSize = 18;
        rerollText.fontStyle = FontStyles.Bold;
        rerollText.alignment = TextAlignmentOptions.Center;
        rerollText.color = Color.white;
        rerollText.raycastTarget = false;

        if (restModal != null)
        {
            DungeonUITheme.ApplyPanel(restModal.GetComponent<Image>(), theme != null ? theme.panelSlate : null);
            if (restConfirmBtn != null)
                DungeonUITheme.ApplyButton(restConfirmBtn, theme != null ? theme.btnPrimaryGreen : null, true);
            restModal.transform.SetAsLastSibling();
        }

        if (backButton != null)
        {
            DungeonUITheme.ApplyButton(backButton, theme != null ? theme.btnSecondarySteel : null, false);
            backButton.transform.SetAsLastSibling();
        }

        RefreshGoldLabel();
    }

    void RefreshGoldLabel()
    {
        if (goldLabel != null && GameRunData.Instance != null)
        {
            goldLabel.text = $"GOLD: {GameRunData.Instance.gold}G";
        }
        if (rerollButton != null && GameRunData.Instance != null)
        {
            bool canAfford = GameRunData.Instance.CanAfford(REROLL_COST);
            rerollButton.interactable = canAfford;
            if (rerollText != null)
            {
                rerollText.color = canAfford ? Color.white : new Color(0.6f, 0.6f, 0.6f, 0.7f);
            }
        }
    }

    void OnRerollClicked()
    {
        var run = GameRunData.Instance;
        if (run == null) return;
        if (!run.CanAfford(REROLL_COST)) return;

        run.SpendGold(REROLL_COST);
        RefreshGoldLabel();

        selectedRewards.Clear();
        confirmButton.interactable = false;

        GenerateRewards();
    }

    void GenerateRewards()
    {
        if (cardContainer != null)
        {
            for (int i = cardContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(cardContainer.GetChild(i).gameObject);
            }
        }

        var pool = new List<RewardData>();

        var claymore = ScriptableObject.CreateInstance<RewardData>();
        claymore.rewardName = "Heavy Claymore";
        claymore.rewardType = RewardType.WeaponBoost;
        claymore.weaponStyle = "HEAVY GREATSWORD";
        claymore.description = "+35% ATK, +20 Max HP\n<color=#EF4444>-15% Move Speed</color>";
        claymore.targetClass = HeroClass.Knight;
        claymore.attackMult = 1.35f;
        claymore.hpBonus = 20;
        claymore.speedMult = 0.85f;
        pool.Add(claymore);

        var rapier = ScriptableObject.CreateInstance<RewardData>();
        rapier.rewardName = "Piercing Rapier";
        rapier.rewardType = RewardType.WeaponBoost;
        rapier.weaponStyle = "PIERCING RAPIER";
        rapier.description = "+30% Attack Speed, +20% Move Speed\n<color=#EF4444>-10 Max HP</color>";
        rapier.targetClass = HeroClass.Knight;
        rapier.attackSpeedMult = 1.30f;
        rapier.speedMult = 1.20f;
        rapier.hpBonus = -10;
        pool.Add(rapier);

        var catalyst = ScriptableObject.CreateInstance<RewardData>();
        catalyst.rewardName = "Arcane Catalyst";
        catalyst.rewardType = RewardType.WeaponBoost;
        catalyst.weaponStyle = "ARCANE CATALYST";
        catalyst.description = "+40% Spell Power, +25 Max Energy\n<color=#EF4444>-10% Move Speed</color>";
        catalyst.targetClass = HeroClass.Mage;
        catalyst.attackMult = 1.40f;
        catalyst.energyBonus = 25;
        catalyst.speedMult = 0.90f;
        pool.Add(catalyst);

        var voidWand = ScriptableObject.CreateInstance<RewardData>();
        voidWand.rewardName = "Void Wand";
        voidWand.rewardType = RewardType.WeaponBoost;
        voidWand.weaponStyle = "VOID WAND";
        voidWand.description = "+55% Spell Power\n<color=#EF4444>-15 Max HP</color>";
        voidWand.targetClass = HeroClass.Mage;
        voidWand.attackMult = 1.55f;
        voidWand.hpBonus = -15;
        pool.Add(voidWand);

        var relic = ScriptableObject.CreateInstance<RewardData>();
        relic.rewardName = "Holy Relic";
        relic.rewardType = RewardType.WeaponBoost;
        relic.weaponStyle = "HOLY RELIC";
        relic.description = "+40% Heal Potency, +30 Max Energy\n<color=#10B981>Divine Aura</color>";
        relic.targetClass = HeroClass.Healer;
        relic.attackMult = 1.40f;
        relic.energyBonus = 30;
        pool.Add(relic);

        var restReward = ScriptableObject.CreateInstance<RewardData>();
        restReward.rewardName = "Camp Coffee";
        restReward.rewardType = RewardType.Rest;
        restReward.description = "Restore 50% Energy to 2 heroes.\n<color=#38BDF8>Combat Readiness</color>";
        pool.Add(restReward);

        var goldStash = ScriptableObject.CreateInstance<RewardData>();
        goldStash.rewardName = "Smuggler's Stash";
        goldStash.rewardType = RewardType.GoldBonus;
        goldStash.description = "Immediate payout of +80 Gold.\n<color=#FCD34D>Tactical Reserves</color>";
        goldStash.attackBonus = 80;
        pool.Add(goldStash);

        for (int i = 0; i < pool.Count; i++)
        {
            int rIdx = Random.Range(i, pool.Count);
            var tmp = pool[i];
            pool[i] = pool[rIdx];
            pool[rIdx] = tmp;
        }

        shownRewards = pool.GetRange(0, 3);

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
            if (selectedRewards.Count >= 2) return;
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
            else if (r.rewardType == RewardType.GoldBonus)
            {
                GameRunData.Instance?.AddGold(r.attackBonus > 0 ? r.attackBonus : 80);
                RefreshGoldLabel();
            }
            else if (r.rewardType == RewardType.UpgradeKit || r.rewardType == RewardType.WeaponBoost)
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

                if (kit.attackMult != 1.0f)
                {
                    hero.data.attackDamage = Mathf.Max(1, Mathf.RoundToInt(hero.data.attackDamage * kit.attackMult));
                }
                if (kit.speedMult != 1.0f)
                {
                    hero.data.moveSpeed = Mathf.Max(1.5f, hero.data.moveSpeed * kit.speedMult);
                }
                if (kit.attackSpeedMult != 1.0f)
                {
                    hero.data.attackSpeed = Mathf.Max(0.5f, hero.data.attackSpeed * kit.attackSpeedMult);
                }
                if (kit.hpBonus != 0)
                {
                    hero.data.maxHP = Mathf.Max(10, hero.data.maxHP + kit.hpBonus);
                    hero.currentHP = Mathf.Clamp(hero.currentHP + kit.hpBonus, 1, hero.data.maxHP);
                }
                if (kit.energyBonus != 0)
                {
                    hero.data.maxEnergy = Mathf.Max(10, hero.data.maxEnergy + kit.energyBonus);
                    hero.currentEnergy = Mathf.Clamp(hero.currentEnergy + kit.energyBonus, 1, hero.data.maxEnergy);
                }
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
