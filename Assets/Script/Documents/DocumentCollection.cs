using System;
using System.Collections.Generic;
using UnityEngine;

// Collection state belongs to the player, never to the shared DocumentSO assets.
public class DocumentCollection : MonoBehaviour
{
    public static DocumentCollection Instance { get; private set; }
    public DocumentSO[] catalog = new DocumentSO[0];
    public event Action Changed;
    private readonly HashSet<string> collected = new HashSet<string>();
    private readonly HashSet<string> read = new HashSet<string>();
    private readonly List<DocumentSO> extraDocuments = new List<DocumentSO>();
    [Serializable] public class SaveState
    {
        public int version = 1;
        public string[] collectedIds = new string[0];
        public string[] readIds = new string[0];
    }
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (catalog != null) foreach (var document in catalog)
                if (document != null && !Instance.extraDocuments.Contains(document)) Instance.extraDocuments.Add(document);
            Destroy(gameObject); return;
        }
        Instance = this;
        if (transform.parent == null) DontDestroyOnLoad(gameObject);
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    public bool Contains(DocumentSO document) => document != null && collected.Contains(document.Id);
    public bool HasRead(DocumentSO document) => Contains(document) && read.Contains(document.Id);
    public bool Collect(DocumentSO document)
    {
        if (document == null || string.IsNullOrWhiteSpace(document.Id)) return false;
        if (!extraDocuments.Contains(document)) extraDocuments.Add(document);
        if (!collected.Add(document.Id)) return false;
        Changed?.Invoke(); return true;
    }
    public void MarkRead(DocumentSO document)
    {
        if (Contains(document) && read.Add(document.Id)) Changed?.Invoke();
    }
    public List<DocumentSO> GetCollected()
    {
        var result = new List<DocumentSO>();
        var ids = new HashSet<string>();
        if (catalog != null) foreach (var document in catalog) Add(document);
        foreach (var document in extraDocuments) Add(document);
        return result;
        void Add(DocumentSO document)
        { if (Contains(document) && ids.Add(document.Id)) result.Add(document); }
    }
    public SaveState CaptureState() => new SaveState
    { collectedIds = new List<string>(collected).ToArray(), readIds = new List<string>(read).ToArray() };
    public void RestoreState(SaveState state)
    {
        if (state == null || state.version != 1) return;
        collected.Clear(); read.Clear();
        if (state.collectedIds != null) foreach (var id in state.collectedIds)
            if (!string.IsNullOrWhiteSpace(id)) collected.Add(id);
        if (state.readIds != null) foreach (var id in state.readIds)
            if (collected.Contains(id)) read.Add(id);
        Changed?.Invoke();
    }
}
