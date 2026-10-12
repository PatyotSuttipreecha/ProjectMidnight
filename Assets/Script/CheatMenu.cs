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
    private Rect window = new Rect(20, 20, 350, 340);
    private bool Available => Application.isEditor || Debug.isDebugBuild;

    private void Awake() => player = GetComponent<PlayerController>();
    private void Update()
    {
        if (!Available || player == null) return;
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
        window = GUI.Window(GetInstanceID(), window, DrawWindow, "Cheat Menu (F10)");
    }
    private void DrawWindow(int id)
    {
        GUILayout.Label("Sprint speed: " + player.cheatSprintMultiplier.ToString("0.0") + "x");
        player.cheatSprintMultiplier = GUILayout.HorizontalSlider(player.cheatSprintMultiplier, 1f, 10f);
        GUILayout.Label("Shift + movement | Walk speed stays the same");
        GUILayout.Label("Sprint: " + ((player.moveSpeed + player.sprintBonus) * player.cheatSprintMultiplier).ToString("0.##") + " m/s");
        GUILayout.Space(10);
        bool flying = GUILayout.Toggle(player.CheatFlying, "Fly Mode (F9)");
        if (flying != player.CheatFlying) player.SetCheatFlying(flying);
        GUILayout.Label("Fly speed: " + player.cheatFlySpeed.ToString("0.0") + " m/s");
        player.cheatFlySpeed = GUILayout.HorizontalSlider(player.cheatFlySpeed, 1f, 50f);
        GUILayout.Label("WASD: camera direction | Space: up | Ctrl: down");
        GUILayout.Label("Shift: 2x fly speed | Collisions remain enabled");
        if (GUILayout.Button("Reset to 1x")) player.cheatSprintMultiplier = 1f;
        if (GUILayout.Button("Disable Flying")) player.SetCheatFlying(false);
        if (GUILayout.Button("Close")) Close();
        GUI.DragWindow(new Rect(0, 0, 320, 24));
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
        if (player != null) { player.cheatSprintMultiplier = 1f; player.SetCheatFlying(false); player.cheatFlySpeed = 8f; }
    }
}
