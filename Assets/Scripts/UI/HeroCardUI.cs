using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>A single hero card in the roster — tappable, color-coded by class.</summary>
public class HeroCardUI : MonoBehaviour, IPointerClickHandler
{
    public TextMeshProUGUI nameLabel;
    public TextMeshProUGUI classLabel;
    public Slider hpBar;
    public Slider energyBar;
    public Image cardBackground;

    [HideInInspector] public HeroInstance heroInstance;
    private System.Action<HeroCardUI> onTap;

    public void Setup(HeroInstance hero, System.Action<HeroCardUI> tapCallback)
    {
        heroInstance = hero;
        onTap = tapCallback;

        if (nameLabel) nameLabel.text = hero.displayName;
        if (classLabel) classLabel.text = hero.data.heroClass.ToString();

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

        // Color the card by hero class
        if (cardBackground)
            cardBackground.color = hero.data.classColor;
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