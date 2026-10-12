using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Level Design/Point Note")]
public class PointNote : MonoBehaviour
{
    public PointNoteData data;
    public enum NoteStatus { Info, Todo, Issue, Done }
    public enum PointType { General, Mission, Objective, Collectible, Loot, EnemySpawn, Patrol, Interaction, Puzzle, Trigger, Audio, SafeZone, Location }
    public enum ObjectiveType { Collect, Reach, Interact, ReadDocument, Defeat, Other }
    [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("pointType")] private PointType legacy_pointType;
    public PointType pointType { get => data != null ? data.pointType : legacy_pointType; set { if (data != null) data.pointType = value; else legacy_pointType = value; } }
    [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("title")] private string legacy_title = "Point Note";
    public string title { get => data != null ? data.title : legacy_title; set { if (data != null) data.title = value; else legacy_title = value; } }
    [Tooltip("Design work status: Info, Todo, Issue or Done. This does not complete a gameplay mission.")]
    [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("status")] private NoteStatus legacy_status;
    public NoteStatus status { get => data != null ? data.status : legacy_status; set { if (data != null) data.status = value; else legacy_status = value; } }
    [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("note")] private string legacy_note;
    public string note { get => data != null ? data.note : legacy_note; set { if (data != null) data.note = value; else legacy_note = value; } }
    [Header("Mission / Objective Design")]
    [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("missionId")] private string legacy_missionId;
    public string missionId { get => data != null ? data.missionId : legacy_missionId; set { if (data != null) data.missionId = value; else legacy_missionId = value; } }
    [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("missionDescription")] private string legacy_missionDescription;
    public string missionDescription { get => data != null ? data.missionDescription : legacy_missionDescription; set { if (data != null) data.missionDescription = value; else legacy_missionDescription = value; } }
    [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("objectiveId")] private string legacy_objectiveId;
    public string objectiveId { get => data != null ? data.objectiveId : legacy_objectiveId; set { if (data != null) data.objectiveId = value; else legacy_objectiveId = value; } }
    [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("objectiveType")] private ObjectiveType legacy_objectiveType;
    public ObjectiveType objectiveType { get => data != null ? data.objectiveType : legacy_objectiveType; set { if (data != null) data.objectiveType = value; else legacy_objectiveType = value; } }
    [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("objectiveDescription")] private string legacy_objectiveDescription;
    public string objectiveDescription { get => data != null ? data.objectiveDescription : legacy_objectiveDescription; set { if (data != null) data.objectiveDescription = value; else legacy_objectiveDescription = value; } }
    [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("requiredCount")] private int legacy_requiredCount = 1;
    public int requiredCount { get => data != null ? data.requiredCount : legacy_requiredCount; set { if (data != null) data.requiredCount = value; else legacy_requiredCount = value; } }
    [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("optionalObjective")] private bool legacy_optionalObjective;
    public bool optionalObjective { get => data != null ? data.optionalObjective : legacy_optionalObjective; set { if (data != null) data.optionalObjective = value; else legacy_optionalObjective = value; } }
    [Tooltip("All listed objectives must be completed first (design metadata only). Empty means a starting objective.")]
    [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("prerequisiteObjectives")] private PointNote[] legacy_prerequisiteObjectives = new PointNote[0];
    public PointNote[] prerequisiteObjectives { get => data != null ? ResolvePoints(data.prerequisiteObjectives) : legacy_prerequisiteObjectives; set { if (data == null) legacy_prerequisiteObjectives = value; } }
    public bool showPrerequisiteLinks = true;
    [Tooltip("Editor-only two-way sync: Hierarchy name <-> Title. Names use [Point Type] Title.")]
    public bool autoNameInHierarchy = true;
    [Tooltip("Optional stable prefix, e.g. Prefab_Evidence_. Empty detects a Prefab_ prefix from the object or prefab source; ordinary notes use [Point Type].")]
    public string hierarchyNamePrefix;
    [Tooltip("Mission-owned objective points. Array size is the number of objective slots.")]
    [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("objectives")] private PointNote[] legacy_objectives = new PointNote[0];
    public PointNote[] objectives { get => data != null ? ResolvePoints(data.objectives) : legacy_objectives; set { if (data == null) legacy_objectives = value; } }
    public bool showObjectiveLinks = true;
    public bool linksOnlyWhenSelected = true;
    public bool showLinkLabels;
    [Range(1, 100)] public int maxVisibleLinks = 20;
    [Header("Related Content")]
    [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("relatedItem")] private ItemSO legacy_relatedItem;
    public ItemSO relatedItem { get => data != null ? data.relatedItem : legacy_relatedItem; set { if (data != null) data.relatedItem = value; else legacy_relatedItem = value; } }
    [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("relatedDocument")] private DocumentSO legacy_relatedDocument;
    public DocumentSO relatedDocument { get => data != null ? data.relatedDocument : legacy_relatedDocument; set { if (data != null) data.relatedDocument = value; else legacy_relatedDocument = value; } }
    [SerializeField, HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("relatedPrefab")] private GameObject legacy_relatedPrefab;
    public GameObject relatedPrefab { get => data != null ? data.relatedPrefab : legacy_relatedPrefab; set { if (data != null) data.relatedPrefab = value; else legacy_relatedPrefab = value; } }
    public GameObject relatedObject;
    public bool showGizmos = true;
    public bool showLabel = true;
    public bool showRelatedNotes = true;
    public bool relatedNotesOnlyWhenSelected;
    [Range(1, 10)] public int maxRelatedNoteLabels = 3;
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
    public static System.Action EditorChangeRequested;
    public static System.Func<bool> VisibilityResolver;
    private void OnValidate() { EditorChangeRequested?.Invoke(); }
    public static System.Func<PointType, NoteColors> PaletteResolver;
    private Texture2D labelTexture;
    private Color textureColor;
    private void OnDisable() { if (labelTexture != null) DestroyImmediate(labelTexture); }
#endif
    private PointNote[] ResolvePoints(PointNoteData[] records)
    {
        if (records == null) return new PointNote[0];
        var points = Object.FindObjectsByType<PointNote>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var result = new PointNote[records.Length];
        for (int i = 0; i < records.Length; i++)
            foreach (var point in points)
                if (records[i] != null && point.data == records[i] && point.gameObject.scene == gameObject.scene) { result[i] = point; break; }
        return result;
    }
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
#if UNITY_EDITOR
        if (VisibilityResolver != null && !VisibilityResolver()) return;
#endif
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
            UnityEditor.Handles.Label(transform.position + labelOffset, BuildViewportLabel(), style);
        }
#endif
    }
#if UNITY_EDITOR
    private string BuildViewportLabel()
    {
        var text = new System.Text.StringBuilder("[" + pointType + " | " + status + "] " +
            (string.IsNullOrWhiteSpace(title) ? name : title));
        if (!showRelatedNotes || data == null || data.relatedNotes == null ||
            (relatedNotesOnlyWhenSelected && !UnityEditor.Selection.Contains(gameObject))) return text.ToString();
        var unique = new System.Collections.Generic.HashSet<PointNoteData>();
        int count = 0;
        foreach (var related in data.relatedNotes)
        {
            if (related == null || related == data || !unique.Add(related)) continue;
            count++;
            if (count <= Mathf.Clamp(maxRelatedNoteLabels, 1, 10))
                text.Append("\n  Related [").Append(related.pointType).Append("] ")
                    .Append(string.IsNullOrWhiteSpace(related.title) ? related.name : related.title);
        }
        int remaining = count - Mathf.Clamp(maxRelatedNoteLabels, 1, 10);
        if (remaining > 0) text.Append("\n  +").Append(remaining).Append(" more related notes");
        return text.ToString();
    }

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
