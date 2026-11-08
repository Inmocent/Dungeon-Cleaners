using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// A single slot in the 3x3 formation grid. Supports tap-to-swap, tap-to-select, and drag-drop.
/// Designed with generous hitboxes for mobile screens.
/// </summary>
public class GridSlotUI : MonoBehaviour, IPointerClickHandler, IDropHandler
{
    public Image background;
    public Image selectionOutline;
    public TextMeshProUGUI heroLabel;
    public TextMeshProUGUI hpLabel;
    public Image warningIcon;  // shown when fatigued

    [HideInInspector] public int slotIndex;
    [HideInInspector] public HeroInstance hero;

    private System.Action<int> onTap;
    private System.Action<int, int> onDrop; // (from, to)

    public static readonly Color EmptyColor      = new Color(0.14f, 0.15f, 0.22f, 0.85f);
    public static readonly Color KnightColor     = new Color(0.2f, 0.42f, 0.82f, 0.95f);
    public static readonly Color MageColor       = new Color(0.55f, 0.22f, 0.82f, 0.95f);
    public static readonly Color HealerColor     = new Color(0.2f, 0.72f, 0.42f, 0.95f);
    public static readonly Color SelectedBorder  = new Color(1f, 0.85f, 0.2f, 1f);
    public static readonly Color NormalBorder    = new Color(0.3f, 0.35f, 0.45f, 0.5f);

    public void Setup(int index, HeroInstance h, System.Action<int> tapCb, System.Action<int, int> dropCb)
    {
        slotIndex = index;
        hero = h;
        onTap = tapCb;
        onDrop = dropCb;

        var drag = GetComponent<GridSlotDrag>();
        if (drag != null)
        {
            drag.sourceSlot = index;
        }

        bool isEmpty = h == null;
        if (heroLabel != null)
        {
            heroLabel.raycastTarget = false;
            heroLabel.text = isEmpty ? $"[Slot {index + 1}]" : h.displayName;
        }

        if (hpLabel != null)
        {
            hpLabel.raycastTarget = false;
            hpLabel.text = isEmpty ? "Empty" : $"{h.currentHP}/{h.data.maxHP} HP";
        }

        if (background != null)
        {
            background.color = GetBaseColor();
        }

        if (selectionOutline != null)
        {
            selectionOutline.color = NormalBorder;
        }

        if (warningIcon != null)
        {
            warningIcon.raycastTarget = false;
            warningIcon.gameObject.SetActive(h != null && h.isFatigued);
        }
    }

    public Color GetBaseColor()
    {
        if (hero == null) return EmptyColor;
        return hero.data.heroClass switch
        {
            HeroClass.Knight => KnightColor,
            HeroClass.Mage   => MageColor,
            HeroClass.Healer => HealerColor,
            _                => EmptyColor
        };
    }

    public void SetSelected(bool isSelected)
    {
        if (selectionOutline != null)
        {
            selectionOutline.color = isSelected ? SelectedBorder : NormalBorder;
        }
        else if (background != null)
        {
            background.color = isSelected ? Color.Lerp(GetBaseColor(), Color.yellow, 0.35f) : GetBaseColor();
        }
    }

    public void Refresh()
    {
        if (hero == null)
        {
            if (heroLabel) heroLabel.text = $"[Slot {slotIndex + 1}]";
            if (hpLabel) hpLabel.text = "Empty";
            if (background) background.color = EmptyColor;
            if (warningIcon) warningIcon.gameObject.SetActive(false);
            return;
        }

        if (hpLabel) hpLabel.text = $"{hero.currentHP}/{hero.data.maxHP} HP";
        if (warningIcon) warningIcon.gameObject.SetActive(hero.isFatigued);
        if (background) background.color = GetBaseColor();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        onTap?.Invoke(slotIndex);
    }

    public void OnDrop(PointerEventData eventData)
    {
        var dragged = eventData.pointerDrag?.GetComponent<GridSlotDrag>();
        if (dragged != null && dragged.sourceSlot != slotIndex)
        {
            onDrop?.Invoke(dragged.sourceSlot, slotIndex);
        }
    }
}
