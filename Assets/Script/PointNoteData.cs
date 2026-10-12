using UnityEngine;

[CreateAssetMenu(menuName = "Level Design/Point Note Data", fileName = "NoteData")]
public class PointNoteData : ScriptableObject
{
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(recordId)) recordId = System.Guid.NewGuid().ToString("N");
#if UNITY_EDITOR
        PointNote.EditorChangeRequested?.Invoke();
#endif
    }
    [HideInInspector] public string recordId;
    public PointNote.PointType pointType;
    public string title = "New Note";
    public PointNote.NoteStatus status;
    [TextArea(4, 20)] public string note;
    [Tooltip("Chapter, region or location that this record belongs to, independent of loaded scenes.")]
    public string location;
    public string plannedScene;
    public string missionId;
    [TextArea(3, 8)] public string missionDescription;
    public string objectiveId;
    public PointNote.ObjectiveType objectiveType;
    [TextArea(2, 5)] public string objectiveDescription;
    [Min(1)] public int requiredCount = 1;
    public bool optionalObjective;
    public PointNoteData[] objectives = new PointNoteData[0];
    public PointNoteData[] prerequisiteObjectives = new PointNoteData[0];
    [Tooltip("Other design records associated with this record, e.g. a Trigger linked to a Reach Objective. These links do not execute gameplay events.")]
    public PointNoteData[] relatedNotes = new PointNoteData[0];
    public ItemSO relatedItem;
    public DocumentSO relatedDocument;
    public GameObject relatedPrefab;
}
