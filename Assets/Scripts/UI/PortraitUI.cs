using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>Circular portrait in the combat bottom panel.</summary>
public class PortraitUI : MonoBehaviour, IPointerClickHandler
{
    public Image portraitCircle;           // tinted by class color
    public TextMeshProUGUI nameLabel;
    public Slider hpBar;
    public Slider energyBar;
    public Image hpFill;
    public Image energyFill;
    public TextMeshProUGUI hpLabel;       // e.g. "85/85"
    public TextMeshProUGUI fatiguedLabel;  // "FATIGUED" warning
    public Image deadOverlay;             // semi-transparent dark overlay when dead
    public TextMeshProUGUI upgradesBadgeLabel; // Mini-badge row showing acquired upgrades

    private HeroInstance hero;
    private System.Action<HeroInstance> onTap;

    static readonly Color HP_COLOR      = new Color(0.2f, 0.8f, 0.2f);
    static readonly Color HP_LOW_COLOR  = new Color(0.9f, 0.2f, 0.2f);
    static readonly Color EN_COLOR      = new Color(0.2f, 0.5f, 0.9f);

    public void Setup(HeroInstance h, System.Action<HeroInstance> tapCb)
    {
        hero = h;
        onTap = tapCb;

        string prefix = SpriteMapping.GetHeroSpritePrefix(h.data.heroName);
        var heroSprite = SpriteCache.LoadSprite($"{prefix}_idle_anim_f0");
        if (portraitCircle)
        {
            if (heroSprite != null)
            {
                portraitCircle.sprite = heroSprite;
                portraitCircle.preserveAspect = true;
                portraitCircle.color = Color.white;
            }
            else
            {
                portraitCircle.color = h.data.classColor;
            }
        }
        if (nameLabel) nameLabel.text = h.displayName;

        if (hpBar)
        {
            hpBar.maxValue = h.data.maxHP;
            hpBar.value = h.currentHP;
        }
        if (energyBar)
        {
            energyBar.maxValue = h.data.maxEnergy;
            energyBar.value = h.currentEnergy;
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
        if (hero == null) return;

        if (hpBar) hpBar.value = hero.currentHP;
        if (energyBar) energyBar.value = hero.currentEnergy;
        if (hpLabel) hpLabel.text = $"{hero.currentHP}/{hero.data.maxHP}";

        // HP color: green when ok, red when < 30%
        if (hpFill)
            hpFill.color = ((float)hero.currentHP / hero.data.maxHP) < 0.3f ? HP_LOW_COLOR : HP_COLOR;

        bool fatigued = hero.isFatigued;
        if (fatiguedLabel) fatiguedLabel.gameObject.SetActive(fatigued);

        bool dead = !hero.isAlive;
        if (deadOverlay) deadOverlay.gameObject.SetActive(dead);

        // Upgrade badges row
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

        var existing = transform.Find("UpgradesBadge");
        if (existing != null)
        {
            upgradesBadgeLabel = existing.GetComponent<TextMeshProUGUI>();
            return;
        }

        // Auto-create badge row if not in serialized prefab
        var badgeObj = new GameObject("UpgradesBadge");
        badgeObj.transform.SetParent(transform, false);

        var rt = badgeObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(0f, 57f);
        rt.sizeDelta = new Vector2(0f, 15f);

        upgradesBadgeLabel = badgeObj.AddComponent<TextMeshProUGUI>();
        upgradesBadgeLabel.fontSize = 10;
        upgradesBadgeLabel.fontStyle = FontStyles.Bold;
        upgradesBadgeLabel.alignment = TextAlignmentOptions.Center;
        upgradesBadgeLabel.textWrappingMode = TextWrappingModes.NoWrap;
        upgradesBadgeLabel.overflowMode = TextOverflowModes.Ellipsis;
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