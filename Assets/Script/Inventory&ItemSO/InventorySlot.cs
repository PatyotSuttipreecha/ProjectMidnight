using UnityEngine;
using UnityEngine.UI;

public class InventorySlot : MonoBehaviour
{
    public bool isOccupied = false;
    public Image iconImage;
    private ItemSO itemData;

    public void AddItem(ItemSO newItem)
    {
        itemData = newItem;
        iconImage.sprite = newItem.icon;
        iconImage.enabled = true;
        isOccupied = true;
    }

    public void ClearSlot()
    {
        itemData = null;
        iconImage.sprite = null;
        iconImage.enabled = false;
        isOccupied = false;
    }
}
