using UnityEngine;

[System.Serializable]
public class InventoryGrid
{
    public int width;
    public int height;
    public InventoryItemData[,] grid;

    public InventoryGrid(int width, int height)
    {
        this.width = width;
        this.height = height;
        grid = new InventoryItemData[width, height];
    }

    public bool CanPlaceItem(InventoryItemData item, int startX, int startY)
    {
        return item != null && CanPlaceFootprint(item.Width, item.Height, startX, startY);
    }

    public bool CanPlaceFootprint(int itemWidth, int itemHeight, int startX, int startY, InventoryItemData ignore = null)
    {
        if (grid == null || itemWidth <= 0 || itemHeight <= 0) return false;

        if (startX < 0 || startY < 0 ||
            startX + itemWidth > width ||
            startY + itemHeight > height)
            return false;

        for (int x = 0; x < itemWidth; x++)
            for (int y = 0; y < itemHeight; y++)
                if (grid[startX + x, startY + y] != null && grid[startX + x, startY + y] != ignore)
                    return false;

        return true;
    }

    public bool PlaceItem(InventoryItemData item, int startX, int startY)
    {
        if (!CanPlaceItem(item, startX, startY)) return false;

        for (int x = 0; x < item.Width; x++)
            for (int y = 0; y < item.Height; y++)
                grid[startX + x, startY + y] = item;

        Debug.Log($"Placed {item.ItemName} at ({startX},{startY}) size {item.Width}x{item.Height}");
        return true;
    }

    public void ClearItem(InventoryItemData item)
    {
        if (item == null || grid == null) return;

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (grid[x, y] == item)
                    grid[x, y] = null;
    }

    // Find first available top-left slot to place item (scan left->right, top->bottom)
    // Returns true and out position if found
    public bool FindFirstAvailableSlot(InventoryItemData item, out int outX, out int outY)
    {
        outX = -1; outY = -1;
        if (item == null) return false;

        for (int y = 0; y < height; y++) // top -> bottom (y increasing is down)
        {
            for (int x = 0; x < width; x++)
            {
                if (CanPlaceItem(item, x, y))
                {
                    outX = x; outY = y;
                    return true;
                }
            }
        }
        return false;
    }

    // Get top-left coordinates of an item instance (if exists), otherwise -1,-1
    public bool FindTopLeftOfItem(InventoryItemData item, out int topLeftX, out int topLeftY)
    {
        topLeftX = -1; topLeftY = -1;
        if (item == null) return false;

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] == item)
                {
                    // ensure this position is top-left (no same item on left or above)
                    if ((x == 0 || grid[x - 1, y] != item) && (y == 0 || grid[x, y - 1] != item))
                    {
                        topLeftX = x; topLeftY = y;
                        return true;
                    }
                }
            }
        return false;
    }
}
