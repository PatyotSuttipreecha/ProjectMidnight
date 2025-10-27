using UnityEngine;

[System.Serializable]
public class InventoryGrid
{
    public int width;
    public int height;
    public ItemSO[,] grid;

    public InventoryGrid(int width, int height)
    {
        this.width = width;
        this.height = height;
        grid = new ItemSO[width, height];
    }

    public bool CanPlaceItem(ItemSO item, int x, int y)
    {
        if (x + item.width > width || y + item.height > height)
            return false;

        for (int i = 0; i < item.width; i++)
        {
            for (int j = 0; j < item.height; j++)
            {
                if (grid[x + i, y + j] != null)
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
                grid[x + i, y + j] = item;
            }
        }
    }
}
