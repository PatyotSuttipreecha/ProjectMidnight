using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    public RectTransform gridParent;
    public GameObject slotPrefab;
    public GameObject itemUIPrefab;
    [Min(1)] public float cellSize = 150f;
    [Min(0)] public float spacing = 4f;
    public Color validColor = new Color(.2f, .9f, .4f, .7f);
    public Color invalidColor = new Color(1f, .25f, .2f, .7f);
    private readonly List<SlotUI> slots = new List<SlotUI>();
    private readonly List<Color> colors = new List<Color>();
    private readonly Dictionary<InventoryItemData, RectTransform> visuals = new Dictionary<InventoryItemData, RectTransform>();
    private InventoryManager manager;
    private int builtWidth, builtHeight;
    private InventoryItemData dragged;
    private RectTransform ghost;
    private RectTransform itemsRoot;
    private bool rotated;
    private Vector2Int grabOffset, candidate;
    private Vector2 pointer;
    private Camera eventCamera;
    [Header("Editable UI Prefabs")]
    public InventoryPopupView contextMenuPrefab;
    public InventoryPopupView dropConfirmationPrefab;
    public InventoryPopupView inspectionPrefab;
    public TMP_FontAsset menuFont;
    [Header("Inspection Controls")]
    [Min(0f)] public float inspectionRotateSpeed = .35f;
    [Min(0f)] public float inspectionZoomSpeed = .3f;
    private GameObject popup;
    private ItemInspectionPreview inspectionPreview;

    private void OnEnable() => BindManager();
    private void Start() => BindManager();
    private void Update()
    {
        if (popup != null && Input.GetKeyDown(KeyCode.Escape)) ClosePopup();
        if (manager != InventoryManager.Instance || slots.Count == 0) BindManager();
        if (dragged != null && Input.GetKeyDown(KeyCode.Escape)) CancelDrag();
        if (dragged != null && Input.GetKeyDown(KeyCode.R) && dragged.itemSO.allowRotation)
        { rotated = !rotated; grabOffset = Vector2Int.zero; UpdateDrag(pointer, eventCamera); }
    }
    private void OnDisable()
    {
        ClosePopup();
        CancelDrag();
        if (manager != null) manager.OnInventoryChanged -= RefreshUI;
        manager = null;
    }
    private void BindManager()
    {
        if (InventoryManager.Instance == null || InventoryManager.Instance.inventoryGrid == null
            || gridParent == null || slotPrefab == null || itemUIPrefab == null) return;
        if (manager != InventoryManager.Instance)
        {
            if (manager != null) manager.OnInventoryChanged -= RefreshUI;
            manager = InventoryManager.Instance;
            manager.OnInventoryChanged += RefreshUI;
        }
        RefreshUI();
    }
    public void BuildGridVisuals()
    {
        if (manager == null || gridParent == null || slotPrefab == null) return;
        CancelDrag();
        for (int i = gridParent.childCount - 1; i >= 0; i--)
        {
            Transform child = gridParent.GetChild(i);
            child.gameObject.SetActive(false); child.SetParent(null, false); Destroy(child.gameObject);
        }
        slots.Clear(); colors.Clear(); visuals.Clear();
        var layout = gridParent.GetComponent<GridLayoutGroup>();
        if (layout == null) layout = gridParent.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(cellSize, cellSize); layout.spacing = new Vector2(spacing, spacing);
        layout.startCorner = GridLayoutGroup.Corner.UpperLeft; layout.startAxis = GridLayoutGroup.Axis.Horizontal;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        builtWidth = manager.inventoryGrid.width; builtHeight = manager.inventoryGrid.height;
        layout.constraintCount = builtWidth;
        gridParent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
            builtWidth * cellSize + (builtWidth - 1) * spacing + layout.padding.horizontal);
        gridParent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            builtHeight * cellSize + (builtHeight - 1) * spacing + layout.padding.vertical);
        for (int y = 0; y < builtHeight; y++)
            for (int x = 0; x < builtWidth; x++)
            {
                var obj = Instantiate(slotPrefab, gridParent); obj.name = $"Slot [{x},{y}]";
                var slot = obj.GetComponent<SlotUI>();
                if (slot == null) slot = obj.AddComponent<SlotUI>();
                slot.Setup(x, y, this); slots.Add(slot);
                var image = obj.GetComponent<Image>();
                if (image != null) image.raycastTarget = true;
                colors.Add(image != null ? image.color : Color.white);
            }
        var overlay = new GameObject("Inventory Items", typeof(RectTransform), typeof(LayoutElement));
        itemsRoot = (RectTransform)overlay.transform; itemsRoot.SetParent(gridParent, false);
        overlay.GetComponent<LayoutElement>().ignoreLayout = true;
        itemsRoot.anchorMin = Vector2.zero; itemsRoot.anchorMax = Vector2.one;
        itemsRoot.offsetMin = itemsRoot.offsetMax = Vector2.zero;
        Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(gridParent);
    }
    private Vector2 Size(int w, int h) => new Vector2(w * cellSize + (w - 1) * spacing, h * cellSize + (h - 1) * spacing);
    public void RefreshUI()
    {
        ClosePopup();
        if (manager == null || gridParent == null || itemUIPrefab == null) return;
        CancelDrag();
        if (slots.Count == 0 || builtWidth != manager.inventoryGrid.width || builtHeight != manager.inventoryGrid.height) BuildGridVisuals();
        foreach (var rect in visuals.Values)
            if (rect != null) { rect.gameObject.SetActive(false); Destroy(rect.gameObject); }
        visuals.Clear();
        foreach (var pair in manager.placedPositions)
        {
            var item = pair.Key;
            var obj = Instantiate(itemUIPrefab, itemsRoot);
            var rect = obj.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0, 1); rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            var layout = gridParent.GetComponent<GridLayoutGroup>();
            rect.anchoredPosition = new Vector2(layout.padding.left + pair.Value.x * (cellSize + spacing),
                -layout.padding.top - pair.Value.y * (cellSize + spacing));
            rect.sizeDelta = Size(item.Width, item.Height);
            foreach (var graphic in obj.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            var background = obj.GetComponent<Image>();
            if (background != null) { background.sprite = null; background.color = new Color(.15f, .19f, .25f, .9f); }
            var iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            var iconRect = (RectTransform)iconObj.transform; iconRect.SetParent(rect, false);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(.5f, .5f);
            iconRect.sizeDelta = Size(item.itemSO.width, item.itemSO.height) - new Vector2(12, 12);
            iconRect.localRotation = Quaternion.Euler(0, 0, item.isRotated ? -90 : 0);
            var icon = iconObj.GetComponent<Image>();
            icon.sprite = item.Icon; icon.preserveAspect = true; icon.raycastTarget = false; icon.enabled = item.Icon != null;
            var labelObj = new GameObject("Item Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var labelRect = (RectTransform)labelObj.transform; labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(6, 6); labelRect.offsetMax = new Vector2(-6, -6);
            var label = labelObj.GetComponent<TextMeshProUGUI>();
            label.fontSize = 18; label.alignment = TextAlignmentOptions.BottomRight; label.raycastTarget = false;
            label.text = item.Icon == null ? item.ItemName + (item.quantity > 1 ? $" ×{item.quantity}" : "")
                : item.quantity > 1 ? item.quantity.ToString() : "";
            if (PlayerController.instance != null && PlayerController.instance.EquippedInventoryItem == item)
                label.text += "\nEquipped";
            visuals[item] = rect;
        }
    }
    public void BeginDrag(int x, int y, PointerEventData data)
    {
        ClosePopup();
        if (manager == null || data.button != PointerEventData.InputButton.Left) return;
        var item = manager.inventoryGrid.grid[x, y]; if (item == null) return;
        CancelDrag(); dragged = item; rotated = item.isRotated;
        grabOffset = new Vector2Int(x, y) - manager.placedPositions[item];
        var obj = new GameObject("Placement Preview", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        ghost = (RectTransform)obj.transform; ghost.SetParent(gridParent, false);
        obj.GetComponent<LayoutElement>().ignoreLayout = true;
        ghost.pivot = new Vector2(0, 1); ghost.anchorMin = ghost.anchorMax = new Vector2(0, 1);
        obj.GetComponent<Image>().raycastTarget = false;
        visuals[item].gameObject.SetActive(false); UpdateDrag(data.position, data.pressEventCamera);
    }
    public void Drag(PointerEventData data) { if (dragged != null) UpdateDrag(data.position, data.pressEventCamera); }
    private InventoryItemData Target()
    {
        if (candidate.x < 0 || candidate.y < 0 || candidate.x >= builtWidth || candidate.y >= builtHeight) return null;
        return manager.inventoryGrid.grid[candidate.x, candidate.y];
    }
    private void UpdateDrag(Vector2 position, Camera camera)
    {
        pointer = position; eventCamera = camera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gridParent, position, camera, out Vector2 local)) return;
        var layout = gridParent.GetComponent<GridLayoutGroup>();
        float left = gridParent.rect.xMin + layout.padding.left, top = gridParent.rect.yMax - layout.padding.top;
        candidate = new Vector2Int(Mathf.FloorToInt((local.x - left) / (cellSize + spacing)),
            Mathf.FloorToInt((top - local.y) / (cellSize + spacing))) - grabOffset;
        int w = rotated ? dragged.itemSO.height : dragged.itemSO.width;
        int h = rotated ? dragged.itemSO.width : dragged.itemSO.height;
        ghost.sizeDelta = Size(w, h);
        ghost.anchoredPosition = new Vector2(layout.padding.left + candidate.x * (cellSize + spacing),
            -layout.padding.top - candidate.y * (cellSize + spacing));
        var target = Target();
        bool merge = target != null && target != dragged && target.itemSO == dragged.itemSO && target.quantity < target.itemSO.StackLimit;
        bool valid = manager.CanMoveItem(dragged, candidate.x, candidate.y, rotated) || merge;
        Color color = valid ? validColor : invalidColor; ghost.GetComponent<Image>().color = color;
        ResetColors();
        for (int y = Mathf.Max(0, candidate.y); y < Mathf.Min(builtHeight, candidate.y + h); y++)
            for (int x = Mathf.Max(0, candidate.x); x < Mathf.Min(builtWidth, candidate.x + w); x++)
                if (slots[y * builtWidth + x].TryGetComponent<Image>(out var image)) image.color = color;
    }
    public void EndDrag(PointerEventData data)
    {
        if (dragged == null) return;
        UpdateDrag(data.position, data.pressEventCamera);
        var item = dragged; var target = Target(); var destination = candidate; bool orientation = rotated;
        CancelDrag();
        if (!manager.TryMergeItems(item, target)) manager.TryMoveItem(item, destination.x, destination.y, orientation);
    }
    private void ResetColors()
    {
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] != null && slots[i].TryGetComponent<Image>(out var image)) image.color = colors[i];
    }
    private void CancelDrag()
    {
        if (dragged != null && visuals.TryGetValue(dragged, out var rect) && rect != null) rect.gameObject.SetActive(true);
        if (ghost != null) { ghost.gameObject.SetActive(false); Destroy(ghost.gameObject); }
        ghost = null; dragged = null; ResetColors();
    }
    public void OnSlotRightClick(int x, int y, Vector2 position, Camera camera)
    {
        if (manager == null || dragged != null) return;
        var grid = manager.inventoryGrid;
        if (x < 0 || y < 0 || x >= grid.width || y >= grid.height) return;
        var item = grid.grid[x, y];
        if (item == null) { ClosePopup(); return; }
        ShowContext(item, position, camera);
    }

    public void CloseItemMenu() => ClosePopup();
    private void ClosePopup()
    {
        if (inspectionPreview != null) inspectionPreview.Dispose();
        inspectionPreview = null;
        if (popup != null) { popup.SetActive(false); Destroy(popup); }
        popup = null;
    }
    private InventoryPopupView OpenPopup(InventoryPopupView prefab)
    {
        ClosePopup();
        if (prefab == null) { Debug.LogError("Assign the inventory UI prefabs.", this); return null; }
        Canvas canvas = gridParent.GetComponentInParent<Canvas>();
        if (canvas == null) return null;
        var view = Instantiate(prefab, canvas.rootCanvas.transform, false);
        popup = view.gameObject;
        popup.SetActive(true);
        popup.transform.SetAsLastSibling();
        Wire(view.close, ClosePopup);
        Wire(view.backdrop, ClosePopup);
        return view;
    }
    private static void Wire(Button button, System.Action action, bool enabled = true)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.interactable = enabled;
        button.onClick.AddListener(() => action());
    }
    private bool Locate(InventoryItemData item, out Vector2Int slot)
    {
        slot = default;
        return manager != null && manager.placedPositions.TryGetValue(item, out slot);
    }
    private void ShowContext(InventoryItemData item, Vector2 position, Camera camera)
    {
        var view = OpenPopup(contextMenuPrefab); if (view == null) return;
        Canvas.ForceUpdateCanvases();
        var overlay = (RectTransform)view.transform;
        var panel = view.panel;
        Vector2 size = panel.rect.size;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, position, camera, out Vector2 local);
        panel.anchoredPosition = new Vector2(Mathf.Clamp(local.x, overlay.rect.xMin, Mathf.Max(overlay.rect.xMin, overlay.rect.xMax - size.x)),
            Mathf.Clamp(local.y, Mathf.Min(overlay.rect.yMax, overlay.rect.yMin + size.y), overlay.rect.yMax));
        view.title.text = item.ItemName;
        var player = PlayerController.instance;
        bool weapon = item.itemSO.itemType == ItemType.Weapon;
        bool reloading = player != null && player.EquippedGun != null && player.EquippedGun.isReloading;
        bool equipped = player != null && player.EquippedInventoryItem == item;
        bool canUse = false;
        string useLabel = weapon ? "Equip" : "Use";
        if (weapon)
        {
            canUse = player != null && player.SupportsWeapon(item.itemSO) && !equipped && !reloading;
            if (equipped) useLabel = "Equipped";
            else if (reloading) useLabel = "Equip (reloading)";
            else if (!canUse) useLabel = "Equip (unavailable)";
        }
        else if (item.itemSO.itemType == ItemType.Health)
        {
            canUse = player != null && player.currentHealth > 0 && player.currentHealth < player.maxHealth && item.itemSO.healAmount > 0;
            if (!canUse) useLabel = "Use (cannot heal)";
        }
        else if (item.itemSO.itemType == ItemType.Ammo)
        {
            canUse = player != null && player.OwnsWeapon(item.itemSO.weaponType) && item.itemSO.ammoAmount > 0;
            useLabel = canUse ? "Use / Add ammo" : "Use (no compatible weapon)";
        }
        else useLabel = "Use (unavailable)";
        view.useLabel.text = useLabel;
        Wire(view.use, () =>
        { ClosePopup(); if (Locate(item, out var slot)) manager.UseItemAt(slot.x, slot.y); }, canUse);
        Wire(view.examine, () => ShowInspection(item));
        bool canDrop = player != null && item.itemSO.pickupPrefab != null && !(equipped && reloading);
        view.dropLabel.text = canDrop ? "Drop" : "Drop (unavailable)";
        Wire(view.drop, () => ShowDropConfirmation(item), canDrop);

    }
    private void ShowDropConfirmation(InventoryItemData item)
    {
        var view = OpenPopup(dropConfirmationPrefab); if (view == null) return;
        view.title.text = $"Drop {item.ItemName} ×{item.quantity}?";
        Wire(view.drop, () =>
        { ClosePopup(); if (Locate(item, out var slot)) manager.RemoveItemAt(slot.x, slot.y); });
    }
    private void ShowInspection(InventoryItemData item)
    {
        if (!Locate(item, out _)) { ClosePopup(); return; }
        var view = OpenPopup(inspectionPrefab); if (view == null) return;
        if (PlayerController.instance != null && PlayerController.instance.EquippedInventoryItem == item)
            PlayerController.instance.EquippedGun?.StoreInventoryAmmo();
        view.title.text = item.ItemName;
        Canvas.ForceUpdateCanvases();
        inspectionPreview = view.preview;
        inspectionPreview.rotateSpeed = Mathf.Max(0, inspectionRotateSpeed);
        inspectionPreview.zoomSpeed = Mathf.Max(0, inspectionZoomSpeed);
        bool hasModel = inspectionPreview.Initialize(item.itemSO);
        view.modelView.enabled = hasModel;
        view.fallbackIcon.sprite = item.Icon;
        view.fallbackIcon.gameObject.SetActive(!hasModel && item.Icon != null);
        view.fallbackHint.gameObject.SetActive(!hasModel);
        Wire(view.reset, () => inspectionPreview?.ResetView());
        string details = $"Type: {item.itemSO.itemType}\nSize: {item.Width} × {item.Height}\nQuantity: {item.quantity} / {item.itemSO.StackLimit}";
        if (item.itemSO.itemType == ItemType.Weapon) details += $"\nMagazine: {item.magazineAmmo}\nReserve: {item.reserveAmmo}";
        if (item.itemSO.itemType == ItemType.Health) details += $"\nHealing per unit: {item.itemSO.healAmount}";
        if (item.itemSO.itemType == ItemType.Ammo) details += $"\nAmmo per unit: {item.itemSO.ammoAmount}\nCompatible: {item.itemSO.weaponType}";
        view.details.text = details.Replace("\n", "   |   ");
        view.description.text = string.IsNullOrWhiteSpace(item.itemSO.description) ? "No description available." : item.itemSO.description;
    }
    public void RebuildAndRefresh() { BuildGridVisuals(); RefreshUI(); }
}
