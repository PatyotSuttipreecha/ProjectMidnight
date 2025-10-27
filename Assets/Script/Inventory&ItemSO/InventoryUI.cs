using UnityEngine;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    public GameObject slotPrefab;
    public GameObject itemUIPrefab;
    public RectTransform gridParent;

    private InventoryGrid gridData;

    void Start()
    {
        gridData = InventoryManager.Instance.inventoryGrid;
        CreateGrid();
        RefreshUI();
    }

    public void CreateGrid()
    {
        foreach (Transform child in gridParent)
            Destroy(child.gameObject);

        for (int y = 0; y < gridData.height; y++)
        {
            for (int x = 0; x < gridData.width; x++)
            {
                Instantiate(slotPrefab, gridParent);
            }
        }
    }

    public void RefreshUI()
    {
        foreach (Transform child in gridParent)
        {
            // ลบ icon เก่าออกก่อน
            foreach (Transform sub in child)
                Destroy(sub.gameObject);
        }

        for (int x = 0; x < gridData.width; x++)
        {
            for (int y = 0; y < gridData.height; y++)
            {
                ItemSO item = gridData.grid[x, y];
                if (item != null)
                {
                    int index = y * gridData.width + x;
                    Transform slot = gridParent.GetChild(index);

                    GameObject icon = Instantiate(itemUIPrefab, slot);
                    icon.GetComponent<Image>().sprite = item.icon;
                }
            }
        }
    }
}
