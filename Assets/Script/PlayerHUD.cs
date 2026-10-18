using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(250)]
public class PlayerHUD : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private CanvasGroup visibility;
    [SerializeField] private Image healthFill;
    [SerializeField] private Text healthValue, healthMaximum, weaponName, ammunition, weaponStatus;
    [SerializeField] private Image weaponIcon;
    [SerializeField] private Color healthyColor = new Color(.65f, .86f, .75f);
    [SerializeField] private Color dangerColor = new Color(1, .34f, .28f);
    [SerializeField, Range(0, 1)] private float dangerThreshold = .25f;
    public PlayerController Owner => player;

    public void Bind(PlayerController owner) { player = owner; Refresh(); }
    private void LateUpdate() => Refresh();
    public void Refresh()
    {
        if (player == null) player = PlayerController.instance;
        bool visible = player != null && player.isActiveAndEnabled && !player.IsGameplayInputBlocked;
        if (visibility != null) { visibility.alpha = visible ? 1 : 0; visibility.blocksRaycasts = false; visibility.interactable = false; }
        if (player == null) return;
        float maximum = Mathf.Max(0, player.maxHealth);
        float health = Mathf.Clamp(player.currentHealth, 0, maximum);
        float fraction = maximum > 0 ? health / maximum : 0;
        Color healthColor = fraction <= dangerThreshold ? dangerColor : healthyColor;
        if (healthFill != null) { healthFill.fillAmount = fraction; healthFill.color = healthColor; }
        if (healthValue != null) { healthValue.text = Mathf.CeilToInt(health).ToString(); healthValue.color = healthColor; }
        if (healthMaximum != null) healthMaximum.text = "/ " + Mathf.CeilToInt(maximum) + " HP";
        Guns gun = player.EquippedGun;
        bool equipped = gun != null && gun.isActiveAndEnabled && gun.weaponStat.weaponName != Guns.WeaponType.None;
        ItemSO item = equipped && gun.BoundInventoryItem != null ? gun.BoundInventoryItem.itemSO : null;
        if (weaponName != null) weaponName.text = !equipped ? "UNARMED" : item != null && !string.IsNullOrWhiteSpace(item.itemName)
            ? item.itemName : gun.weaponStat.weaponName.ToString().ToUpperInvariant();
        if (weaponIcon != null) { weaponIcon.sprite = item != null ? item.icon : null; weaponIcon.enabled = weaponIcon.sprite != null; }
        bool firearm = equipped && gun.weaponStat.weaponName != Guns.WeaponType.Knife;
        int ammo = firearm ? Mathf.Max(0, gun.weaponStat.currentAmmo) : 0;
        int reserve = firearm ? Mathf.Max(0, gun.weaponStat.ammoReserve) : 0;
        if (ammunition != null)
        {
            ammunition.text = firearm ? ammo + " <size=20><color=#9DA9AD>/ " + reserve + "</color></size>" : "—";
            ammunition.color = firearm && ammo == 0 ? dangerColor : new Color(.94f, .93f, .87f);
        }
        if (weaponStatus != null) weaponStatus.text = !firearm ? "" : gun.isReloading ? "RELOADING" : ammo == 0 ? (reserve > 0 ? "R  RELOAD" : "NO AMMO") : "MAG / RESERVE";
    }

    public static PlayerHUD EnsureFor(PlayerController owner)
    {
        foreach (var existing in FindObjectsByType<PlayerHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (existing.Owner == owner || existing.Owner == null && existing.gameObject.scene == owner.gameObject.scene)
            { existing.Bind(owner); existing.RetireOldHUD(owner); return existing; }
        var prefab = Resources.Load<GameObject>("PlayerHUD");
        PlayerHUD hud = prefab != null ? Instantiate(prefab, owner.transform).GetComponent<PlayerHUD>() : CreateDefault(owner);
        hud.Bind(owner);
        hud.RetireOldHUD(owner);
        return hud;
    }
    private void RetireOldHUD(PlayerController owner)
    {
        foreach (var old in FindObjectsByType<AmmoHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (old.gameObject.scene == owner.gameObject.scene) old.Retire();
    }

    public static PlayerHUD CreateDefault(PlayerController owner = null)
    {
        var root = new GameObject("Player HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup), typeof(PlayerHUD));
        if (owner != null) root.transform.SetParent(owner.transform, false);
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        root.GetComponent<Canvas>().sortingOrder = 10;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        var hud = root.GetComponent<PlayerHUD>(); hud.player = owner; hud.visibility = root.GetComponent<CanvasGroup>();
        hud.visibility.blocksRaycasts = false; hud.visibility.interactable = false;
        RectTransform card = Rect(root.transform, "Vitals and Equipment", new Vector2(440, 150), new Vector2(36, 36));
        card.anchorMin = card.anchorMax = card.pivot = Vector2.zero;
        Paint(card, new Color(.035f, .055f, .065f, .94f));
        Paint(Rect(card, "Top accent", new Vector2(440, 2), new Vector2(0, 148)), new Color(.72f, .64f, .46f));
        Sprite ring = Resources.Load<Sprite>("PlayerHealthRing");
        var track = Rect(card, "Health ring background", new Vector2(116, 116), new Vector2(18, 17));
        Paint(track, new Color(.18f, .25f, .25f)); track.GetComponent<Image>().sprite = ring;
        var fill = Rect(card, "Health ring fill", new Vector2(116, 116), new Vector2(18, 17));
        hud.healthFill = Paint(fill, hud.healthyColor); hud.healthFill.sprite = ring;
        hud.healthFill.type = Image.Type.Filled; hud.healthFill.fillMethod = Image.FillMethod.Radial360;
        hud.healthFill.fillOrigin = (int)Image.Origin360.Top; hud.healthFill.fillClockwise = true;
        hud.healthValue = Label(card, "Health value", "100", 34, new Vector2(24, 59), new Vector2(104, 42), TextAnchor.MiddleCenter);
        hud.healthMaximum = Label(card, "Health maximum", "/ 100 HP", 12, new Vector2(24, 41), new Vector2(104, 22), TextAnchor.MiddleCenter);
        Paint(Rect(card, "Divider", new Vector2(1, 110), new Vector2(150, 20)), new Color(.25f, .3f, .3f));
        Label(card, "Equipment heading", "EQUIPPED", 11, new Vector2(170, 117), new Vector2(235, 20));
        hud.weaponName = Label(card, "Weapon name", "UNARMED", 23, new Vector2(170, 83), new Vector2(235, 31));
        hud.weaponName.resizeTextForBestFit = true; hud.weaponName.resizeTextMinSize = 14; hud.weaponName.resizeTextMaxSize = 23;
        hud.ammunition = Label(card, "Ammunition", "—", 34, new Vector2(170, 34), new Vector2(170, 44));
        hud.weaponStatus = Label(card, "Weapon status", "", 11, new Vector2(170, 15), new Vector2(235, 20));
        hud.weaponIcon = Paint(Rect(card, "Weapon icon", new Vector2(64, 42), new Vector2(348, 34)), Color.white);
        hud.weaponIcon.preserveAspect = true; hud.weaponIcon.enabled = false;
        return hud;
    }
    private static RectTransform Rect(Transform parent, string name, Vector2 size, Vector2 position)
    {
        var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
        var rect = (RectTransform)obj.transform; rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
    }
    private static Image Paint(RectTransform rect, Color color)
    { var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image; }
    private static Text Label(Transform parent, string name, string value, int size, Vector2 position, Vector2 dimensions, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        var text = Rect(parent, name, dimensions, position).gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = size; text.text = value;
        text.color = new Color(.88f, .9f, .87f); text.alignment = alignment; text.raycastTarget = false; text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }
}
