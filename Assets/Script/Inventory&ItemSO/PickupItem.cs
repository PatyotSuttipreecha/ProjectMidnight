using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PickupItem : MonoBehaviour
{
    public ItemSO itemData;
    [Min(1)] public int quantity = 1;
    [SerializeField] private TMP_Text text;
    private static readonly HashSet<PickupItem> available = new HashSet<PickupItem>();
    private static int handledFrame = -1;
    private readonly HashSet<Collider> overlaps = new HashSet<Collider>();
    private Transform owner;
    private InventoryItemData storedItem;
    private bool collected;
    private void OnEnable() { available.Add(this); }
    private void Start()
    {
        if (text != null) text.gameObject.SetActive(false);
        BuildWeaponVisual();
    }
    [ContextMenu("Build Weapon Preview")]
    public void BuildWeaponVisual()
    {
        if (itemData == null || itemData.itemType != ItemType.Weapon || itemData.weaponVisualPrefab == null) return;
        if (transform.Find("Weapon Visual") != null) return;
        var visual = new GameObject("Weapon Visual");
        visual.transform.SetParent(transform, false);
        // Copy render geometry only: world pickups must never run shooting, recoil or aiming scripts.
        CopyVisual(itemData.weaponVisualPrefab.transform, visual.transform);
        var renderers = visual.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        visual.transform.position += transform.position - bounds.center;
        // Lay the weapon on the nearest solid ground, without hitting pickup triggers.
        RaycastHit[] hits = Physics.RaycastAll(transform.position + Vector3.up, Vector3.down, 4f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        foreach (var hit in hits)
        {
            if (hit.collider.GetComponentInParent<PlayerController>() != null || hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.distance < nearest) { nearest = hit.distance; bounds.center = hit.point; }
        }
        if (nearest < float.PositiveInfinity)
            transform.position = new Vector3(transform.position.x, bounds.center.y + bounds.extents.y + .03f, transform.position.z);
    }
    private static void CopyVisual(Transform source, Transform destination)
    {
        if (source.TryGetComponent<MeshFilter>(out var filter) && source.TryGetComponent<MeshRenderer>(out var renderer))
        {
            destination.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            destination.gameObject.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
        }
        foreach (Transform child in source)
        {
            if (child.GetComponent<ParticleSystem>() != null) continue;
            var node = new GameObject(child.name).transform;
            node.SetParent(destination, false);
            node.localPosition = child.localPosition; node.localRotation = child.localRotation; node.localScale = child.localScale;
            CopyVisual(child, node);
        }
    }
    private void OnGUI()
    {
        if (collected || itemData == null || itemData.itemType != ItemType.Weapon || overlaps.Count == 0 || owner == null || Camera.main == null) return;
        var player = owner.GetComponent<PlayerController>();
        if (player == null || player.isCheckInventory) return;
        Vector3 point = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * .3f);
        if (point.z > 0) GUI.Label(new Rect(point.x - 100f, Screen.height - point.y, 220f, 30f), "[F] Pick up " + itemData.itemName);
    }
    private void OnDrawGizmosSelected()
    {
        if (itemData == null || itemData.itemType != ItemType.Weapon) return;
        Gizmos.color = new Color(.2f, .9f, .7f);
        if (TryGetComponent<SphereCollider>(out var sphere))
            Gizmos.DrawWireSphere(transform.TransformPoint(sphere.center), sphere.radius * transform.lossyScale.x);
    }
    private void OnDisable()
    {
        available.Remove(this); overlaps.Clear(); owner = null;
        if (text != null) text.gameObject.SetActive(false);
    }
    public void SetStoredItem(InventoryItemData item) { storedItem = item; itemData = item.itemSO; }
    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;
        owner = player.transform; overlaps.Add(other);
    }
    private void OnTriggerExit(Collider other) { overlaps.Remove(other); }
    private void Update()
    {
        overlaps.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
        bool nearby = !collected && overlaps.Count > 0 && owner != null;
        if (text != null) text.gameObject.SetActive(nearby);
        PlayerController player = owner != null ? owner.GetComponent<PlayerController>() : null;
        if (!nearby || player == null || player.isCheckInventory || !Input.GetKeyDown(KeyCode.F) || handledFrame == Time.frameCount) return;
        PickupItem nearest = null;
        float distance = float.PositiveInfinity;
        foreach (PickupItem pickup in available)
        {
            if (pickup == null || pickup.collected || pickup.itemData == null || pickup.owner != owner || pickup.overlaps.Count == 0) continue;
            float candidate = (pickup.transform.position - owner.position).sqrMagnitude;
            if (candidate < distance || (candidate == distance && nearest != null && pickup.GetInstanceID() < nearest.GetInstanceID()))
            { nearest = pickup; distance = candidate; }
        }
        handledFrame = Time.frameCount;
        if (nearest != null) nearest.TryPickup();
    }
    private void TryPickup()
    {
        if (collected || itemData == null) return;
        bool added = false;
        if (InventoryManager.Instance != null)
            added = storedItem != null ? InventoryManager.Instance.TryAddInstance(storedItem) : InventoryManager.Instance.TryAddItemAutoPlace(itemData, quantity);
        if (!added) { Debug.Log("Cannot pick up: no inventory space.", this); return; }
        collected = true; available.Remove(this);
        gameObject.SetActive(false); Destroy(gameObject);
    }
}
