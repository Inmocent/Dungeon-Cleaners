using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Enables smooth mobile drag-and-drop on a GridSlotUI tile using a floating drag avatar.
/// Avoids unparenting from GridLayoutGroup, preventing visual layout shifts or flickering.
/// </summary>
public class GridSlotDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int sourceSlot;

    private GridSlotUI slotUI;
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private GameObject dragAvatar;

    void Awake()
    {
        slotUI = GetComponent<GridSlotUI>();
        canvas = GetComponentInParent<Canvas>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (slotUI == null || slotUI.hero == null)
        {
            eventData.pointerDrag = null; // Do not drag empty slots
            return;
        }

        sourceSlot = slotUI.slotIndex;

        // Dim source slot slightly while dragging
        if (canvasGroup != null) canvasGroup.alpha = 0.45f;

        // Create floating drag avatar under canvas root
        if (canvas != null)
        {
            dragAvatar = new GameObject("DragAvatar_Hero", typeof(RectTransform), typeof(CanvasGroup));
            dragAvatar.transform.SetParent(canvas.transform, false);
            dragAvatar.transform.SetAsLastSibling();

            var rt = dragAvatar.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(130, 130);
            rt.position = eventData.position;

            var img = dragAvatar.AddComponent<Image>();
            img.color = slotUI.GetBaseColor();

            var textGO = new GameObject("AvatarLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(dragAvatar.transform, false);
            var textRt = textGO.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;

            var tmp = textGO.GetComponent<TextMeshProUGUI>();
            tmp.text = slotUI.hero.displayName;
            tmp.fontSize = 15;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;

            var cg = dragAvatar.GetComponent<CanvasGroup>();
            cg.blocksRaycasts = false; // Must allow raycasts to pass through to IDropHandler underneath!
            cg.alpha = 0.85f;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragAvatar != null)
        {
            dragAvatar.transform.position = eventData.position;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (canvasGroup != null) canvasGroup.alpha = 1f;

        if (dragAvatar != null)
        {
            Destroy(dragAvatar);
            dragAvatar = null;
        }
    }

    void OnDisable()
    {
        if (dragAvatar != null)
        {
            Destroy(dragAvatar);
            dragAvatar = null;
        }
        if (canvasGroup != null) canvasGroup.alpha = 1f;
    }
}
