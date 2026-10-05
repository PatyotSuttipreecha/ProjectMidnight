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
    [Min(1)] public int quantity = 1;
    public bool hasWeaponAmmo;
    public int magazineAmmo;
    public int reserveAmmo;

    public InventoryItemData(ItemSO so)
    {
        itemSO = so;
        uniqueID = Random.Range(100000, 999999);
        currentWidth = so.width;
        currentHeight = so.height;
        isRotated = false;
        if (so.itemType == ItemType.Weapon)
        {
            hasWeaponAmmo = true;
            magazineAmmo = Mathf.Max(0, so.startingMagazineAmmo);
            reserveAmmo = Mathf.Max(0, so.startingReserveAmmo);
        }
    }

    public string ItemName => itemSO.itemName;
    public Sprite Icon => itemSO.icon;

    public int Width => currentWidth;
    public int Height => currentHeight;
}
