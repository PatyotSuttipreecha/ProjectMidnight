using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PickupItem : MonoBehaviour
{
    public ItemSO itemData;
    [SerializeField] private TMP_Text text;
    private static readonly HashSet<PickupItem> available = new HashSet<PickupItem>();
    private static int handledFrame = -1;
    private readonly HashSet<Collider> overlaps = new HashSet<Collider>();
    private Transform owner;
    private InventoryItemData storedItem;
    private bool collected;
    private void OnEnable() { available.Add(this); }
    private void Start() { if (text != null) text.gameObject.SetActive(false); }
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
        bool added = InventoryManager.TryApplyAmmo(itemData, owner);
        if (!added && InventoryManager.Instance != null)
            added = storedItem != null ? InventoryManager.Instance.TryAddInstance(storedItem) : InventoryManager.Instance.TryAddItemAutoPlace(itemData);
        if (!added) { Debug.Log("Cannot pick up: no inventory space or compatible weapon.", this); return; }
        collected = true; available.Remove(this);
        gameObject.SetActive(false); Destroy(gameObject);
    }
}
