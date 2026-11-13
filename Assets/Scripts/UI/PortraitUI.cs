using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>Hero portrait card in the combat bottom panel — themed and non-overlapping.</summary>
public class PortraitUI : MonoBehaviour, IPointerClickHandler
{
    public Image portraitCircle;           // hero sprite / class color
    public TextMeshProUGUI nameLabel;
    public Slider hpBar;
    public Slider energyBar;
    public Image hpFill;
    public Image energyFill;
    public TextMeshProUGUI hpLabel;
    public TextMeshProUGUI fatiguedLabel;
    public Image deadOverlay;
    public TextMeshProUGUI upgradesBadgeLabel;

    private HeroInstance hero;
    private System.Action<HeroInstance> onTap;

    static readonly Color HP_COLOR     = new Color(0.24f, 0.84f, 0.34f);
    static readonly Color HP_LOW_COLOR = new Color(0.94f, 0.24f, 0.24f);
    static readonly Color EN_COLOR     = new Color(0.26f, 0.58f, 0.98f);

    public void Setup(HeroInstance h, System.Action<HeroInstance> tapCb)
    {
        hero = h;
        onTap = tapCb;

        if (hero == null || hero.data == null) return;
        if (hero.upgrades == null) hero.upgrades = new();

        var theme = DungeonUITheme.Instance;
        var rootImg = GetComponent<Image>();
        if (rootImg != null) DungeonUITheme.ApplyCard(rootImg, new Color(0.14f, 0.16f, 0.23f, 0.96f));

        string prefix = SpriteMapping.GetHeroSpritePrefix(hero.data.heroName, hero.data.spritePrefix);
        var heroSprite = SpriteCache.LoadHeroOrBossSprite(prefix);
        if (portraitCircle)
        {
            portraitCircle.enabled = true;
            if (heroSprite != null)
            {
                portraitCircle.sprite = heroSprite;
                portraitCircle.preserveAspect = true;
                portraitCircle.color = Color.white;
            }
            else
            {
                portraitCircle.color = hero.data.classColor;
            }
        }

        if (nameLabel)
        {
            nameLabel.text = hero.displayName ?? hero.data.heroName;
            nameLabel.raycastTarget = false;
        }

        if (hpBar)
        {
            hpBar.maxValue = hero.data.maxHP;
            hpBar.value = hero.currentHP;
            DungeonUITheme.ApplySlider(hpBar, theme != null ? theme.hpGreen : HP_COLOR);
            if (hpFill == null && hpBar.fillRect != null)
                hpFill = hpBar.fillRect.GetComponent<Image>();
        }
        if (energyBar)
        {
            energyBar.maxValue = hero.data.maxEnergy;
            energyBar.value = hero.currentEnergy;
            DungeonUITheme.ApplySlider(energyBar, theme != null ? theme.energyBlue : EN_COLOR);
            if (energyFill == null && energyBar.fillRect != null)
                energyFill = energyBar.fillRect.GetComponent<Image>();
        }

        if (hpLabel == null && hpBar != null)
        {
            hpLabel = hpBar.GetComponentInChildren<TextMeshProUGUI>();
        }

        if (fatiguedLabel) fatiguedLabel.gameObject.SetActive(false);
        if (deadOverlay) deadOverlay.gameObject.SetActive(false);

        EnsureBadgeLabel();
        Refresh();
    }

    public void Refresh()
    {
        if (hero == null || hero.data == null) return;
        if (hero.upgrades == null) hero.upgrades = new();

        if (hpBar)
        {
            hpBar.maxValue = hero.data.maxHP;
            hpBar.value = hero.currentHP;
        }
        if (energyBar)
        {
            energyBar.maxValue = hero.data.maxEnergy;
            energyBar.value = hero.currentEnergy;
        }
        if (hpLabel) hpLabel.text = $"{hero.currentHP}/{hero.data.maxHP}";

        if (hpFill)
            hpFill.color = ((float)hero.currentHP / Mathf.Max(1, hero.data.maxHP)) < 0.3f ? HP_LOW_COLOR : HP_COLOR;

        bool fatigued = hero.isFatigued;
        if (fatiguedLabel) fatiguedLabel.gameObject.SetActive(fatigued);

        bool dead = !hero.isAlive;
        if (deadOverlay) deadOverlay.gameObject.SetActive(dead);

        if (upgradesBadgeLabel != null)
        {
            if (hero.upgrades != null && hero.upgrades.Count > 0)
            {
                upgradesBadgeLabel.gameObject.SetActive(true);
                upgradesBadgeLabel.text = FormatBadges(hero.upgrades);
            }
            else
            {
                upgradesBadgeLabel.gameObject.SetActive(false);
            }
        }
    }

    void EnsureBadgeLabel()
    {
        if (upgradesBadgeLabel != null) return;

        var existing = transform.Find("UpgradesBadge") ?? transform.Find("BadgeText");
        if (existing != null)
        {
            upgradesBadgeLabel = existing.GetComponent<TextMeshProUGUI>();
            if (upgradesBadgeLabel != null) return;
        }

        try
        {
            var badgeObj = new GameObject("UpgradesBadge", typeof(RectTransform));
            badgeObj.transform.SetParent(transform, false);

            var rt = badgeObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(0f, 52f);
            rt.sizeDelta = new Vector2(0f, 16f);

            upgradesBadgeLabel = badgeObj.AddComponent<TextMeshProUGUI>();
            if (nameLabel != null && nameLabel.font != null)
            {
                upgradesBadgeLabel.font = nameLabel.font;
            }
            upgradesBadgeLabel.fontSize = 11;
            upgradesBadgeLabel.fontStyle = FontStyles.Bold;
            upgradesBadgeLabel.alignment = TextAlignmentOptions.Center;
            upgradesBadgeLabel.textWrappingMode = TextWrappingModes.NoWrap;
            upgradesBadgeLabel.raycastTarget = false;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[PortraitUI] EnsureBadgeLabel fallback: {ex.Message}");
        }
    }

    public static string FormatBadges(System.Collections.Generic.List<string> list)
    {
        if (list == null || list.Count == 0) return "";

        int atkCount = 0;
        int defCount = 0;
        int hpCount = 0;
        int magCount = 0;
        int spdCount = 0;
        int otherCount = 0;

        foreach (var u in list)
        {
            if (string.IsNullOrEmpty(u)) continue;
            string lower = u.ToLowerInvariant();
            if (lower.Contains("blade") || lower.Contains("atk") || lower.Contains("damage")) atkCount++;
            else if (lower.Contains("bulwark") || lower.Contains("def") || lower.Contains("shield") || lower.Contains("armor")) defCount++;
            else if (lower.Contains("blessing") || lower.Contains("heal") || lower.Contains("hp")) hpCount++;
            else if (lower.Contains("pyre") || lower.Contains("arcane") || lower.Contains("fire") || lower.Contains("spell")) magCount++;
            else if (lower.Contains("stride") || lower.Contains("swift") || lower.Contains("speed")) spdCount++;
            else otherCount++;
        }

        var parts = new System.Collections.Generic.List<string>();
        if (atkCount > 0) parts.Add($"<color=#EF4444>[ATK{(atkCount > 1 ? "x" + atkCount : "")}]</color>");
        if (defCount > 0) parts.Add($"<color=#3B82F6>[DEF{(defCount > 1 ? "x" + defCount : "")}]</color>");
        if (hpCount > 0) parts.Add($"<color=#10B981>[HP{(hpCount > 1 ? "x" + hpCount : "")}]</color>");
        if (magCount > 0) parts.Add($"<color=#F97316>[MAG{(magCount > 1 ? "x" + magCount : "")}]</color>");
        if (spdCount > 0) parts.Add($"<color=#8B5CF6>[SPD{(spdCount > 1 ? "x" + spdCount : "")}]</color>");
        if (otherCount > 0) parts.Add($"<color=#FCD34D>[+{(otherCount > 1 ? otherCount.ToString() : "")}]</color>");

        return string.Join(" ", parts);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        onTap?.Invoke(hero);
    }
}
