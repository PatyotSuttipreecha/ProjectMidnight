using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Item : MonoBehaviour
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

        switch (itemData.itemType)
        {
            case ItemType.Ammo:
                AddAmmo();
                break;

            case ItemType.Health:
                AddHealToInventory();
                break;

            case ItemType.Weapon:
                AddWeaponToInventory();
                break;

            default:
                Debug.Log($"{itemData.itemName} collected!");
                break;
        }

        Destroy(gameObject);
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

    private void AddHealToInventory()
    {
        Debug.Log($"🗡️ Picked up Heal: {itemData.itemName}");
        // TODO: Add to InventoryGrid here later
    }

    private void AddWeaponToInventory()
    {
        Debug.Log($"🗡️ Picked up weapon: {itemData.itemName}");
        // TODO: Add to InventoryGrid here later
    }
}
