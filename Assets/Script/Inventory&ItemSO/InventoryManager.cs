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
    public void NotifyEquipmentChanged() => OnInventoryChanged?.Invoke();

    public bool TryUseKey(ItemSO key, bool consume)
    {
        if (key == null || key.itemType != ItemType.Key || inventoryGrid == null) return false;
        InventoryItemData found = null;
        foreach (var item in placedPositions.Keys)
            if (item.itemSO == key && item.quantity > 0) { found = item; break; }
        if (found == null) return false;
        if (!consume) return true;
        if (--found.quantity == 0) { inventoryGrid.ClearItem(found); placedPositions.Remove(found); }
        OnInventoryChanged?.Invoke();
        return true;
    }

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
    public bool TryAddItemAutoPlace(ItemSO baseItem, int quantity = 1)
    {
        if (baseItem == null || baseItem.width <= 0 || baseItem.height <= 0) return false;

        InventoryItemData newItem = new InventoryItemData(baseItem) { quantity = quantity };

        return TryAddInstance(newItem);
    }

    public bool TryAddInstance(InventoryItemData newItem)
    {
        if (newItem == null || newItem.itemSO == null || newItem.quantity <= 0 || newItem.Width <= 0 || newItem.Height <= 0
            || inventoryGrid == null || placedPositions.ContainsKey(newItem)) return false;
        // Plan the entire pickup first. A failed pickup must not partially fill existing stacks.
        int remaining = newItem.quantity;
        int limit = newItem.itemSO.StackLimit;
        var additions = new Dictionary<InventoryItemData, int>();
        foreach (var existing in placedPositions.Keys)
        {
            if (existing.itemSO != newItem.itemSO || limit == 1) continue;
            int added = Mathf.Min(remaining, Mathf.Max(0, limit - existing.quantity));
            if (added > 0) additions[existing] = added;
            remaining -= added;
            if (remaining == 0) break;
        }
        var plannedGrid = new InventoryGrid(inventoryGrid.width, inventoryGrid.height);
        Array.Copy(inventoryGrid.grid, plannedGrid.grid, inventoryGrid.grid.Length);
        var placements = new Dictionary<InventoryItemData, Vector2Int>();
        var counts = new Dictionary<InventoryItemData, int>();
        while (remaining > 0)
        {
            InventoryItemData stack = placements.Count == 0 ? newItem : new InventoryItemData(newItem.itemSO)
            { currentWidth = newItem.Width, currentHeight = newItem.Height, isRotated = newItem.isRotated,
                hasWeaponAmmo = newItem.hasWeaponAmmo, magazineAmmo = newItem.magazineAmmo, reserveAmmo = newItem.reserveAmmo };
            if (!plannedGrid.FindFirstAvailableSlot(stack, out int x, out int y)) return false;
            plannedGrid.PlaceItem(stack, x, y);
            placements[stack] = new Vector2Int(x, y);
            counts[stack] = Mathf.Min(remaining, limit);
            remaining -= counts[stack];
        }
        foreach (var pair in additions) pair.Key.quantity += pair.Value;
        foreach (var pair in placements)
        {
            pair.Key.quantity = counts[pair.Key];
            placedPositions[pair.Key] = pair.Value;
        }
        inventoryGrid = plannedGrid;
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool CanMoveItem(InventoryItemData item, int x, int y, bool rotated)
    {
        if (item == null || item.itemSO == null || !placedPositions.ContainsKey(item)
            || (rotated != item.isRotated && !item.itemSO.allowRotation)) return false;
        int width = rotated ? item.itemSO.height : item.itemSO.width;
        int height = rotated ? item.itemSO.width : item.itemSO.height;
        return inventoryGrid.CanPlaceFootprint(width, height, x, y, item);
    }

    public bool TryMoveItem(InventoryItemData item, int x, int y, bool rotated)
    {
        if (!CanMoveItem(item, x, y, rotated)) return false;
        inventoryGrid.ClearItem(item);
        item.isRotated = rotated;
        item.currentWidth = rotated ? item.itemSO.height : item.itemSO.width;
        item.currentHeight = rotated ? item.itemSO.width : item.itemSO.height;
        inventoryGrid.PlaceItem(item, x, y);
        placedPositions[item] = new Vector2Int(x, y);
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool TryMergeItems(InventoryItemData source, InventoryItemData target)
    {
        if (source == null || target == null || source == target || source.itemSO != target.itemSO
            || !placedPositions.ContainsKey(source) || !placedPositions.ContainsKey(target)) return false;
        int amount = Mathf.Min(source.quantity, source.itemSO.StackLimit - target.quantity);
        if (amount <= 0) return false;
        source.quantity -= amount;
        target.quantity += amount;
        if (source.quantity == 0) { inventoryGrid.ClearItem(source); placedPositions.Remove(source); }
        OnInventoryChanged?.Invoke();
        return true;
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
                if (player == null || !player.TryUseHealing(item.itemSO)) return;
                break;
            case ItemType.Weapon:
                if (PlayerController.instance != null) PlayerController.instance.TryEquipWeapon(item);
                return;
            case ItemType.Ammo:
                if (!TryApplyAmmo(item.itemSO, PlayerController.instance != null ? PlayerController.instance.transform : null)) return;
                break;
            default:
                Debug.Log("This item has no use action; it stays in the inventory.");
                return;
        }

        // One use consumes one unit, not the entire stack.
        item.quantity--;
        if (item.quantity <= 0)
        {
            inventoryGrid.ClearItem(item);
            placedPositions.Remove(item);
        }

        OnInventoryChanged?.Invoke();
    }

    // Remove item (drop) at top-left
    public void RemoveItemAt(int x, int y)
    {
        if (!inventoryGridFindValid(x, y)) return;
        var item = inventoryGrid.grid[x, y];
        if (item == null) return;
        PlayerController player = PlayerController.instance;
        if (player != null && player.EquippedInventoryItem == item && player.EquippedGun != null)
        {
            if (player.EquippedGun.isReloading) { Debug.LogWarning("Finish reloading before dropping this weapon."); return; }
            player.EquippedGun.StoreInventoryAmmo();
        }
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
        PlayerController player = owner.GetComponent<PlayerController>();
        if (player != null) return player.TryAddAmmoToOwnedWeapon(item);
        foreach (Guns gun in owner.GetComponentsInChildren<Guns>(true))
            if (gun.weaponStat.weaponName == item.weaponType && item.weaponType != Guns.WeaponType.None && item.weaponType != Guns.WeaponType.Knife)
            { gun.AddAmmo(item.ammoAmount); return true; }
        return false;
    }
}
