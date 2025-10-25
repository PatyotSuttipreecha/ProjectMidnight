using UnityEngine;

public class InventoryGrid : MonoBehaviour
{
    public int gridWidth = 8;
    public int gridHeight = 8;
    private ItemSO[,] gridItems;

    private void Awake()
    {
        gridItems = new ItemSO[gridWidth, gridHeight];
    }

    public bool CanPlaceItem(ItemSO item, int x, int y)
    {
        for (int i = 0; i < item.width; i++)
        {
            for (int j = 0; j < item.height; j++)
            {
                if (x + i >= gridWidth || y + j >= gridHeight || gridItems[x + i, y + j] != null)
                    return false;
            }
        }
        return true;
    }

    public void PlaceItem(ItemSO item, int x, int y)
    {
        for (int i = 0; i < item.width; i++)
        {
            for (int j = 0; j < item.height; j++)
            {
                gridItems[x + i, y + j] = item;
            }
        }
    }

    public void RemoveItem(ItemSO item)
    {
        for (int i = 0; i < gridWidth; i++)
        {
            for (int j = 0; j < gridHeight; j++)
            {
                if (gridItems[i, j] == item)
                    gridItems[i, j] = null;
            }
        }
    }
}
