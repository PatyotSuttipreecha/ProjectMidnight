using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Level Design/Point Note")]
public class PointNote : MonoBehaviour
{
    public enum NoteStatus { Info, Todo, Issue, Done }
    public enum PointType { General, Mission, Objective, Collectible, Loot, EnemySpawn, Patrol, Interaction, Puzzle, Trigger, Audio, SafeZone, Location }
    public enum ObjectiveType { Collect, Reach, Interact, ReadDocument, Defeat, Other }
    public PointType pointType;
    public string title = "Point Note";
    [Tooltip("Design work status: Info, Todo, Issue or Done. This does not complete a gameplay mission.")]
    public NoteStatus status;
    [TextArea(4, 20)] public string note;
    [Header("Mission / Objective Design")]
    public string missionId;
    [TextArea(3, 8)] public string missionDescription;
    public string objectiveId;
    public ObjectiveType objectiveType;
    [TextArea(2, 5)] public string objectiveDescription;
    [Min(1)] public int requiredCount = 1;
    public bool optionalObjective;
    [Tooltip("All listed objectives must be completed first (design metadata only). Empty means a starting objective.")]
    public PointNote[] prerequisiteObjectives = new PointNote[0];
    public bool showPrerequisiteLinks = true;
    public bool autoNameInHierarchy = true;
    [Tooltip("Mission-owned objective points. Array size is the number of objective slots.")]
    public PointNote[] objectives = new PointNote[0];
    public bool showObjectiveLinks = true;
    public bool linksOnlyWhenSelected = true;
    public bool showLinkLabels;
    [Range(1, 100)] public int maxVisibleLinks = 20;
    [Header("Related Content")]
    public ItemSO relatedItem;
    public DocumentSO relatedDocument;
    public GameObject relatedPrefab;
    public GameObject relatedObject;
    public bool showGizmos = true;
    public bool showLabel = true;
    [Min(.02f)] public float markerSize = .15f;
    [Range(5, 20)] public int labelSize = 10;
    public Vector3 labelOffset = new Vector3(0, .3f, 0);
    public bool overrideColors;
    public Color markerColor = Color.cyan;
    public Color labelBackground = new Color(.08f, .1f, .12f, .95f);
    public Color labelTextColor = Color.white;
    [System.Serializable]
    public struct NoteColors { public Color marker, background, text; }
#if UNITY_EDITOR
    public static System.Func<PointType, NoteColors> PaletteResolver;
    private Texture2D labelTexture;
    private Color textureColor;
    private void OnDisable() { if (labelTexture != null) DestroyImmediate(labelTexture); }
#endif
    public static NoteColors DefaultColors(PointType type)
    {
        Color color = Color.HSVToRGB(((int)type * .137f + .5f) % 1f, .65f, 1);
        return new NoteColors { marker = color, background = new Color(color.r * .18f, color.g * .18f, color.b * .18f, .95f), text = Color.white };
    }
    private NoteColors Colors
    {
        get
        {
            if (overrideColors) return new NoteColors { marker = markerColor, background = labelBackground, text = labelTextColor };
#if UNITY_EDITOR
            if (PaletteResolver != null) return PaletteResolver(pointType);
#endif
            return DefaultColors(pointType);
        }
    }
    public Color MarkerColor => Colors.marker;
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        Gizmos.color = MarkerColor;
        Gizmos.DrawWireSphere(transform.position, markerSize);
        Gizmos.DrawLine(transform.position, transform.position + labelOffset);
#if UNITY_EDITOR
        DrawObjectiveLinks();
        DrawPrerequisiteLinks();
        if (showLabel)
        {
            var style = new GUIStyle(UnityEditor.EditorStyles.helpBox) { fontSize = labelSize, fontStyle = FontStyle.Bold, padding = new RectOffset(4,4,2,2) };
            var colors = Colors;
            bool created = labelTexture == null;
            if (created)
                labelTexture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            if (created || textureColor != colors.background)
            { textureColor = colors.background; labelTexture.SetPixel(0, 0, textureColor); labelTexture.Apply(); }
            style.normal.background = labelTexture;
            style.normal.textColor = colors.text;
            UnityEditor.Handles.Label(transform.position + labelOffset, "[" + pointType + " | " + status + "] " + (string.IsNullOrWhiteSpace(title) ? name : title), style);
        }
#endif
    }
#if UNITY_EDITOR
    private void DrawPrerequisiteLinks()
    {
        if (pointType != PointType.Objective || !showPrerequisiteLinks || prerequisiteObjectives == null) return;
        if (linksOnlyWhenSelected)
        {
            bool selected = UnityEditor.Selection.Contains(gameObject);
            foreach (var prerequisite in prerequisiteObjectives)
                if (prerequisite != null && UnityEditor.Selection.Contains(prerequisite.gameObject)) selected = true;
            foreach (var selectedObject in UnityEditor.Selection.gameObjects)
            {
                var mission = selectedObject.GetComponent<PointNote>();
                if (mission != null && mission.pointType == PointType.Mission && mission.gameObject.scene == gameObject.scene && mission.objectives != null && System.Array.IndexOf(mission.objectives, this) >= 0)
                    selected = true;
            }
            if (!selected) return;
        }
        int count = 0;
        var unique = new System.Collections.Generic.HashSet<PointNote>();
        foreach (var prerequisite in prerequisiteObjectives)
        {
            if (prerequisite == null || prerequisite == this || prerequisite.pointType != PointType.Objective || prerequisite.gameObject.scene != gameObject.scene || !unique.Add(prerequisite)) continue;
            if (count++ >= maxVisibleLinks) break;
            Vector3 from = prerequisite.transform.position, to = transform.position;
            UnityEditor.Handles.color = MarkerColor;
            UnityEditor.Handles.DrawAAPolyLine(3, from, to);
            Vector3 direction = to - from;
            if (direction.sqrMagnitude > .001f)
                UnityEditor.Handles.ConeHandleCap(0, Vector3.Lerp(from, to, .85f), Quaternion.LookRotation(direction), UnityEditor.HandleUtility.GetHandleSize(to) * .12f, EventType.Repaint);
            if (showLinkLabels) UnityEditor.Handles.Label((from + to) * .5f, prerequisite.objectiveId + " -> " + objectiveId, UnityEditor.EditorStyles.miniBoldLabel);
        }
    }

    private void DrawObjectiveLinks()
    {
        if (!showGizmos || !showObjectiveLinks || pointType != PointType.Mission || objectives == null) return;
        if (linksOnlyWhenSelected)
        {
            bool selected = UnityEditor.Selection.Contains(gameObject);
            foreach (var objective in objectives)
                if (objective != null && UnityEditor.Selection.Contains(objective.gameObject)) selected = true;
            if (!selected) return;
        }
        int drawn = 0;
        var unique = new System.Collections.Generic.HashSet<PointNote>();
        foreach (var objective in objectives)
        {
            if (objective == null || objective == this || objective.pointType != PointType.Objective || objective.gameObject.scene != gameObject.scene || !unique.Add(objective)) continue;
            if (drawn++ >= maxVisibleLinks) break;
            UnityEditor.Handles.color = MarkerColor;
            Vector3 from = transform.position, to = objective.transform.position;
            UnityEditor.Handles.DrawDottedLine(from, to, 5);
            Vector3 direction = to - from;
            if (direction.sqrMagnitude > .001f)
                UnityEditor.Handles.ConeHandleCap(0, Vector3.Lerp(from, to, .85f), Quaternion.LookRotation(direction), UnityEditor.HandleUtility.GetHandleSize(to) * .12f, EventType.Repaint);
            if (showLinkLabels) UnityEditor.Handles.Label((from + to) * .5f, objective.objectiveId, UnityEditor.EditorStyles.miniBoldLabel);
        }
    }
#endif
}
