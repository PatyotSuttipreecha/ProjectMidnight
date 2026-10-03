using System.Collections.Generic;
using UnityEngine;
using System;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Header("Grid settings (editable in Inspector)")]
    public int gridWidth = 4;
    public int gridHeight = 4;

    public event Action OnInventoryChanged;

    [HideInInspector] public InventoryGrid inventoryGrid;

    // mapping for UI creation (inventoryUI uses this)
    public Dictionary<InventoryItemData, Vector2Int> placedPositions = new Dictionary<InventoryItemData, Vector2Int>();

    private void Awake()
    {
        if (Instance == null) Instance = this;

        else { Destroy(gameObject); return; }

        gridWidth = Mathf.Max(1, gridWidth); gridHeight = Mathf.Max(1, gridHeight);
        inventoryGrid = new InventoryGrid(gridWidth, gridHeight);
    }

    private void Start()
    {
        // nothing else; UI will call Refresh when needed
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    // Try add ItemSO by creating InventoryItemData and auto place
    public bool TryAddItemAutoPlace(ItemSO baseItem)
    {
        if (baseItem == null || baseItem.width <= 0 || baseItem.height <= 0) return false;

        InventoryItemData newItem = new InventoryItemData(baseItem);

        return TryAddInstance(newItem);
    }

    public bool TryAddInstance(InventoryItemData newItem)
    {
        if (newItem == null || newItem.itemSO == null || inventoryGrid == null || placedPositions.ContainsKey(newItem)) return false;
        if (inventoryGrid.FindFirstAvailableSlot(newItem, out int x, out int y) && inventoryGrid.PlaceItem(newItem, x, y))
        {
            placedPositions[newItem] = new Vector2Int(x, y);

            OnInventoryChanged?.Invoke(); // 🔥 สำคัญ
            return true;
        }

        Debug.Log("Inventory Full - cannot place " + newItem.ItemName);
        return false;
    }


    // Use item at top-left slot x,y (called from UI)
    public void UseItemAt(int x, int y)
    {
        if (!inventoryGridFindValid(x, y)) return;

        var item = inventoryGrid.grid[x, y];
        if (item == null) return;

        // find top-left pos to ensure we only use once
        if (!inventoryGrid.FindTopLeftOfItem(item, out int tlx, out int tly)) return;

        // perform use logic based on itemSO (same as before)
        switch (item.itemSO.itemType)
        {
            case ItemType.Health:
                Debug.Log($"Use {item.ItemName} heal {item.itemSO.healAmount}");

                PlayerController player = PlayerController.instance;
                if (player == null || player.currentHealth <= 0f || player.currentHealth >= player.maxHealth || item.itemSO.healAmount <= 0) return;


                player.currentHealth += item.itemSO.healAmount;

                player.currentHealth = Mathf.Clamp(player.currentHealth, 0, player.maxHealth);
                break;
            case ItemType.Weapon:
                Debug.Log("Weapon equipping is not implemented; the item stays in the inventory.");
                return;
            case ItemType.Ammo:
                if (!TryApplyAmmo(item.itemSO, PlayerController.instance != null ? PlayerController.instance.transform : null)) return;
                break;
            default:
                Debug.Log("This item has no use action; it stays in the inventory.");
                return;
        }

        // remove from grid (clear all occupied cells)
        inventoryGrid.ClearItem(item);
        if (placedPositions.ContainsKey(item)) placedPositions.Remove(item);

        OnInventoryChanged?.Invoke();
    }

    // Remove item (drop) at top-left
    public void RemoveItemAt(int x, int y)
    {
        if (!inventoryGridFindValid(x, y)) return;
        var item = inventoryGrid.grid[x, y];
        if (item == null) return;
        PlayerController player = PlayerController.instance;
        if (player == null || item.itemSO.pickupPrefab == null)
        { Debug.LogWarning("Cannot drop: assign a Pickup Prefab on the ItemSO and ensure a player exists."); return; }
        Vector3 position = player.transform.position + player.transform.forward * 1.2f + Vector3.up * 0.2f;
        PickupItem dropped = Instantiate(item.itemSO.pickupPrefab, position, Quaternion.identity);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(dropped.gameObject, player.gameObject.scene);
        dropped.SetStoredItem(item);
        dropped.enabled = true;
        dropped.gameObject.SetActive(true);
        inventoryGrid.ClearItem(item);
        if (placedPositions.ContainsKey(item)) placedPositions.Remove(item);
        OnInventoryChanged?.Invoke();
    }

    private bool inventoryGridFindValid(int x, int y)
    {
        return inventoryGrid != null && (x >= 0 && y >= 0 && x < inventoryGrid.width && y < inventoryGrid.height);
    }

    // helper: resize grid at runtime (if user adjusts in inspector or via UI)
    public void ResizeGrid(int newWidth, int newHeight)
    {
        if (newWidth <= 0 || newHeight <= 0 || inventoryGrid == null) return;
        // create new grid and copy existing items where possible (simple approach: re-place items if possible)
        InventoryGrid newGrid = new InventoryGrid(newWidth, newHeight);

        List<InventoryItemData> allItems = new List<InventoryItemData>();
        for (int x = 0; x < inventoryGrid.width; x++)
            for (int y = 0; y < inventoryGrid.height; y++)
                if (inventoryGrid.grid[x, y] != null && !allItems.Contains(inventoryGrid.grid[x, y]))
                    allItems.Add(inventoryGrid.grid[x, y]);

        var newPositions = new Dictionary<InventoryItemData, Vector2Int>();

        // try re-place old items in same order
        foreach (var it in allItems)
        {
            if (newGrid.FindFirstAvailableSlot(it, out int px, out int py) && newGrid.PlaceItem(it, px, py))
            {
                newPositions[it] = new Vector2Int(px, py);
            }
            else
            {
                Debug.LogWarning("Resize cancelled: not all items fit. The original inventory is unchanged.");
                return;
            }
        }

        inventoryGrid = newGrid;
        placedPositions = newPositions;
        gridWidth = newWidth; gridHeight = newHeight;
        OnInventoryChanged?.Invoke();
    }

    public static bool TryApplyAmmo(ItemSO item, Transform owner)
    {
        if (item == null || item.itemType != ItemType.Ammo || item.ammoAmount <= 0 || owner == null) return false;
        foreach (Guns gun in owner.GetComponentsInChildren<Guns>(true))
            if (gun.weaponStat.weaponName == item.weaponType && item.weaponType != Guns.WeaponType.None && item.weaponType != Guns.WeaponType.Knife)
            { gun.AddAmmo(item.ammoAmount); return true; }
        return false;
    }
}
