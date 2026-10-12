using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

[AddComponentMenu("Gameplay/Item Spawn Point")]
[RequireComponent(typeof(PointNote))]
public class ItemSpawnPoint : MonoBehaviour
{
    public enum SelectionMode { FixedItem, RandomByType }
    public SelectionMode selectionMode;
    public ItemSO item;
    public ItemType randomItemType;
    [Tooltip("Candidate assets saved with this point. Only matching types with a usable pickup prefab are eligible.")]
    public ItemSO[] randomPool = new ItemSO[0];
    [Min(1)] public int quantity = 1;
    [Tooltip("Optional world pickup prefab. Otherwise uses the selected ItemSO's Pickup Prefab.")]
    public PickupItem pickupPrefabOverride;
    public bool spawnOnStart = true;
    [Range(0, 100)] public float spawnChance = 100;
    [Tooltip("Prevents this point from spawning again after its first successful spawn.")]
    public bool spawnOnce = true;
    public Vector3 localOffset;
    public Vector3 rotationOffset;
    public UnityEvent onSpawned = new UnityEvent();

    public PickupItem CurrentPickup { get; private set; }
    public bool HasSpawned { get; private set; }
    public bool HasRolled { get; private set; }
    public ItemSO LastSpawnedItem { get; private set; }
    public PickupItem EffectivePrefab => pickupPrefabOverride != null ? pickupPrefabOverride : item != null ? item.pickupPrefab : null;
    public Vector3 SpawnPosition => transform.TransformPoint(localOffset);

    private void Reset()
    {
        var note = GetComponent<PointNote>();
        // Keep any existing catalog binding or authored note intact.
        if (note != null && note.data == null && note.pointType == PointNote.PointType.General &&
            note.title == "Point Note" && string.IsNullOrWhiteSpace(note.note))
        {
            note.pointType = PointNote.PointType.Loot;
            note.title = gameObject.name;
        }
    }

    public ItemSpawnGroup Group => GetComponentInParent<ItemSpawnGroup>();
    public bool CanSpawn => isActiveAndEnabled && CurrentPickup == null && !(spawnOnce && HasRolled) &&
        (selectionMode == SelectionMode.RandomByType ? GetEligibleItems().Count > 0 : item != null && EffectivePrefab != null);

    private void Start() { if (spawnOnStart && Group == null) Spawn(); }

    // Void signature allows WorldTrigger and Timeline UnityEvents to call this method.
    public void Spawn() => SpawnItem();

    public List<ItemSO> GetEligibleItems()
    {
        var result = new List<ItemSO>();
        if (randomPool == null) return result;
        foreach (var candidate in randomPool)
            if (candidate != null && candidate.itemType == randomItemType &&
                (pickupPrefabOverride != null || candidate.pickupPrefab != null) && !result.Contains(candidate))
                result.Add(candidate);
        return result;
    }

    public ItemSO SelectItem()
    {
        if (selectionMode == SelectionMode.FixedItem) return item;
        var eligible = GetEligibleItems();
        return eligible.Count > 0 ? eligible[Random.Range(0, eligible.Count)] : null;
    }

    public PickupItem SpawnItem()
    {
        // A group owns allocation: point events must not bypass its location budget.
        if (Group != null) return null;
        return SpawnAllocated(true);
    }

    internal PickupItem SpawnAllocated(bool applyChance)
    {
        if (!Application.isPlaying || !CanSpawn) return null;
        HasRolled = true;
        float chance = Mathf.Clamp(spawnChance, 0, 100);
        if (applyChance && (chance <= 0 || (chance < 100 && Random.value >= chance / 100f))) return null;
        var selectedItem = SelectItem();
        var prefab = pickupPrefabOverride != null ? pickupPrefabOverride : selectedItem != null ? selectedItem.pickupPrefab : null;
        if (selectedItem == null || prefab == null)
        {
            Debug.LogWarning("Item Spawn Point has no eligible item with a world Pickup Prefab. Check the item or random pool/type.", this);
            return null;
        }
        // Configure before enabling: pickup registration and other prefab scripts see the correct item.
        var staging = new GameObject("Item Spawn Staging");
        staging.SetActive(false);
        var pickup = Instantiate(prefab, SpawnPosition,
            transform.rotation * Quaternion.Euler(rotationOffset), staging.transform);
        pickup.itemData = selectedItem;
        pickup.quantity = Mathf.Max(1, quantity);
        pickup.name = string.IsNullOrWhiteSpace(selectedItem.itemName) ? selectedItem.name : selectedItem.itemName;
        pickup.gameObject.SetActive(true);
        CurrentPickup = pickup;
        HasSpawned = true;
        LastSpawnedItem = selectedItem;
        pickup.transform.SetParent(null, true);
        Destroy(staging);
        onSpawned.Invoke();
        return pickup;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, .7f, .15f);
        Gizmos.DrawWireCube(SpawnPosition, Vector3.one * .25f);
        Gizmos.DrawLine(transform.position, SpawnPosition);
    }
}
