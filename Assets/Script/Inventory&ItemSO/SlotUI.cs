using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attach to slot prefab. Will call InventoryUI_Auto.OnSlotRightClick on right mouse button.
/// </summary>
public class SlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int x;
    public int y;
    private InventoryUI ui;

    public void Setup(int _x, int _y, InventoryUI _ui)
    {
        x = _x; y = _y; ui = _ui;
    }

    public void OnBeginDrag(PointerEventData eventData) => ui.BeginDrag(x, y, eventData);
    public void OnDrag(PointerEventData eventData) => ui.Drag(eventData);
    public void OnEndDrag(PointerEventData eventData) => ui.EndDrag(eventData);

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            ui.OnSlotRightClick(x, y);
        }
    }
}
