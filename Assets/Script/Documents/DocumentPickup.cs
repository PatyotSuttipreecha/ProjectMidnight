using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DocumentPickup : MonoBehaviour
{
    public DocumentSO document;
    public KeyCode collectKey = KeyCode.E;
    private static readonly HashSet<DocumentPickup> available = new HashSet<DocumentPickup>();
    private static int handledFrame = -1;
    private readonly HashSet<Collider> overlaps = new HashSet<Collider>();
    private PlayerController player;
    private void OnEnable() { available.Add(this); }
    private void OnDisable() { available.Remove(this); overlaps.Clear(); player = null; }
    private void OnTriggerEnter(Collider other)
    {
        var owner = other.GetComponentInParent<PlayerController>();
        if (owner == null) return;
        player = owner; overlaps.Add(other);
    }
    private void OnTriggerExit(Collider other) { overlaps.Remove(other); }
    private bool Available
    {
        get
        {
            overlaps.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
            return document != null && player != null && overlaps.Count > 0 && !player.isCheckInventory;
        }
    }
    private bool IsNearest()
    {
        float distance = (transform.position - player.transform.position).sqrMagnitude;
        foreach (var other in available)
        {
            if (other == this || !other.Available || other.player != player || other.collectKey != collectKey) continue;
            float candidate = (other.transform.position - player.transform.position).sqrMagnitude;
            if (candidate < distance || (candidate == distance && other.GetInstanceID() < GetInstanceID())) return false;
        }
        return true;
    }
    private void Update()
    {
        if (document != null && DocumentCollection.Instance != null && DocumentCollection.Instance.Contains(document))
        { gameObject.SetActive(false); return; }
        if (!Available || !Input.GetKeyDown(collectKey) || handledFrame == Time.frameCount || !IsNearest()) return;
        handledFrame = Time.frameCount;
        if (DocumentCollection.Instance == null) { Debug.LogWarning("Add a Document Collection to the scene.", this); return; }
        if (DocumentCollection.Instance.Collect(document)) gameObject.SetActive(false);
    }
    private void OnGUI()
    {
        if (!Available || !IsNearest() || Camera.main == null) return;
        var point = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * .3f);
        if (point.z > 0) GUI.Label(new Rect(point.x - 140, Screen.height - point.y, 320, 50),
            $"[{collectKey}] Collect {document.title}\nI > Documents to read");
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(.9f, .75f, .25f);
        if (TryGetComponent<SphereCollider>(out var sphere))
            Gizmos.DrawWireSphere(transform.TransformPoint(sphere.center), sphere.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z));
    }
}
