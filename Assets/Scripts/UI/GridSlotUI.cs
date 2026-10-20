using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>A single slot in the 3x3 formation grid. Supports tap + drag-drop swap.</summary>
public class GridSlotUI : MonoBehaviour, IPointerClickHandler, IDropHandler
{
    public Image background;
    public TextMeshProUGUI heroLabel;
    public TextMeshProUGUI hpLabel;
    public Image warningIcon;  // shown when fatigued

    [HideInInspector] public int slotIndex;
    [HideInInspector] public HeroInstance hero;

    private System.Action<int> onTap;
    private System.Action<int, int> onDrop; // (from, to)

    static readonly Color EmptyColor  = new Color(0.15f, 0.15f, 0.2f, 0.8f);
    static readonly Color KnightColor = new Color(0.2f, 0.4f, 0.8f, 0.9f);
    static readonly Color MageColor   = new Color(0.5f, 0.2f, 0.8f, 0.9f);
    static readonly Color HealerColor = new Color(0.2f, 0.7f, 0.4f, 0.9f);

    public void Setup(int index, HeroInstance h, System.Action<int> tapCb, System.Action<int,int> dropCb)
    {
        slotIndex = index;
        hero = h;
        onTap = tapCb;
        onDrop = dropCb;

        bool isEmpty = h == null;
        heroLabel.text = isEmpty ? $"[{index+1}]" : h.displayName;
        hpLabel.text   = isEmpty ? "" : $"{h.currentHP}/{h.data.maxHP}";

        if (background)
        {
            if (isEmpty) background.color = EmptyColor;
            else background.color = h.data.heroClass switch
            {
                HeroClass.Knight => KnightColor,
                HeroClass.Mage   => MageColor,
                HeroClass.Healer => HealerColor,
                _                => EmptyColor
            };
        }

        if (warningIcon) warningIcon.gameObject.SetActive(h != null && h.isFatigued);
    }

    public void Refresh()
    {
        if (hero == null) return;
        hpLabel.text = $"{hero.currentHP}/{hero.data.maxHP}";
        if (warningIcon) warningIcon.gameObject.SetActive(hero.isFatigued);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        onTap?.Invoke(slotIndex);
    }

    // Basic drag support via IDropHandler — relies on a DragHandler component on the card
    public void OnDrop(PointerEventData eventData)
    {
        var dragged = eventData.pointerDrag?.GetComponent<GridSlotDrag>();
        if (dragged != null && dragged.sourceSlot != slotIndex)
        {
            onDrop?.Invoke(dragged.sourceSlot, slotIndex);
        }
    }
}