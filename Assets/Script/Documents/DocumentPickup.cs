using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(PointNote))]
public class DocumentPickup : MonoBehaviour, IWorldInteractable
{
    private void Reset()
    {
        var note = GetComponent<PointNote>();
        if (note != null && note.data == null && note.pointType == PointNote.PointType.General &&
            note.title == "Point Note" && string.IsNullOrWhiteSpace(note.note))
        {
            note.pointType = PointNote.PointType.Collectible;
            note.title = gameObject.name;
            note.relatedDocument = document;
        }
    }
    public DocumentSO document;
    [Tooltip("Optional override. Empty uses the Documents UI prefab assigned in the scene inventory.")]
    public DocumentLibraryView pickupViewPrefab;
    [Min(.1f)] public float interactionDistance = 2.5f;
    [Tooltip("Invoked after the pickup preview closes, the record is collected, and gameplay input is released. Connect CutsceneController.Play here.")]
    public UnityEvent onCollected = new UnityEvent();
    private void Start() => BuildEvidenceVisual();
    public void BuildEvidenceVisual()
    {
        if (document == null || !document.IsEvidence || document.inspectionPrefab == null ||
            GetComponentInChildren<Renderer>() != null) return;
        var visual = new GameObject("Evidence Visual");
        visual.transform.SetParent(transform, false);
        CopyGeometry(document.inspectionPrefab.transform, visual.transform);
    }
    private static void CopyGeometry(Transform source, Transform target)
    {
        if (source.TryGetComponent<MeshFilter>(out var filter) && source.TryGetComponent<MeshRenderer>(out var renderer))
        {
            target.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            target.gameObject.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
        }
        foreach (Transform child in source)
        {
            if (child.GetComponent<ParticleSystem>() != null) continue;
            var node = new GameObject(child.name).transform;
            node.SetParent(target, false);
            node.localPosition = child.localPosition; node.localRotation = child.localRotation; node.localScale = child.localScale;
            CopyGeometry(child, node);
        }
    }
    private void OnEnable() => PlayerInteraction.Register(this);
    private void OnDisable() => PlayerInteraction.Unregister(this);
    public MonoBehaviour InteractionOwner => this;
    public Vector3 InteractionPoint => transform.position + Vector3.up * .15f;
    public float InteractionRange => interactionDistance;
    public bool RequiresAim => false;
    public bool InteractionAvailable => document != null && (DocumentCollection.Instance == null || !DocumentCollection.Instance.Contains(document));
    public string InteractionPrompt => "Collect " + (document != null ? document.title : "document");
    public string Interact(PlayerController player)
    {
        if (document == null) return "Document is not assigned.";
        if (DocumentCollection.Instance == null) return "Document collection is unavailable.";
        if (DocumentCollection.Instance.Contains(document)) return "Document already collected.";
        if (!DocumentLibraryUI.OpenPickup(document, player, pickupViewPrefab, CompletePickup))
            return "Cannot open document: assign a Documents UI prefab and an active Canvas.";
        return "";
    }
    private void CompletePickup()
    {
        if (this == null) return;
        gameObject.SetActive(false);
        onCollected.Invoke();
    }
    private void Update()
    {
        if (document != null && DocumentCollection.Instance != null && DocumentCollection.Instance.Contains(document)) gameObject.SetActive(false);
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(.9f, .75f, .25f);
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}
