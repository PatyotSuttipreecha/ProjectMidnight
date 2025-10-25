using UnityEngine;

[CreateAssetMenu(fileName = "ItemSO", menuName = "Scriptable Objects/ItemSO")]
public class ItemSO : ScriptableObject
{
    [Header("Basic Information")]
    public string itemName;
    public ItemType itemType;
    public Sprite icon;

    [Header("Stack & Size")]
    public int maxStack;
    public int height;
    public int width;

    [Header("Gun Data")]
    public Guns.WeaponType weaponType; // ����繡���ع
    public int ammoAmount;

    [Header("Heal Data")]
    public int healAmount;
}
public enum ItemType
{
    Health,
    Weapon,
    Ammo,
    Key,
    Collection,
    Resources,
    Amulets,
}
