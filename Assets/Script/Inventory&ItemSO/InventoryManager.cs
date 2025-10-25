using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public InventorySlot[] slots;

    public bool AddItem(ItemSO newItem)
    {
        foreach (var slot in slots)
        {
            if (!slot.isOccupied)
            {
                slot.AddItem(newItem);
                return true;
            }
        }
        Debug.Log("Inventory Full!");
        return false;
    }

    public void RemoveItem(ItemSO itemToRemove)
    {
        foreach (var slot in slots)
        {
            // ��Ҫ�ͧ����ͪ�ͧ�������������
            if (slot.isOccupied && slot.name == itemToRemove.name)
            {
                slot.ClearSlot();
                return;
            }
        }
    }
}
