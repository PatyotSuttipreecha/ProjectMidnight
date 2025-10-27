using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PickupItem : MonoBehaviour
{
    [Header("Item Data")]
    public ItemSO itemData;

    [Header("UI Text")]
    [SerializeField] private TMP_Text text;

    private bool isInArea;

    private void Start()
    {
        text?.gameObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isInArea = true;
            text?.gameObject.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isInArea = false;
            text?.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (isInArea && Input.GetKeyDown(KeyCode.F))
        {
            TryPickup();
        }
    }

    private void TryPickup()
    {
        var player = FindAnyObjectByType<PlayerController>();
        if (player == null) return;

        // ✅ ถ้าเป็นของทั่วไป ให้เข้า Inventory
        if (itemData.itemType == ItemType.Health || itemData.itemType == ItemType.Weapon)
        {
            bool added = InventoryManager.Instance.AddItem(itemData);
            if (added)
            {
                Debug.Log($"✅ {itemData.itemName} added to inventory.");
                Destroy(gameObject);
            }
            else
            {
                Debug.Log("❌ Inventory full!");
            }
            return;
        }

        // ของพิเศษอื่น ๆ เช่น Ammo, Key
        switch (itemData.itemType)
        {
            case ItemType.Ammo:
                AddAmmo();
                Destroy(gameObject);
                break;
        }
    }

    private void AddAmmo()
    {
        Guns[] allGuns = Resources.FindObjectsOfTypeAll<Guns>();
        foreach (var gun in allGuns)
        {
            if (gun.weaponStat.weaponName == itemData.weaponType)
            {
                int ammoGive = Random.Range(6, 12);
                gun.weaponStat.ammoReserve += ammoGive;
                Debug.Log($"🔫 Added {ammoGive} ammo to {gun.weaponStat.weaponName}");
                return;
            }
        }
    }
}
