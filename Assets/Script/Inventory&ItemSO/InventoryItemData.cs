using UnityEngine;

[System.Serializable]
public class InventoryItemData
{
    public ItemSO itemSO;
    public int uniqueID;

    // instance-specific state
    public int currentWidth;
    public int currentHeight;
    public bool isRotated;

    public InventoryItemData(ItemSO so)
    {
        itemSO = so;
        uniqueID = Random.Range(100000, 999999);
        currentWidth = so.width;
        currentHeight = so.height;
        isRotated = false;
    }

    public string ItemName => itemSO.itemName;
    public Sprite Icon => itemSO.icon;

    public int Width => currentWidth;
    public int Height => currentHeight;
}
