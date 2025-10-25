using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public ItemSO itemData;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    [HideInInspector]public Vector2 originalPosition;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
    }

    public void Initialize(ItemSO item)
    {
        itemData = item;
        GetComponent<Image>().sprite = item.icon;
        rectTransform.sizeDelta = new Vector2(item.width * 100, item.height * 100); // ขนาดไอเท็มตามช่อง
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalPosition = rectTransform.anchoredPosition;
        canvasGroup.alpha = 0.6f;
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        rectTransform.anchoredPosition += eventData.delta;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        // ถ้าไม่ได้วางบนช่อง ให้กลับที่เดิม
        rectTransform.anchoredPosition = originalPosition;
    }
}
