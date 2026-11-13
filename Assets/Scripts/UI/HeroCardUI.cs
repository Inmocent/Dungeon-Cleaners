using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>A single hero card in the roster — tappable, themed, with class color & hero sprite.</summary>
public class HeroCardUI : MonoBehaviour, IPointerClickHandler
{
    public TextMeshProUGUI nameLabel;
    public TextMeshProUGUI classLabel;
    public Slider hpBar;
    public Slider energyBar;
    public Image cardBackground;
    public Image portraitIcon;

    [HideInInspector] public HeroInstance heroInstance;
    private System.Action<HeroCardUI> onTap;

    public void Setup(HeroInstance hero, System.Action<HeroCardUI> tapCallback)
    {
        heroInstance = hero;
        onTap = tapCallback;

        var theme = DungeonUITheme.Instance;

        if (nameLabel) nameLabel.text = hero.displayName;
        if (classLabel) classLabel.text = $"{hero.data.heroClass} • {hero.data.attackDamage} ATK";

        if (hpBar)
        {
            hpBar.maxValue = hero.data.maxHP;
            hpBar.value = hero.currentHP;
            DungeonUITheme.ApplySlider(hpBar, theme != null ? theme.hpGreen : new Color(0.24f, 0.84f, 0.34f));
        }
        if (energyBar)
        {
            energyBar.maxValue = hero.data.maxEnergy;
            energyBar.value = hero.currentEnergy;
            DungeonUITheme.ApplySlider(energyBar, theme != null ? theme.energyBlue : new Color(0.26f, 0.58f, 0.98f));
        }

        if (cardBackground)
        {
            Color tinted = Color.Lerp(hero.data.classColor, new Color(0.18f, 0.21f, 0.28f), 0.45f);
            DungeonUITheme.ApplyCard(cardBackground, tinted);
        }

        if (portraitIcon == null)
        {
            var found = transform.Find("Portrait/Icon") ?? transform.Find("PortraitIcon");
            if (found != null) portraitIcon = found.GetComponent<Image>();
        }
        if (portraitIcon != null)
        {
            string prefix = SpriteMapping.GetHeroSpritePrefix(hero.data.heroName, hero.data.spritePrefix);
            var spr = SpriteCache.LoadHeroOrBossSprite(prefix);
            if (spr != null)
            {
                portraitIcon.sprite = spr;
                portraitIcon.preserveAspect = true;
                portraitIcon.color = Color.white;
                portraitIcon.enabled = true;
            }
            else
            {
                portraitIcon.color = hero.data.classColor;
            }
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        onTap?.Invoke(this);
    }

    public void RefreshBars()
    {
        if (heroInstance == null) return;
        if (hpBar) hpBar.value = heroInstance.currentHP;
        if (energyBar) energyBar.value = heroInstance.currentEnergy;
    }
}
