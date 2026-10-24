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
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        onTap?.Invoke(hero);
    }
}