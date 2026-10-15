using UnityEngine;
using System.Collections.Generic;

// Shared session-only settings. One sorted overlay draws all active enemies.
public static class EnemyStateDisplay
{
    public static bool Enabled;
    public static bool ShowDetails = true;
    public static float MaxDistance = 30f;
    public static int FontSize = 14;
    private static GUIStyle title, detail;
    private sealed class Label
    {
        public EnemyController Enemy;
        public float Distance;
        public Rect Rect;
        public Vector2 Anchor;
    }
    private static readonly List<Label> labels = new List<Label>();
    private static int lastDrawFrame = -1;
    public static bool Available => Application.isEditor || Debug.isDebugBuild;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset()
    {
        Enabled = false; ShowDetails = true; MaxDistance = 30; FontSize = 14;
        title = detail = null;
        lastDrawFrame = -1;
        labels.Clear();
    }
    public static void DrawAll(IEnumerable<EnemyController> enemies)
    {
        if (!Available || !Enabled || Event.current.type != EventType.Repaint) return;
        if (lastDrawFrame == Time.frameCount) return;
        var camera = Camera.main;
        if (camera == null) return;
        lastDrawFrame = Time.frameCount;
        labels.Clear();
        float width = Mathf.Min(260, Screen.width), height = ShowDetails ? 66 : 30;
        foreach (var enemy in enemies)
        {
            if (enemy == null || !enemy.isActiveAndEnabled) continue;
            float distance = Vector3.Distance(camera.transform.position, enemy.transform.position);
            if (distance > MaxDistance) continue;
            var screen = camera.WorldToScreenPoint(enemy.transform.position + Vector3.up * 2.5f);
            if (screen.z <= 0 || screen.x < 0 || screen.x > Screen.width || screen.y < 0 || screen.y > Screen.height) continue;
            var anchor = new Vector2(screen.x, Screen.height - screen.y);
            labels.Add(new Label { Enemy = enemy, Distance = distance, Anchor = anchor,
                Rect = new Rect(Mathf.Clamp(anchor.x - width / 2, 0, Mathf.Max(0, Screen.width - width)),
                    Mathf.Clamp(anchor.y - height, 0, Mathf.Max(0, Screen.height - height)), width, height) });
        }
        labels.Sort((a, b) => { int distance = a.Distance.CompareTo(b.Distance); return distance != 0 ? distance : a.Enemy.GetInstanceID().CompareTo(b.Enemy.GetInstanceID()); });
        // Give the closest label its preferred position first, then shift farther ones.
        for (int i = 1; i < labels.Count; i++)
        {
            var original = labels[i].Rect;
            for (int attempt = 0; attempt < Mathf.CeilToInt(Screen.height / (height + 4)) * 2; attempt++)
            {
                var candidate = original;
                int step = (attempt + 1) / 2;
                candidate.y = Mathf.Clamp(original.y + (attempt % 2 == 1 ? -1 : 1) * step * (height + 4), 0, Mathf.Max(0, Screen.height - height));
                bool overlaps = false;
                for (int j = 0; j < i; j++)
                    if (candidate.Overlaps(labels[j].Rect)) { overlaps = true; break; }
                if (!overlaps) { labels[i].Rect = candidate; break; }
            }
        }
        int previousDepth = GUI.depth;
        GUI.depth = 100;
        // Draw farthest first so nearby labels stay on top when the screen is full.
        for (int i = labels.Count - 1; i >= 0; i--) DrawLabel(labels[i], i + 1);
        GUI.depth = previousDepth;
    }
    private static void DrawLabel(Label label, int rank)
    {
        var enemy = label.Enemy;
        string status = enemy.GameplayDebugState;
        if (title == null)
        {
            title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            detail = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            detail.normal.textColor = Color.white;
        }
        title.fontSize = Mathf.Clamp(FontSize, 10, 22);
        detail.fontSize = Mathf.Max(10, title.fontSize - 2);
        title.normal.textColor = enemy.StateColor;
        var rect = label.Rect;
        float width = rect.width;
        var savedColor = GUI.color;
        var connection = new Vector2(Mathf.Clamp(label.Anchor.x, rect.xMin, rect.xMax), rect.yMax);
        if (Vector2.Distance(connection, label.Anchor) > 8)
        {
            var savedMatrix = GUI.matrix;
            GUI.color = new Color(enemy.StateColor.r, enemy.StateColor.g, enemy.StateColor.b, .65f);
            Vector2 delta = label.Anchor - connection;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, connection);
            GUI.DrawTexture(new Rect(connection.x, connection.y, delta.magnitude, 1), Texture2D.whiteTexture);
            GUI.matrix = savedMatrix;
        }
        GUI.color = new Color(.025f, .035f, .05f, .9f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = enemy.StateColor;
        GUI.DrawTexture(new Rect(rect.x, rect.y, width, 3), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x + 4, rect.y + 4, width - 8, 24), "#" + rank + " " + status + "  " + label.Distance.ToString("0.0") + "m", title);
        if (ShowDetails)
        {
            string behaviour = !enemy.IsDead && !enemy.IsPatrolRunning ? "AI paused" : status != enemy.CurrentState.ToString() ? "Check enemy setup" : Describe(enemy.CurrentState);
            GUI.Label(new Rect(rect.x + 4, rect.y + 28, width - 8, 18), behaviour, detail);
            string mode = enemy.IsAggressive ? "Aggressive" : "Normal";
            string turn = enemy.IsDead || !enemy.IsPatrolRunning ? "Inactive" : enemy.HasAttackTurn ? "Attack turn: ready" : "Attack turn: waiting";
            GUI.Label(new Rect(rect.x + 4, rect.y + 46, width - 8, 18), mode + " | " + turn, detail);
        }
        GUI.color = savedColor;
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
            case EnemyController.EnemyState.Approach: return "Closing in with attack permission";
            case EnemyController.EnemyState.Pressure: return "Circling while waiting for a turn";
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
