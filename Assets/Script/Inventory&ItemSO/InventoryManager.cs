using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;
    public InventoryGrid inventoryGrid;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);

        inventoryGrid = new InventoryGrid(4, 4); // 8x6 grid
    }

    public bool AddItem(ItemSO item)
    {
        for (int x = 0; x < inventoryGrid.width; x++)
        {
            for (int y = 0; y < inventoryGrid.height; y++)
            {
                if (inventoryGrid.CanPlaceItem(item, x, y))
                {
                    inventoryGrid.PlaceItem(item, x, y);
                    Debug.Log($"🧩 Added {item.itemName} at ({x},{y})");

                    // ✅ อัปเดต UI ทันที
                    var ui = FindAnyObjectByType<InventoryUI>();
                    if (ui != null)
                        ui.RefreshUI();

                    return true;
                }
            }
        }

        Debug.Log("❌ No space to add item!");
        return false;
    }


}
