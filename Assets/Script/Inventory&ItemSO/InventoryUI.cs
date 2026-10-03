using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Inventory UI (Auto-place). No drag/drop.
/// - gridParent should have GridLayoutGroup configured
/// - slotPrefab must contain an Image (background) and optionally a child placeholder for icon
/// - itemUIPrefab should be an Image used to represent placed items (we will set sprite and size)
/// </summary>
public class InventoryUI : MonoBehaviour
{

    [Header("References")]
    public RectTransform gridParent;       // the parent that will hold slot GameObjects (GridLayoutGroup)
    public GameObject slotPrefab;          // prefab for each slot (should have SlotUI component)
    public GameObject itemUIPrefab;        // prefab for item visual (Image)
    public float cellSize = 150f;          // UI cell size (in GridLayoutGroup you'll set same)
    public float spacing = 4f;

    // internal
    private List<SlotUI> slotUIs = new List<SlotUI>();
    private Dictionary<InventoryItemData, RectTransform> itemToUI = new Dictionary<InventoryItemData, RectTransform>();
    private bool isBuilt = false;
    private InventoryManager subscribedManager;
    private int builtWidth;
    private int builtHeight;

    private void OnEnable()
    {
        BindManager();
    }
    private void Start() { BindManager(); }
    private void Update() { if (subscribedManager != InventoryManager.Instance || !isBuilt) BindManager(); }
    private void BindManager()
    {
        InventoryManager manager = InventoryManager.Instance;
        if (manager == null || manager.inventoryGrid == null || gridParent == null || slotPrefab == null || itemUIPrefab == null) return;
        if (subscribedManager != manager)
        {
            if (subscribedManager != null) subscribedManager.OnInventoryChanged -= RefreshUI;
            subscribedManager = manager;
            subscribedManager.OnInventoryChanged += RefreshUI;
            isBuilt = false;
        }
        if (!isBuilt) BuildGridVisuals();
        RefreshUI();
    }

    private void OnDisable()
    {
        if (subscribedManager != null)
        {
            subscribedManager.OnInventoryChanged -= RefreshUI;
        }
        subscribedManager = null;
    }

    // Build slot GameObjects according to current grid size
    public void BuildGridVisuals()
    {
        if (InventoryManager.Instance == null || InventoryManager.Instance.inventoryGrid == null || gridParent == null || slotPrefab == null) return;
        // clear old
        for (int i = gridParent.childCount - 1; i >= 0; i--)
        { Transform child = gridParent.GetChild(i); child.SetParent(null, false); Destroy(child.gameObject); }
        slotUIs.Clear();
        itemToUI.Clear();

        // ensure GridLayoutGroup matches cellSize & spacing & constraint
        GridLayoutGroup layout = gridParent.GetComponent<GridLayoutGroup>();
        if (layout == null) layout = gridParent.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(cellSize, cellSize);
        layout.spacing = new Vector2(spacing, spacing);
        layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = InventoryManager.Instance.inventoryGrid.width;

        int total = InventoryManager.Instance.inventoryGrid.width * InventoryManager.Instance.inventoryGrid.height;
        builtWidth = InventoryManager.Instance.inventoryGrid.width;
        builtHeight = InventoryManager.Instance.inventoryGrid.height;
        isBuilt = true;

        for (int i = 0; i < total; i++)
        {
            GameObject s = Instantiate(slotPrefab, gridParent);
            s.name = $"Slot [{i}]";
            SlotUI su = s.GetComponent<SlotUI>();
            if (su == null) su = s.AddComponent<SlotUI>();
            int index = i;
            su.Setup(index % InventoryManager.Instance.inventoryGrid.width, index / InventoryManager.Instance.inventoryGrid.width, this);
            slotUIs.Add(su);
        }
    }

    public void RefreshUI()
    {
        if (InventoryManager.Instance == null || InventoryManager.Instance.inventoryGrid == null || gridParent == null || itemUIPrefab == null) return;
        if (!isBuilt || builtWidth != InventoryManager.Instance.inventoryGrid.width || builtHeight != InventoryManager.Instance.inventoryGrid.height) BuildGridVisuals();

        // destroy existing item UI visuals
        foreach (var kv in itemToUI)
            if (kv.Value != null) Destroy(kv.Value.gameObject);
        itemToUI.Clear();

        // For each distinct top-left item in grid, instantiate itemUI at that slot, sized accordingly
        InventoryGrid grid = InventoryManager.Instance.inventoryGrid;
        bool[,] drawn = new bool[grid.width, grid.height];

        for (int y = 0; y < grid.height; y++)
        {
            for (int x = 0; x < grid.width; x++)
            {
                var item = grid.grid[x, y];
                if (item == null) continue;

                // only create UI for top-left
                if (!grid.FindTopLeftOfItem(item, out int tlx, out int tly)) continue;
                if (drawn[tlx, tly]) continue;

                int index = tly * grid.width + tlx;
                Transform slot = gridParent.GetChild(index);

                GameObject itemVis = Instantiate(itemUIPrefab, slot);
                RectTransform rect = itemVis.GetComponent<RectTransform>();
                rect.pivot = new Vector2(0f, 1f); // top-left pivot to align with slots
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(item.Width * cellSize + (item.Width - 1) * spacing,
                                             item.Height * cellSize + (item.Height - 1) * spacing);

                Image img = itemVis.GetComponent<Image>();
                if (img != null) { img.sprite = item.Icon; img.raycastTarget = false; }

                itemToUI[item] = rect;
                drawn[tlx, tly] = true;
            }
        }
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(gridParent);
    }

    // Called by SlotUI when player right-clicks a slot
    public void OnSlotRightClick(int x, int y)
    {
        if (InventoryManager.Instance == null || InventoryManager.Instance.inventoryGrid == null) return;
        var grid = InventoryManager.Instance.inventoryGrid;
        if (x < 0 || y < 0 || x >= grid.width || y >= grid.height) return;

        var item = grid.grid[x, y];
        if (item == null) return;

        // find top-left coord for that item and ensure we only show action once
        if (!grid.FindTopLeftOfItem(item, out int tlx, out int tly)) return;

        // Show a simple context choice: Use (immediate) or Drop
        // For simplicity here, we'll call Use. You can expand to show a modal menu.
        var mgr = InventoryManager.Instance;
        if (mgr != null)
        {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) mgr.RemoveItemAt(tlx, tly);
            else mgr.UseItemAt(tlx, tly);
        }

    }

    // Public helper if grid size changed in inspector at runtime
    public void RebuildAndRefresh()
    {
        BuildGridVisuals();
        RefreshUI();
    }
}
