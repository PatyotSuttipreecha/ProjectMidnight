using UnityEngine;
using System.Collections.Generic;

// Debug overlay: head markers identify owners; bounded compact labels never stack across the screen.
public static class EnemyStateDisplay
{
    public static bool Enabled;
    public static bool ShowDetails = true;
    public static float MaxDistance = 30f;
    public static int FontSize = 14;
    public static int MaxLabels = 8;
    public static float HeadOffset = .12f;
    private static GUIStyle title, detail;
    private sealed class Label
    {
        public EnemyController Enemy;
        public float Distance;
        public Rect Rect;
        public Vector2 Anchor;
        public int Id;
        public bool Expanded;
    }
    private static readonly List<Label> labels = new List<Label>();
    private static readonly Dictionary<EnemyController, int> ids = new Dictionary<EnemyController, int>();
    private static readonly List<EnemyController> removed = new List<EnemyController>();
    private static int nextId, lastDrawFrame = -1;
    public static bool Available => Application.isEditor || Debug.isDebugBuild;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset()
    {
        Enabled = false; ShowDetails = true; MaxDistance = 30; FontSize = 14; MaxLabels = 8; HeadOffset = .12f;
        title = detail = null;
        lastDrawFrame = -1; nextId = 0;
        labels.Clear(); ids.Clear(); removed.Clear();
    }
    public static void DrawAll(IEnumerable<EnemyController> enemies)
    {
        if (!Available || !Enabled || Event.current.type != EventType.Repaint || lastDrawFrame == Time.frameCount) return;
        var camera = Camera.main;
        if (camera == null) return;
        lastDrawFrame = Time.frameCount;
        if (title == null)
        {
            title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            detail = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            title.normal.textColor = detail.normal.textColor = Color.white;
        }
        title.fontSize = Mathf.Clamp(FontSize, 10, 22);
        detail.fontSize = Mathf.Max(11, title.fontSize - 1);
        removed.Clear();
        foreach (var entry in ids) if (entry.Key == null) removed.Add(entry.Key);
        foreach (var enemy in removed) ids.Remove(enemy);
        labels.Clear();
        Label focused = null;
        float focusDistance = Mathf.Min(Screen.width, Screen.height) * .25f;
        foreach (var enemy in enemies)
        {
            if (enemy == null || !enemy.isActiveAndEnabled) continue;
            float distance = Vector3.Distance(camera.transform.position, enemy.transform.position);
            if (distance > MaxDistance) continue;
            var screen = camera.WorldToScreenPoint(enemy.GameplayDebugHeadPosition + Vector3.up * Mathf.Clamp(HeadOffset, 0, .5f));
            if (screen.z <= 0 || screen.x < 0 || screen.x > Screen.width || screen.y < 0 || screen.y > Screen.height) continue;
            if (!ids.TryGetValue(enemy, out int id)) { id = ++nextId; ids[enemy] = id; }
            var label = new Label { Enemy = enemy, Distance = distance, Anchor = new Vector2(screen.x, Screen.height - screen.y), Id = id };
            labels.Add(label);
            float aimDistance = Vector2.Distance(label.Anchor, new Vector2(Screen.width * .5f, Screen.height * .5f));
            if (aimDistance < focusDistance) { focusDistance = aimDistance; focused = label; }
        }
        labels.Sort((a, b) => { int distance = a.Distance.CompareTo(b.Distance); return distance != 0 ? distance : a.Id.CompareTo(b.Id); });
        // Focus gets first choice, followed by closest enemies. Others retain head markers.
        int placed = 0;
        if (focused != null && MaxLabels > 0 && Place(focused)) placed++;
        foreach (var label in labels)
            if (label != focused && placed < Mathf.Max(1, MaxLabels) && Place(label)) placed++;
        int previousDepth = GUI.depth;
        Color previousColor = GUI.color;
        GUI.depth = 100;
        for (int i = labels.Count - 1; i >= 0; i--) DrawLabel(labels[i], labels[i] == focused);
        if (ShowDetails && focused != null) DrawDetails(focused);
        GUI.color = previousColor;
        GUI.depth = previousDepth;
    }
    private static bool Place(Label label)
    {
        string text = "#" + label.Id + " " + label.Enemy.GameplayDebugState;
        float width = Mathf.Min(Screen.width, Mathf.Max(90, title.CalcSize(new GUIContent(text)).x + 14));
        float height = Mathf.Max(23, title.fontSize + 8);
        // No unbounded stacking: a label can move at most 28 pixels from its head.
        foreach (float shift in new[] { 0f, -28f, 28f })
        {
            var candidate = new Rect(label.Anchor.x - width * .5f, label.Anchor.y - height - 9 + shift, width, height);
            if (candidate.xMin < 0 || candidate.xMax > Screen.width || candidate.yMin < 0 || candidate.yMax > Screen.height) continue;
            bool overlaps = false;
            foreach (var other in labels)
                if (other != label && other.Expanded && candidate.Overlaps(other.Rect)) { overlaps = true; break; }
            if (overlaps) continue;
            label.Rect = candidate; label.Expanded = true; return true;
        }
        return false;
    }
    private static void Line(Vector2 from, Vector2 to, Color color, float thickness)
    {
        var matrix = GUI.matrix;
        Vector2 delta = to - from;
        GUI.color = color;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
        GUI.DrawTexture(new Rect(from.x, from.y - thickness * .5f, delta.magnitude, thickness), Texture2D.whiteTexture);
        GUI.matrix = matrix;
    }
    private static void Box(Rect rect, Color color)
    {
        GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture);
    }
    private static void DrawLabel(Label label, bool focused)
    {
        Color color = label.Enemy.StateColor;
        if (label.Expanded)
        {
            Vector2 connection = new Vector2(Mathf.Clamp(label.Anchor.x, label.Rect.xMin, label.Rect.xMax),
                Mathf.Clamp(label.Anchor.y, label.Rect.yMin, label.Rect.yMax));
            Line(connection, label.Anchor, Color.black, 5);
            Line(connection, label.Anchor, color, 2);
            Box(label.Rect, focused ? Color.white : color);
            Box(new Rect(label.Rect.x + 1, label.Rect.y + 1, label.Rect.width - 2, label.Rect.height - 2), new Color(.02f, .025f, .035f, .97f));
            GUI.color = Color.white;
            GUI.Label(label.Rect, "#" + label.Id + " " + label.Enemy.GameplayDebugState, title);
        }
        // This marker always stays at the actual head, even if its pill cannot fit.
        Rect marker = new Rect(label.Anchor.x - 4, label.Anchor.y - 4, 8, 8);
        Box(new Rect(marker.x - 1, marker.y - 1, 10, 10), focused ? Color.white : Color.black);
        Box(marker, color);
        if (!label.Expanded)
        {
            Rect number = new Rect(label.Anchor.x + 6, label.Anchor.y - 10, 38, 20);
            Box(number, new Color(0, 0, 0, .9f));
            GUI.color = Color.white; GUI.Label(number, "#" + label.Id, title);
        }
    }
    private static void DrawDetails(Label label)
    {
        var enemy = label.Enemy;
        float width = Mathf.Min(440, Screen.width - 16);
        float line = Mathf.Max(20, detail.fontSize + 5);
        int rows = EnemyCombatSettings.RushEnabled ? 5 : 4;
        Rect rect = new Rect(8, Mathf.Max(8, Screen.height - line * rows - 20), width, line * rows + 12);
        Box(rect, new Color(.015f, .02f, .03f, .96f));
        Box(new Rect(rect.x, rect.y, 3, rect.height), enemy.StateColor);
        string weapon = enemy.CurrentWeapon != null ? enemy.CurrentWeapon.loadout.ToString() : "Unarmed";
        string state = enemy.GameplayDebugState;
        string turn = !enemy.IsPatrolRunning || enemy.IsDead ? "Inactive" : !enemy.HasAttackTurn ? "Waiting for turn" :
            !enemy.isAttacking && enemy.AttackStartDelay > 0 ? "Start in " + enemy.AttackStartDelay.ToString("0.0") + "s" : "Has attack turn";
        string[] lines = { "FOCUS #" + label.Id + " | " + state + " | " + label.Distance.ToString("0.0") + "m",
            enemy.name + " | " + weapon,
            !enemy.IsDead && !enemy.IsPatrolRunning ? "AI paused" : Describe(enemy.CurrentState),
            (enemy.IsAggressive ? "Aggressive" : "Normal") + " | " + turn,
            EnemyCombatSettings.RushEnabled ? enemy.RushStatus : "" };
        GUI.color = Color.white;
        for (int i = 0; i < rows; i++) GUI.Label(new Rect(rect.x + 10, rect.y + 6 + line * i, width - 18, line), lines[i], detail);
    }
    private static string Describe(EnemyController.EnemyState state)
    {
        switch (state)
        {
            case EnemyController.EnemyState.Idle: return "Standing by";
            case EnemyController.EnemyState.Patrol: return "Following patrol route";
            case EnemyController.EnemyState.Waiting: return "Waiting at patrol point";
            case EnemyController.EnemyState.Detect: return "Confirming sight / investigating sound";
            case EnemyController.EnemyState.Chase: return "Pursuing target";
            case EnemyController.EnemyState.ChaseBurst: return "Boosted pursuit";
            case EnemyController.EnemyState.Rush: return "Rushing in while the pressure group allows";
            case EnemyController.EnemyState.Approach: return "Closing in with attack permission";
            case EnemyController.EnemyState.Pressure: return "Circling while waiting for a turn";
            case EnemyController.EnemyState.EscapeWalk: return "Walking after player; running is paused";
            case EnemyController.EnemyState.Search: return "Investigating last known position";
            case EnemyController.EnemyState.Preparation: return "Preparing attack";
            case EnemyController.EnemyState.Attack: return "Attack in progress";
            case EnemyController.EnemyState.Recovery: return "Recovering / waiting for projectile";
            case EnemyController.EnemyState.Retreat: return "Withdrawing to patrol";
            case EnemyController.EnemyState.HitReaction: return "Staggered by impact";
            case EnemyController.EnemyState.Dead: return "Dead";
            default: return state.ToString();
        }
    }
}
