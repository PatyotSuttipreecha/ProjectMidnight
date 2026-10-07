// Standalone regression runner: compiles the actual inventory core with small Unity service stubs.
using System;
using System.Linq;
using UnityEngine;
public static class Checks
{
    static int passed;
    static void Check(bool result, string name) { if (!result) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
    static ItemSO Item(int w, int h, int stack = 1) => new ItemSO { width = w, height = h, maxStack = stack, itemName = "Test", allowRotation = true };
    static InventoryManager Bag(int w = 4, int h = 4) => new InventoryManager { inventoryGrid = new InventoryGrid(w, h), gridWidth = w, gridHeight = h };
    public static void Main()
    {
        var keyBag = Bag(); var key = Item(1, 1, 3); key.itemType = ItemType.Key;
        var wrongKey = Item(1, 1); wrongKey.itemType = ItemType.Key;
        Check(!keyBag.TryUseKey(key, true), "missing key rejected");
        keyBag.TryAddItemAutoPlace(key, 2);
        int keyNotifications = 0; keyBag.OnInventoryChanged += () => keyNotifications++;
        Check(!keyBag.TryUseKey(wrongKey, true), "different key rejected");
        Check(keyBag.TryUseKey(key, false) && keyBag.placedPositions.Keys.First().quantity == 2 && keyNotifications == 0, "reusable key preserved");
        Check(keyBag.TryUseKey(key, true) && keyBag.placedPositions.Keys.First().quantity == 1 && keyNotifications == 1, "consume one key and notify UI");
        Check(keyBag.TryUseKey(key, true) && keyBag.placedPositions.Count == 0 && keyBag.inventoryGrid.grid[0, 0] == null && keyNotifications == 2, "last key clears grid");
        var bag = Bag(); var pistol = new InventoryItemData(Item(2, 1)); var shotgun = new InventoryItemData(Item(4, 1));
        Check(bag.TryAddInstance(pistol) && bag.TryAddInstance(shotgun), "mixed footprints fit 4x4");
        Check(!bag.TryMoveItem(shotgun, 1, 2, false) && bag.placedPositions[shotgun].y == 1, "invalid bounds preserve position");
        Check(!bag.TryMoveItem(shotgun, 0, 0, false) && bag.inventoryGrid.grid[0, 0] == pistol, "collision preserves both items");
        Check(bag.TryMoveItem(pistol, 1, 0, false) && bag.inventoryGrid.grid[0, 0] == null, "self-overlapping move succeeds");
        Check(bag.TryMoveItem(shotgun, 3, 0, true) && shotgun.Width == 1 && shotgun.Height == 4, "shotgun rotates to 1x4");
        shotgun.itemSO.allowRotation = false;
        Check(!bag.TryMoveItem(shotgun, 0, 2, false) && shotgun.isRotated, "rotation disabled on definition");
        Check(shotgun.itemSO.width == 4 && shotgun.itemSO.height == 1, "runtime rotation preserves SO");
        bag.ResizeGrid(1, 1);
        Check(bag.inventoryGrid.width == 4 && bag.placedPositions.Count == 2, "failed resize preserves inventory");
        var ammo = Item(1, 1, 5); bag = Bag(2, 1);
        Check(bag.TryAddItemAutoPlace(ammo, 7) && bag.placedPositions.Count == 2 && bag.placedPositions.Keys.Sum(i => i.quantity) == 7, "split incoming quantity into stacks");
        var incoming = new InventoryItemData(ammo) { quantity = 4 };
        Check(!bag.TryAddInstance(incoming) && incoming.quantity == 4 && bag.placedPositions.Keys.Sum(i => i.quantity) == 7, "failed pickup cannot partially fill stacks");
        Check(bag.TryAddItemAutoPlace(ammo, 3) && bag.placedPositions.Keys.All(i => i.quantity == 5), "merge into full grid if capacity exists");
        Check(!bag.TryAddItemAutoPlace(ammo, 0), "reject zero quantities");
        bag = Bag(); bag.TryAddItemAutoPlace(ammo, 4);
        var source = bag.placedPositions.Keys.First();
        bag.TryMoveItem(source, 2, 0, false);
        var target = new InventoryItemData(ammo) { quantity = 4 };
        // Place separately to exercise user-driven partial merging.
        bag.inventoryGrid.PlaceItem(target, 0, 0); bag.placedPositions[target] = new Vector2Int(0, 0);
        Check(bag.TryMergeItems(source, target) && target.quantity == 5 && source.quantity == 3, "partial merge preserves excess");
        target.quantity = 1;
        Check(bag.TryMergeItems(source, target) && target.quantity == 4 && !bag.placedPositions.ContainsKey(source) && bag.inventoryGrid.grid[2, 0] == null, "complete merge removes source footprint");
        var weapon = Item(2, 1, 99); weapon.itemType = ItemType.Weapon;
        Check(weapon.StackLimit == 1, "weapons never stack");
        bag = Bag(); bag.TryAddItemAutoPlace(weapon);
        var weaponItem = bag.placedPositions.Keys.First();
        PlayerController.instance = new PlayerController();
        bag.UseItemAt(0, 0);
        Check(PlayerController.instance.lastEquipped == weaponItem && weaponItem.quantity == 1 && bag.placedPositions.ContainsKey(weaponItem), "equip delegates to player without consuming weapon");
        bag = Bag(); var medicine = Item(1, 1, 5); medicine.itemType = ItemType.Health; medicine.healAmount = 10;
        bag.TryAddItemAutoPlace(medicine, 3); var med = bag.placedPositions.Keys.First();
        PlayerController.instance = new PlayerController { currentHealth = 50, maxHealth = 100 };
        bag.UseItemAt(0, 0);
        Check(med.quantity == 2 && PlayerController.instance.currentHealth == 60, "use consumes one unit");
        PlayerController.instance.currentHealth = 100; bag.UseItemAt(0, 0);
        Check(med.quantity == 2, "full health preserves stack");
        var firearm = Item(2, 1); firearm.itemType = ItemType.Weapon;
        firearm.startingMagazineAmmo = 12; firearm.startingReserveAmmo = 36;
        var first = new InventoryItemData(firearm); var second = new InventoryItemData(firearm);
        first.magazineAmmo = 3; first.reserveAmmo = 17;
        Check(second.magazineAmmo == 12 && second.reserveAmmo == 36, "same weapon definition has independent ammo instances");
        firearm.pickupPrefab = new PickupItem(); bag = Bag(); bag.TryAddInstance(first);
        PlayerController.instance = new PlayerController(); bag.RemoveItemAt(0, 0);
        Check(bag.placedPositions.Count == 0 && firearm.pickupPrefab.stored == first && first.magazineAmmo == 3 && first.reserveAmmo == 17,
            "drop transfers actual weapon instance and ammunition");
        Check(bag.TryAddInstance(firearm.pickupPrefab.stored) && bag.placedPositions.Keys.First() == first && first.magazineAmmo == 3,
            "recollect preserves identity and magazine");
        var fullBag = Bag(1, 1); fullBag.TryAddItemAutoPlace(Item(1, 1));
        Check(!fullBag.TryAddInstance(second) && second.magazineAmmo == 12 && second.reserveAmmo == 36 && fullBag.placedPositions.Count == 1,
            "failed weapon pickup preserves both inventory and world ammunition");
        Console.WriteLine($"{passed} checks passed.");
    }
}
namespace UnityEngine
{
    public class Object { public static void Destroy(object value) {} public static T Instantiate<T>(T value, Vector3 p, Quaternion q) => value; }
    public class ScriptableObject : Object {}
    public class MonoBehaviour : Object { public GameObject gameObject = new GameObject(); public Transform transform = new Transform(); }
    public class GameObject { public object scene; public void SetActive(bool value) {} }
    public class Transform { public Vector3 position, forward; public T GetComponent<T>() where T : class => null; public T[] GetComponentsInChildren<T>(bool all) => Array.Empty<T>(); }
    public class Sprite {}
    public struct Vector2Int { public int x,y; public Vector2Int(int x,int y) { this.x=x; this.y=y; } }
    public struct Vector3 { public Vector3(float x,float y,float z) {} public static Vector3 up; public static Vector3 operator +(Vector3 a,Vector3 b)=>a; public static Vector3 operator *(Vector3 a,float b)=>a; }
    public struct Quaternion { public static Quaternion identity; }
    public static class Random { public static int Range(int min,int max)=>min; }
    public static class Mathf { public static int Max(int a,int b)=>Math.Max(a,b); public static int Min(int a,int b)=>Math.Min(a,b); public static float Clamp(float v,float a,float b)=>Math.Clamp(v,a,b); }
    public static class Debug { public static void Log(object value) {} public static void LogWarning(object value) {} }
    public class HeaderAttribute : Attribute { public HeaderAttribute(string x) {} }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string x) {} }
    public class TextAreaAttribute : Attribute { public TextAreaAttribute(int min, int max) {} }
    public class MinAttribute : Attribute { public MinAttribute(float x) {} }
    public class RangeAttribute : Attribute { public RangeAttribute(float a,float b) {} }
    public class HideInInspector : Attribute {}
    public class CreateAssetMenuAttribute : Attribute { public string fileName, menuName; }
}
namespace UnityEngine.SceneManagement { public static class SceneManager { public static void MoveGameObjectToScene(UnityEngine.GameObject g,object s) {} } }
public class PlayerController : UnityEngine.MonoBehaviour { public static PlayerController instance; public float currentHealth,maxHealth; public InventoryItemData lastEquipped; public InventoryItemData EquippedInventoryItem; public Guns EquippedGun; public bool TryEquipWeapon(InventoryItemData item) { lastEquipped = item; return true; } public bool TryUseHealing(ItemSO item) { if (item.healAmount <= 0 || currentHealth <= 0 || currentHealth >= maxHealth) return false; currentHealth = UnityEngine.Mathf.Clamp(currentHealth + item.healAmount, 0, maxHealth); return true; } public bool OwnsWeapon(Guns.WeaponType type) => true; public bool TryAddAmmoToOwnedWeapon(ItemSO ammo) => true; }
public class PickupItem : UnityEngine.MonoBehaviour { public bool enabled; public InventoryItemData stored; public void SetStoredItem(InventoryItemData item) { stored = item; } }
public class Guns { public enum WeaponType { None,Knife,Pistol,Shotgun,Rifle } public struct Stat { public WeaponType weaponName; } public Stat weaponStat; public bool isReloading; public void StoreInventoryAmmo() {} public void AddAmmo(int quantity) {} }
