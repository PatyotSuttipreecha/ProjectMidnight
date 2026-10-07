using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DocumentPickup : MonoBehaviour, IWorldInteractable
{
    public DocumentSO document;
    [Tooltip("Optional override. Empty uses the Documents UI prefab assigned in the scene inventory.")]
    public DocumentLibraryView pickupViewPrefab;
    [Min(.1f)] public float interactionDistance = 2.5f;
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
        if (!DocumentLibraryUI.OpenPickup(document, player, pickupViewPrefab, () => { if (this != null) gameObject.SetActive(false); }))
            return "Cannot open document: assign a Documents UI prefab and an active Canvas.";
        return "";
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
