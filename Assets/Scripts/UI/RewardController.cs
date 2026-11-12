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

    [Header("Info Tooltip Modal")]
    private GameObject infoModal;
    private TextMeshProUGUI infoTitle;
    private TextMeshProUGUI infoDetails;

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

        SetupStylizedWindowPanel();
        GenerateRewards();
    }

    void SetupStylizedWindowPanel()
    {
        Canvas canvas = GetComponentInParent<Canvas>() ?? FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        // Hide legacy scene title if present
        var legacyTitle = canvas.transform.Find("Title");
        if (legacyTitle != null) legacyTitle.gameObject.SetActive(false);

        // Huge framed window panel (matching user sketch)
        var windowGo = new GameObject("RewardWindowPanel", typeof(RectTransform));
        windowGo.transform.SetParent(canvas.transform, false);
        var winRt = windowGo.GetComponent<RectTransform>();
        winRt.anchorMin = new Vector2(0.5f, 0.5f);
        winRt.anchorMax = new Vector2(0.5f, 0.5f);
        winRt.pivot = new Vector2(0.5f, 0.5f);
        winRt.anchoredPosition = new Vector2(0f, 10f);
        winRt.sizeDelta = new Vector2(940f, 540f);

        var winBg = windowGo.AddComponent<Image>();
        winBg.color = new Color(0.06f, 0.08f, 0.14f, 0.98f);

        var winOutline = windowGo.AddComponent<Outline>();
        winOutline.effectColor = new Color(0.24f, 0.65f, 0.95f, 0.9f);
        winOutline.effectDistance = new Vector2(2.5f, 2.5f);

        // Top Header Bar
        var headerGo = new GameObject("HeaderBar", typeof(RectTransform));
        headerGo.transform.SetParent(windowGo.transform, false);
        var headerRt = headerGo.GetComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.anchoredPosition = Vector2.zero;
        headerRt.sizeDelta = new Vector2(0f, 80f);

        var headerBg = headerGo.AddComponent<Image>();
        headerBg.color = new Color(0.03f, 0.04f, 0.08f, 0.95f);

        // 1. Mini Guider Kira Icon (Left Corner per user sketch)
        var badgeGo = new GameObject("GuiderBadge", typeof(RectTransform));
        badgeGo.transform.SetParent(headerGo.transform, false);
        var badgeRt = badgeGo.GetComponent<RectTransform>();
        badgeRt.anchorMin = new Vector2(0f, 0.5f);
        badgeRt.anchorMax = new Vector2(0f, 0.5f);
        badgeRt.pivot = new Vector2(0f, 0.5f);
        badgeRt.anchoredPosition = new Vector2(20f, 0f);
        badgeRt.sizeDelta = new Vector2(56f, 56f);

        var badgeBg = badgeGo.AddComponent<Image>();
        badgeBg.color = new Color(0.09f, 0.14f, 0.24f, 1f);
        var badgeOutline = badgeGo.AddComponent<Outline>();
        badgeOutline.effectColor = new Color(0.35f, 0.85f, 1f, 0.9f);
        badgeOutline.effectDistance = new Vector2(1.5f, 1.5f);

        var sprGo = new GameObject("Avatar", typeof(RectTransform));
        sprGo.transform.SetParent(badgeGo.transform, false);
        var sprRt = sprGo.GetComponent<RectTransform>();
        sprRt.anchorMin = new Vector2(0.1f, 0.1f);
        sprRt.anchorMax = new Vector2(0.9f, 0.9f);
        sprRt.offsetMin = Vector2.zero;
        sprRt.offsetMax = Vector2.zero;
        var sprImg = sprGo.AddComponent<Image>();
        sprImg.sprite = SpriteCache.LoadSprite("doc_idle_anim_f0") ?? SpriteCache.LoadSprite("elf_f_idle_anim_f0");
        sprImg.preserveAspect = true;
        sprImg.raycastTarget = false;

        // 2. Header Text (XXXXX in user sketch)
        var titleGo = new GameObject("TitleText", typeof(RectTransform));
        titleGo.transform.SetParent(headerGo.transform, false);
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 0.5f);
        titleRt.anchorMax = new Vector2(1f, 0.5f);
        titleRt.pivot = new Vector2(0f, 0.5f);
        titleRt.anchoredPosition = new Vector2(90f, 0f);
        titleRt.sizeDelta = new Vector2(-280f, 60f);

        var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "<color=#38BDF8><b>KIRA // TACTICAL ADVISOR</b></color>\n<size=20><color=#FCD34D><b>LEVEL CLEARED! CHOOSE 2 REWARDS</b></color></size> <size=13><color=#94A3B8>— Boss Defeated</color></size>";
        titleTmp.fontSize = 13;
        titleTmp.alignment = TextAlignmentOptions.Left;
        titleTmp.raycastTarget = false;

        // 3. Gold Display (Right Corner inside header)
        var goldGo = new GameObject("RewardGoldLabel", typeof(RectTransform));
        goldGo.transform.SetParent(headerGo.transform, false);
        var goldRt = goldGo.GetComponent<RectTransform>();
        goldRt.anchorMin = new Vector2(1f, 0.5f);
        goldRt.anchorMax = new Vector2(1f, 0.5f);
        goldRt.pivot = new Vector2(1f, 0.5f);
        goldRt.anchoredPosition = new Vector2(-24f, 0f);
        goldRt.sizeDelta = new Vector2(180f, 40f);

        goldLabel = goldGo.AddComponent<TextMeshProUGUI>();
        goldLabel.fontSize = 16;
        goldLabel.fontStyle = FontStyles.Bold;
        goldLabel.alignment = TextAlignmentOptions.Right;
        goldLabel.color = new Color(1f, 0.85f, 0.2f);
        goldLabel.raycastTarget = false;
        RefreshGoldLabel();

        // Reparent Card Container inside WindowPanel
        if (cardContainer != null)
        {
            cardContainer.SetParent(windowGo.transform, false);
            var cardRt = cardContainer.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = new Vector2(0f, 15f);
            cardRt.sizeDelta = new Vector2(880f, 330f);

            var hlg = cardContainer.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                hlg.spacing = 30;
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childControlWidth = false;
                hlg.childControlHeight = false;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
            }
        }

        // Reparent Confirm Button inside WindowPanel (Bottom Centered/Left)
        if (confirmButton != null)
        {
            confirmButton.transform.SetParent(windowGo.transform, false);
            var rt = confirmButton.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-60f, 42f);
            rt.sizeDelta = new Vector2(250f, 50f);

            var img = confirmButton.GetComponent<Image>();
            if (img != null) img.color = new Color(0.14f, 0.62f, 0.36f, 1f);

            var outline = confirmButton.GetComponent<Outline>() ?? confirmButton.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.35f, 0.95f, 0.55f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, 1.5f);
        }

        // Reroll Button (Bottom Right per user sketch)
        var btnGo = new GameObject("RerollButton", typeof(RectTransform));
        btnGo.transform.SetParent(windowGo.transform, false);

        var rrt = btnGo.GetComponent<RectTransform>();
        rrt.anchorMin = new Vector2(0.5f, 0f);
        rrt.anchorMax = new Vector2(0.5f, 0f);
        rrt.pivot = new Vector2(0.5f, 0.5f);
        rrt.anchoredPosition = new Vector2(240f, 42f); // Cleanly on right side
        rrt.sizeDelta = new Vector2(190f, 50f);

        var rimg = btnGo.AddComponent<Image>();
        rimg.color = new Color(0.20f, 0.32f, 0.60f, 1f);

        var routline = btnGo.AddComponent<Outline>();
        routline.effectColor = new Color(0.35f, 0.70f, 1f, 0.85f);
        routline.effectDistance = new Vector2(1.5f, 1.5f);

        rerollButton = btnGo.AddComponent<Button>();
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
        rerollText.fontSize = 15;
        rerollText.fontStyle = FontStyles.Bold;
        rerollText.alignment = TextAlignmentOptions.Center;
        rerollText.color = Color.white;
        rerollText.raycastTarget = false;

        RefreshGoldLabel();

        // Ensure overlays remain in front
        if (restModal != null) restModal.transform.SetAsLastSibling();
        if (backButton != null) backButton.transform.SetAsLastSibling();
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

        // Clear existing cards
        selectedRewards.Clear();
        confirmButton.interactable = false;

        GenerateRewards();
    }

    void GenerateRewards()
    {
        // Clear previous cards in UI container
        if (cardContainer != null)
        {
            for (int i = cardContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(cardContainer.GetChild(i).gameObject);
            }
        }

        // Rich procedural reward pool with weapon boosts, rest, and stat kits
        var pool = new List<RewardData>();

        // 1. Themed Weapon Boost: Heavy Claymore (Knight)
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

        // 2. Themed Weapon Boost: Piercing Rapier (Knight)
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

        // 3. Themed Weapon Boost: Arcane Catalyst (Mage)
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

        // 4. Themed Weapon Boost: Void Wand (Mage)
        var voidWand = ScriptableObject.CreateInstance<RewardData>();
        voidWand.rewardName = "Void Wand";
        voidWand.rewardType = RewardType.WeaponBoost;
        voidWand.weaponStyle = "VOID WAND";
        voidWand.description = "+55% Spell Power\n<color=#EF4444>-15 Max HP</color>";
        voidWand.targetClass = HeroClass.Mage;
        voidWand.attackMult = 1.55f;
        voidWand.hpBonus = -15;
        pool.Add(voidWand);

        // 5. Themed Weapon Boost: Holy Relic (Healer)
        var relic = ScriptableObject.CreateInstance<RewardData>();
        relic.rewardName = "Holy Relic";
        relic.rewardType = RewardType.WeaponBoost;
        relic.weaponStyle = "HOLY RELIC";
        relic.description = "+40% Heal Potency, +30 Max Energy\n<color=#10B981>Divine Aura</color>";
        relic.targetClass = HeroClass.Healer;
        relic.attackMult = 1.40f;
        relic.energyBonus = 30;
        pool.Add(relic);

        // 6. Rest Camp
        var restReward = ScriptableObject.CreateInstance<RewardData>();
        restReward.rewardName = "Camp Coffee";
        restReward.rewardType = RewardType.Rest;
        restReward.description = "Restore 50% Energy to 2 heroes.\n<color=#38BDF8>Combat Readiness</color>";
        pool.Add(restReward);

        // 7. Smuggler's Stash (Gold)
        var goldStash = ScriptableObject.CreateInstance<RewardData>();
        goldStash.rewardName = "Smuggler's Stash";
        goldStash.rewardType = RewardType.GoldBonus;
        goldStash.description = "Immediate payout of +80 Gold.\n<color=#FCD34D>Tactical Reserves</color>";
        goldStash.attackBonus = 80; // Used for gold amount
        pool.Add(goldStash);

        // Shuffle pool
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