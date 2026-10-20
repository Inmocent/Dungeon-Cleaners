using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>A single team slot in the Squad Builder (shows +/- button).</summary>
public class TeamSlotUI : MonoBehaviour
{
    public TextMeshProUGUI heroLabel;
    public TextMeshProUGUI hpLabel;
    public Image slotBackground;
    public Button actionButton;         // + when empty, - when filled
    public TextMeshProUGUI actionLabel; // "+" or "-"

    [HideInInspector] public HeroInstance heroInstance;
    private System.Action onActionClicked;

    // Color states
    private static readonly Color EmptyColor  = new Color(0.25f, 0.25f, 0.25f);
    private static readonly Color FilledColor = new Color(0.2f, 0.6f, 0.3f);

    public void Setup(HeroInstance hero, System.Action actionCallback)
    {
        heroInstance = hero;
        onActionClicked = actionCallback;

        actionButton.onClick.RemoveAllListeners();
        actionButton.onClick.AddListener(() => onActionClicked?.Invoke());

        if (hero == null)
        {
            heroLabel.text = "Empty";
            if (hpLabel) hpLabel.text = "";
            actionLabel.text = "+";
            if (slotBackground) slotBackground.color = EmptyColor;
            actionButton.interactable = false; // can't remove empty slot
        }
        else
        {
            heroLabel.text = hero.displayName;
            if (hpLabel) hpLabel.text = $"{hero.currentHP}/{hero.data.maxHP} HP";
            actionLabel.text = "-";
            if (slotBackground) slotBackground.color = FilledColor;
            actionButton.interactable = true;
        }
    }
}