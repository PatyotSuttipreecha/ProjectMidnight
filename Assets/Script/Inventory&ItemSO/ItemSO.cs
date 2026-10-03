using UnityEngine;

[CreateAssetMenu(fileName = "ItemSO", menuName = "Scriptable Objects/ItemSO")]
public class ItemSO : ScriptableObject
{
    [Header("Basic Information")]
    public string itemName;
    public ItemType itemType;
    public Sprite icon;
    [Tooltip("World pickup prefab for dropped items. Requires PickupItem and a trigger collider.")]
    public PickupItem pickupPrefab;

    [Header("Inventory Size")]
    [Range(1,4)]public int height;
    [Range(1,4)]public int width;

    [Header("Gun Data")]
    public Guns.WeaponType weaponType;
    public int ammoAmount;

    [Header("Heal Data")]
    public HealType healType;
    public int healAmount;
}
public enum ItemType
{ Health,Weapon,Ammo,Key,Collection,Resources,Amulets }
public enum HealType
{ Bandage,PainKiller,FirstAid }

