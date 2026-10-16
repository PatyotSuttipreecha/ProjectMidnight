using UnityEditor;
using UnityEngine;

public class CheatMenuWindow : EditorWindow
{
    private void OnInspectorUpdate() { if (Application.isPlaying) Repaint(); }
    [MenuItem("System Modification/Cheat Menu")]
    public static void Open() => GetWindow<CheatMenuWindow>("Cheat Menu");
    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("DEBUG CONTROL CENTER", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Gameplay testing tools", EditorStyles.miniLabel);
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to use cheats. F10 opens the in-game menu.", MessageType.Info);
            return;
        }
        var player = PlayerController.instance;
        if (player == null) { EditorGUILayout.HelpBox("No active player found.", MessageType.Info); return; }
        EditorGUILayout.Space(8);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Survival", EditorStyles.boldLabel);
        bool infinite = EditorGUILayout.Toggle(new GUIContent("Infinite Health", "Blocks damage and restores full health when enabled. Temporary Play Mode setting."), player.CheatInfiniteHealth);
        if (infinite != player.CheatInfiniteHealth) player.SetCheatInfiniteHealth(infinite);
        EditorGUILayout.LabelField("Health", player.currentHealth.ToString("0.#") + " / " + player.maxHealth.ToString("0.#"));
        EditorGUILayout.LabelField("Last damage attempt", player.LastDamageResult);
        var effects = player.GetComponent<EnemyPlayerEffects>();
        if (effects != null)
        {
            EditorGUILayout.LabelField("Last enemy hit", effects.LastHitResult);
            EditorGUILayout.LabelField("Hit protection", effects.BlockReason);
        }
        EditorGUILayout.LabelField("Last health change", player.LastHealthChange, EditorStyles.wordWrappedLabel);
        player.logHealthChanges = EditorGUILayout.Toggle("Log Health Changes", player.logHealthChanges);
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(8);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Movement", EditorStyles.boldLabel);
        player.cheatSprintMultiplier = EditorGUILayout.Slider("Sprint Multiplier", player.cheatSprintMultiplier, 1f, 10f);
        EditorGUILayout.LabelField("Sprint Speed (m/s)", ((player.moveSpeed + player.sprintBonus) * player.cheatSprintMultiplier).ToString("0.##"));
        if (GUILayout.Button("Reset to 1x")) player.cheatSprintMultiplier = 1f;
        bool flying = EditorGUILayout.Toggle("Fly Mode (F9)", player.CheatFlying);
        if (flying != player.CheatFlying) player.SetCheatFlying(flying);
        player.cheatFlySpeed = EditorGUILayout.Slider("Fly Speed (m/s)", player.cheatFlySpeed, 1f, 50f);
        EditorGUILayout.HelpBox("WASD: camera direction | Space: up | Ctrl: down | Shift: 2x speed. F9 toggles flight. Collisions remain enabled.", MessageType.Info);
        if (GUILayout.Button("Disable Flying")) player.SetCheatFlying(false);
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(8);
        if (GUILayout.Button("Reset All Cheats", GUILayout.Height(30)))
        {
            player.SetCheatInfiniteHealth(false);
            player.SetCheatFlying(false);
            player.cheatSprintMultiplier = 1f;
            player.cheatFlySpeed = 8f;
            EnemyStateDisplay.Reset();
        }
        EditorGUILayout.Space(8);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Enemy Debug", EditorStyles.boldLabel);
        EnemyStateDisplay.Enabled = EditorGUILayout.Toggle("Show Enemy States (F8)", EnemyStateDisplay.Enabled);
        EnemyStateDisplay.ShowDetails = EditorGUILayout.Toggle("Focused Enemy Details", EnemyStateDisplay.ShowDetails);
        EnemyStateDisplay.MaxLabels = EditorGUILayout.IntSlider("Compact Label Limit", EnemyStateDisplay.MaxLabels, 1, 20);
        EnemyStateDisplay.HeadOffset = EditorGUILayout.Slider("Offset Above Head (m)", EnemyStateDisplay.HeadOffset, 0, .5f);
        EditorGUILayout.HelpBox("Aim near an enemy's head to focus its details. IDs stay with each enemy. Closest enemies receive compact labels first; crowded labels collapse to head markers instead of floating far away.", MessageType.Info);
        EnemyStateDisplay.MaxDistance = EditorGUILayout.Slider("Label Range (m)", EnemyStateDisplay.MaxDistance, 5, 100);
        EnemyStateDisplay.FontSize = EditorGUILayout.IntSlider("Text Size", EnemyStateDisplay.FontSize, 10, 22);
        EditorGUILayout.EndVertical();
        EditorGUILayout.HelpBox("Temporary Play Mode values. Walk speed and saved movement settings are unchanged.", MessageType.Info);
    }
}
