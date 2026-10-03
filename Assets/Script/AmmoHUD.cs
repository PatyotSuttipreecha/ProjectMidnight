using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AmmoHUD : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text ammoText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color lowAmmoColor = new Color(1f, 0.35f, 0.25f);
    [SerializeField] private Color reloadColor = new Color(1f, 0.8f, 0.3f);
    [SerializeField, Range(0f, 1f)] private float lowAmmoFraction = 0.2f;
    private Guns lastGun;
    private int lastAmmo = -1, lastReserve = -1;
    private bool lastReload;
    private bool wasVisible;

    private void LateUpdate()
    {
        if (player == null) player = PlayerController.instance;
        Guns gun = player != null ? player.EquippedGun : null;
        bool visible = gun != null && gun.isActiveAndEnabled && gun.weaponStat.weaponName != Guns.WeaponType.None
            && gun.weaponStat.weaponName != Guns.WeaponType.Knife && !player.isCheckInventory;
        if (panel != null) panel.SetActive(visible);
        if (!visible) { wasVisible = false; return; }
        int ammo = Mathf.Max(0, gun.weaponStat.currentAmmo);
        int reserve = Mathf.Max(0, gun.weaponStat.ammoReserve);
        if (!wasVisible || lastGun != gun || lastAmmo != ammo || lastReserve != reserve || lastReload != gun.isReloading)
        {
            if (ammoText != null)
            {
                ammoText.SetText("{0} / {1}", ammo, reserve);
                ammoText.color = gun.isReloading ? reloadColor : ammo <= gun.weaponStat.magazineSize * lowAmmoFraction ? lowAmmoColor : normalColor;
            }
            if (statusText != null) statusText.text = gun.isReloading ? "RELOADING" : ammo == 0 ? (reserve > 0 ? "R  RELOAD" : "NO AMMO") : gun.weaponStat.weaponName.ToString().ToUpperInvariant();
            lastGun = gun; lastAmmo = ammo; lastReserve = reserve; lastReload = gun.isReloading;
        }
        wasVisible = true;
    }

    public static AmmoHUD CreateDefault(PlayerController owner)
    {
        GameObject root = new GameObject("Ammo HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(AmmoHUD));
        root.transform.SetParent(owner.transform, false);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;
        GameObject panel = new GameObject("Ammo Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        // Bottom-right, independent of player world position.
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-36f, 36f); rect.sizeDelta = new Vector2(240f, 100f);
        Image background = panel.GetComponent<Image>();
        background.color = new Color(0.035f, 0.045f, 0.06f, 0.8f); background.raycastTarget = false;
        AmmoHUD hud = root.GetComponent<AmmoHUD>();
        hud.player = owner; hud.panel = panel;
        hud.ammoText = CreateLabel(panel.transform, "Ammo", 38f, new Vector2(18f, -12f), new Vector2(208f, 46f));
        hud.statusText = CreateLabel(panel.transform, "Status", 16f, new Vector2(18f, -62f), new Vector2(208f, 26f));
        panel.SetActive(false);
        return hud;
    }
    private static TMP_Text CreateLabel(Transform parent, string name, float size, Vector2 position, Vector2 dimensions)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position; rect.sizeDelta = dimensions;
        TMP_Text text = obj.GetComponent<TMP_Text>();
        text.fontSize = size; text.color = Color.white; text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        return text;
    }
}
