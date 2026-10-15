using UnityEngine;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(PlayerController))]
[AddComponentMenu("Debug/Cheat Menu")]
public class CheatMenu : MonoBehaviour
{
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
        EnemyStateDisplay.ShowDetails = GUILayout.Toggle(EnemyStateDisplay.ShowDetails, "Show behaviour and attack permission");
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
