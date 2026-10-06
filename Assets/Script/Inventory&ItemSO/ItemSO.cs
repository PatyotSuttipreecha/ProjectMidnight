using UnityEngine;

[CreateAssetMenu(fileName = "ItemSO", menuName = "Scriptable Objects/ItemSO")]
public class ItemSO : ScriptableObject
{
    [Header("Basic Information")]
    public string itemName;
    [TextArea(3, 8)] public string description;
    public ItemType itemType;
    public Sprite icon;
    [Header("3D Inspection")]
    [Tooltip("Optional visual model for Examine. Falls back to Weapon Visual Prefab or Pickup Prefab.")]
    public GameObject inspectionPrefab;
    public Vector3 inspectionRotation = new Vector3(0, 25, 0);
    [Tooltip("World pickup prefab for dropped items. Requires PickupItem and a trigger collider.")]
    public PickupItem pickupPrefab;

    [Header("Inventory Size")]
    [Range(1,4)]public int height = 1;
    [Range(1,4)]public int width = 1;
    public bool allowRotation = true;
    [Tooltip("Units per grid item. Weapons always have a stack limit of one.")]
    [Min(1)] public int maxStack = 1;
    public int StackLimit => itemType == ItemType.Weapon ? 1 : Mathf.Max(1, maxStack);

    [Header("Gun Data")]
    public Guns.WeaponType weaponType;
    [Tooltip("Visual source for world weapon pickups; only meshes and materials are copied, not firing scripts.")]
    public GameObject weaponVisualPrefab;
    [Min(0)] public int startingMagazineAmmo;
    [Min(0)] public int startingReserveAmmo;
    public int ammoAmount;

    [Header("Heal Data")]
    public HealType healType;
    public int healAmount;
}
public enum ItemType
{ Health,Weapon,Ammo,Key,Collection,Resources,Amulets }
public enum HealType
{ Bandage,PainKiller,FirstAid }

