using UnityEngine;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(PlayerController))]
[AddComponentMenu("Debug/Cheat Menu")]
public class CheatMenu : MonoBehaviour
{
    public bool InfiniteAmmo { get; private set; }
    public void SetInfiniteAmmo(bool value) => InfiniteAmmo = Available && value;
    public ItemSO spawnItem;
    public int spawnQuantity = 1;
    public bool spawnInWorld;
    private ItemSO[] items = new ItemSO[0];
    private string itemSearch = "", spawnResult = "";
    public string SpawnResult => spawnResult;
    private Vector2 itemScroll;
    public static bool HasInfiniteAmmo(PlayerController target) => target != null &&
        target.TryGetComponent(out CheatMenu menu) && menu.Available && menu.InfiniteAmmo;
    public void RefreshItems()
    {
#if UNITY_EDITOR
        var records = new System.Collections.Generic.List<ItemSO>();
        foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:ItemSO", new[] { "Assets" }))
        {
            var item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemSO>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (item != null) records.Add(item);
        }
        items = records.ToArray();
#else
        items = Resources.FindObjectsOfTypeAll<ItemSO>();
#endif
        System.Array.Sort(items, (a,b) => string.Compare(a.itemName, b.itemName, System.StringComparison.OrdinalIgnoreCase));
    }
    public bool SpawnSelected()
    {
        if (!Available || !Application.isPlaying || player == null || spawnItem == null)
        { spawnResult = "Select an item in Play Mode."; return false; }
        int quantity = Mathf.Clamp(spawnQuantity, 1, 999);
        if (!spawnInWorld)
        {
            bool added = InventoryManager.Instance != null && InventoryManager.Instance.TryAddItemAutoPlace(spawnItem, quantity);
            spawnResult = added ? "Added " + quantity + " x " + spawnItem.itemName : "Inventory full, missing, or item does not fit. Nothing added.";
            return added;
        }
        if (spawnItem.pickupPrefab == null) { spawnResult = "Item has no Pickup Prefab. Use Add to inventory."; return false; }
        // Initialise the actual pickup data; never change the shared ItemSO.
        var pickup = Instantiate(spawnItem.pickupPrefab, player.transform.position + player.transform.forward * 1.5f + Vector3.up * .3f, Quaternion.identity);
        pickup.itemData = spawnItem;
        pickup.quantity = quantity;
        spawnResult = "Spawned " + quantity + " x " + spawnItem.itemName;
        return true;
    }
    private PlayerController player;
    private bool open;
    private CursorLockMode cursorLock;
    private bool cursorVisible;
    private Rect window = new Rect(20, 20, 420, 660);
    private Vector2 scroll;
    private GUIStyle heading, muted, card;
    private bool Available => Application.isEditor || Debug.isDebugBuild;

    private void Awake() => player = GetComponent<PlayerController>();
    private void Update()
    {
        if (!Available || player == null) return;
        if (Input.GetKeyDown(KeyCode.F8)) EnemyStateDisplay.Enabled = !EnemyStateDisplay.Enabled;
        if (Input.GetKeyDown(KeyCode.F9) && !player.IsGameplayInputBlocked)
            player.SetCheatFlying(!player.CheatFlying);
        if (Input.GetKeyDown(KeyCode.F10))
        {
            if (open) Close();
            else if (!player.IsGameplayInputBlocked)
            {
                cursorLock = Cursor.lockState; cursorVisible = Cursor.visible;
                player.LockGameplayInput(this);
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
                RefreshItems();
                open = true;
            }
        }
        if (open && Input.GetKeyDown(KeyCode.Escape)) Close();
    }
    private void OnGUI()
    {
        if (!Available || !open || player == null) return;
        if (heading == null)
        {
            heading = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            heading.normal.textColor = new Color(.4f, .85f, 1f);
            muted = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 12 };
            muted.normal.textColor = new Color(.7f, .75f, .8f);
            card = new GUIStyle(GUI.skin.box) { padding = new RectOffset(14, 14, 10, 10) };
        }
        window.width = Mathf.Min(420, Screen.width);
        window.height = Mathf.Min(660, Screen.height);
        window.x = Mathf.Clamp(window.x, 0, Mathf.Max(0, Screen.width - window.width));
        window.y = Mathf.Clamp(window.y, 0, Mathf.Max(0, Screen.height - window.height));
        window = GUI.Window(GetInstanceID(), window, DrawWindow, "Cheat Menu (F10)");
    }
    private void DrawWindow(int id)
    {
        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label("DEBUG CONTROL CENTER", heading);
        GUILayout.Label("Temporary controls for gameplay testing", muted);
        GUILayout.Space(8);
        GUILayout.BeginVertical(card);
        GUILayout.Label("SURVIVAL", heading);
        bool infinite = GUILayout.Toggle(player.CheatInfiniteHealth, "Infinite Health");
        if (infinite != player.CheatInfiniteHealth) player.SetCheatInfiniteHealth(infinite);
        GUILayout.Label("Health: " + player.currentHealth.ToString("0.#") + " / " + player.maxHealth.ToString("0.#"), muted);
        GUILayout.Label("Blocks damage while enabled. Enabling restores full health.", muted);
        GUILayout.EndVertical();
        GUILayout.Space(8);
        GUILayout.BeginVertical(card);
        GUILayout.Label("WEAPONS & ITEMS", heading);
        SetInfiniteAmmo(GUILayout.Toggle(InfiniteAmmo, "Infinite Ammo (no reload needed)"));
        itemSearch = GUILayout.TextField(itemSearch);
        if (GUILayout.Button("Refresh item list")) RefreshItems();
        itemScroll = GUILayout.BeginScrollView(itemScroll, GUILayout.Height(140));
        foreach (var item in items)
        {
            if (item == null || (!string.IsNullOrEmpty(itemSearch) && (item.itemName + " " + item.name + " " + item.itemType).IndexOf(itemSearch, System.StringComparison.OrdinalIgnoreCase) < 0)) continue;
            if (GUILayout.Toggle(spawnItem == item, item.itemName + " [" + item.itemType + "]", GUI.skin.button)) spawnItem = item;
        }
        GUILayout.EndScrollView();
        GUILayout.Label("Quantity: " + spawnQuantity);
        spawnQuantity = Mathf.RoundToInt(GUILayout.HorizontalSlider(spawnQuantity, 1, 99));
        spawnInWorld = GUILayout.Toggle(spawnInWorld, "Spawn world pickup (off = add to inventory)");
        if (GUILayout.Button("Spawn selected item")) SpawnSelected();
        GUILayout.Label(spawnResult, muted);
        GUILayout.EndVertical();
        GUILayout.Space(8);
        GUILayout.BeginVertical(card);
        GUILayout.Label("MOVEMENT", heading);
        GUILayout.Label("Sprint speed: " + player.cheatSprintMultiplier.ToString("0.0") + "x");
        player.cheatSprintMultiplier = GUILayout.HorizontalSlider(player.cheatSprintMultiplier, 1f, 10f);
        GUILayout.Label("Shift + movement | Walk speed stays the same", muted);
        GUILayout.Label("Sprint: " + ((player.moveSpeed + player.sprintBonus) * player.cheatSprintMultiplier).ToString("0.##") + " m/s");
        GUILayout.Space(10);
        bool flying = GUILayout.Toggle(player.CheatFlying, "Fly Mode (F9)");
        if (flying != player.CheatFlying) player.SetCheatFlying(flying);
        GUILayout.Label("Fly speed: " + player.cheatFlySpeed.ToString("0.0") + " m/s");
        player.cheatFlySpeed = GUILayout.HorizontalSlider(player.cheatFlySpeed, 1f, 50f);
        GUILayout.Label("WASD: camera direction | Space: up | Ctrl: down", muted);
        GUILayout.Label("Shift: 2x fly speed | Collisions remain enabled", muted);
        GUILayout.EndVertical();
        GUILayout.Space(10);
        GUILayout.BeginVertical(card);
        GUILayout.Label("ENEMY DEBUG", heading);
        EnemyStateDisplay.Enabled = GUILayout.Toggle(EnemyStateDisplay.Enabled, "Show Enemy States (F8)");
        EnemyStateDisplay.ShowPermissions = GUILayout.Toggle(EnemyStateDisplay.ShowPermissions, "Show granted rights / legend (P+ / A+ / FRONT)");
        EnemyStateDisplay.ShowDetails = GUILayout.Toggle(EnemyStateDisplay.ShowDetails, "Show focused enemy details (aim near its head)");
        GUILayout.Label("Compact labels: " + EnemyStateDisplay.MaxLabels + " | Others keep head markers", muted);
        EnemyStateDisplay.MaxLabels = Mathf.RoundToInt(GUILayout.HorizontalSlider(EnemyStateDisplay.MaxLabels, 1, 20));
        GUILayout.Label("Head offset: " + EnemyStateDisplay.HeadOffset.ToString("0.00") + " m", muted);
        EnemyStateDisplay.HeadOffset = GUILayout.HorizontalSlider(EnemyStateDisplay.HeadOffset, 0, .5f);
        GUILayout.Label("Label range: " + EnemyStateDisplay.MaxDistance.ToString("0") + " m", muted);
        EnemyStateDisplay.MaxDistance = GUILayout.HorizontalSlider(EnemyStateDisplay.MaxDistance, 5, 100);
        GUILayout.Label("Text size: " + EnemyStateDisplay.FontSize, muted);
        EnemyStateDisplay.FontSize = Mathf.RoundToInt(GUILayout.HorizontalSlider(EnemyStateDisplay.FontSize, 10, 22));
        GUILayout.EndVertical();
        if (GUILayout.Button("Reset All Cheats", GUILayout.Height(30))) ResetCheats();
        if (GUILayout.Button("Resume Game (Esc / F10)", GUILayout.Height(32))) Close();
        GUILayout.EndScrollView();
        GUI.DragWindow(new Rect(0, 0, window.width, 24));
    }
    private void ResetCheats()
    {
        if (player == null) return;
        SetInfiniteAmmo(false);
        player.cheatSprintMultiplier = 1f;
        player.SetCheatFlying(false);
        player.cheatFlySpeed = 8f;
        player.SetCheatInfiniteHealth(false);
        EnemyStateDisplay.Reset();
    }
    private void Close()
    {
        if (!open) return;
        open = false;
        if (player != null) player.UnlockGameplayInput(this);
        Cursor.lockState = cursorLock; Cursor.visible = cursorVisible;
    }
    private void OnDisable()
    {
        Close();
        ResetCheats();
    }
}
