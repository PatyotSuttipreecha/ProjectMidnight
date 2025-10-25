using UnityEngine;
using UnityEngine.EventSystems;

public class InventorySlotUI : MonoBehaviour, IDropHandler
{
    public int x;
    public int y;
    private InventoryGrid grid;

    private void Start()
    {
        grid = GetComponentInParent<InventoryGrid>();
    }

    public void OnDrop(PointerEventData eventData)
    {
        var draggedItem = eventData.pointerDrag.GetComponent<InventoryItemUI>();
        if (draggedItem != null)
        {
            ItemSO item = draggedItem.itemData;
            if (grid.CanPlaceItem(item, x, y))
            {
                grid.PlaceItem(item, x, y);
                draggedItem.GetComponent<RectTransform>().anchoredPosition = GetComponent<RectTransform>().anchoredPosition;
            }
            else
            {
                draggedItem.GetComponent<RectTransform>().anchoredPosition = draggedItem.GetComponent<InventoryItemUI>().originalPosition;
            }
        }
    }
}
