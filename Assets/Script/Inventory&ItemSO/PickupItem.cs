using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PickupItem : MonoBehaviour, IWorldInteractable
{
    public ItemSO itemData;
    [Min(1)] public int quantity = 1;
    [SerializeField] private TMP_Text text;
    private InventoryItemData storedItem;
    private bool collected;
    private void OnEnable() => PlayerInteraction.Register(this);
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
    private void OnDrawGizmosSelected()
    {
        if (itemData == null) return;
        Gizmos.color = new Color(.2f, .9f, .7f);
        if (TryGetComponent<SphereCollider>(out var sphere))
            Gizmos.DrawWireSphere(transform.TransformPoint(sphere.center), sphere.radius * transform.lossyScale.x);
    }
    private void OnDisable()
    {
        PlayerInteraction.Unregister(this);
        if (text != null) text.gameObject.SetActive(false);
    }
    public void SetStoredItem(InventoryItemData item) { storedItem = item; itemData = item.itemSO; }
    [Min(.1f)] public float interactionDistance = 2.5f;
    public MonoBehaviour InteractionOwner => this;
    public Vector3 InteractionPoint => transform.position + Vector3.up * .15f;
    public float InteractionRange => interactionDistance;
    public bool RequiresAim => false;
    public bool InteractionAvailable => !collected && itemData != null;
    public string InteractionPrompt => "Pick up " + (itemData != null ? itemData.itemName : "item");
    public string Interact(PlayerController player)
    {
        return TryPickup() ? "Collected " + itemData.itemName : "Cannot pick up: inventory unavailable or no space.";
    }
    private bool TryPickup()
    {
        if (collected || itemData == null) return false;
        bool added = false;
        if (InventoryManager.Instance != null)
            added = storedItem != null ? InventoryManager.Instance.TryAddInstance(storedItem) : InventoryManager.Instance.TryAddItemAutoPlace(itemData, quantity);
        if (!added) return false;
        collected = true; PlayerInteraction.Unregister(this);
        gameObject.SetActive(false); Destroy(gameObject);
        return true;
    }
}
