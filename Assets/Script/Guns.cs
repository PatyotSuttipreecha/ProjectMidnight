using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class Guns : MonoBehaviour
{
    [System.Serializable]
    public struct WeaponStat
    {
        public WeaponType weaponName;
        public float damage;
        public float fireRate;
        public float recoil;
        public float recoilRecovery;
        public int currentAmmo;
        public int magazineSize;
        public float reloadTime;
        public int ammoReserve;

        [Header("Bullet Settings")]
        public float bulletSpeed;
        [Min(1)] public int pelletCount;
        [Tooltip("Shotgun pellet cone half-angle in degrees. Independent of aiming accuracy spread.")]
        [Range(0f, 45f)] public float pelletSpreadAngle;
        public float baseSpread;       // spread ปกติ
        public float minSpread;     // spread ต่ำสุดเมื่อเล็งมั่นคง
        public float aimTime;       // เวลาในการหด spread ให้แคบลง
        public GameObject bulletPrefab;
        public Transform firePoint;    // จุดที่กระสุนถูกยิงออกมา
        public ParticleSystem muzzleFlash;

        [Header("SoundSetting")]
        public SoundSO aimingSO;
        public SoundSO nonAimSO;
        public SoundSO shootSO;
        public SoundSO reloadSO;
        public SoundSO emptyMagSO;
    }

    public enum WeaponType
    {
        None,
        Knife,
        Pistol,
        Shotgun,
        Rifle
    }

    [Header("Weapon Status")]
    public WeaponStat weaponStat;

    public PlayerController playerController;
    public bool isReloading;
    public InventoryItemData BoundInventoryItem { get; private set; }

    public void BindInventoryItem(InventoryItemData item)
    {
        StoreInventoryAmmo();
        BoundInventoryItem = item;
        if (item == null) return;
        if (item.hasWeaponAmmo)
        {
            weaponStat.currentAmmo = Mathf.Clamp(item.magazineAmmo, 0, weaponStat.magazineSize);
            weaponStat.ammoReserve = Mathf.Max(0, item.reserveAmmo);
        }
        StoreInventoryAmmo();
    }

    public void StoreInventoryAmmo()
    {
        if (BoundInventoryItem == null) return;
        BoundInventoryItem.hasWeaponAmmo = true;
        BoundInventoryItem.magazineAmmo = weaponStat.currentAmmo;
        BoundInventoryItem.reserveAmmo = weaponStat.ammoReserve;
    }

    private Vector2 currentRecoil;
    private CinemachineRotationComposer camRotationComposer;

    private float currentSpread;    // spread ปัจจุบัน
    private float aimTimer;         // เวลาที่เล็งสะสม

    [Header("Crosshair")]
    public Crosshair crosshairUI; // assign ใน Inspector

    [Header("Aim Target")]
    [SerializeField] private AimPosition3D aimPosition;

    [Header("Breathing Sway")]
    [Tooltip("Sway amplitude as a fraction of camera viewport width/height.")]
    [SerializeField] private Vector2 breathingAmplitude = new Vector2(0.0015f, 0.002f);
    [SerializeField, Min(0f)] private float breathingFrequency = 0.25f;
    [SerializeField, Min(0f)] private float swayBlendSpeed = 4f;
    [Tooltip("Horizontal yaw and vertical pitch amplitude in degrees. Zero disables camera breathing.")]
    [SerializeField] private Vector2 cameraBreathingAmplitude = new Vector2(0.08f, 0.12f);
    private BreathingCameraSway breathingCameraSway;
    [Header("Movement Accuracy")]
    [SerializeField, Range(0f, 1f)] private float movingSpreadFraction = 0.12f;
    [SerializeField, Min(0f)] private float movementThreshold = 0.05f;
    [SerializeField, Min(0f)] private float movementBlendSpeed = 8f;
    private CharacterController movementController;
    private float movementWeight;
    private float breathingWeight;
    private float breathingPhase;
    private Vector2 aimViewportOffset;
    [Header("Spread Preview")]
    [SerializeField] private bool showSpreadGizmos = true;
    [SerializeField, Min(0.1f)] private float spreadPreviewDistance = 10f;
    private bool IsAiming => playerController != null && playerController.isAiming
        && !playerController.isCheckInventory && !isReloading;

    private void Awake()
    {
        if (aimPosition == null)
            aimPosition = FindFirstObjectByType<AimPosition3D>();
        if (playerController != null)
            movementController = playerController.GetComponent<CharacterController>();
    }

    private void Update()
    {
        // The player also has a Guns component for the unarmed slot. It must
        // not overwrite the equipped weapon's shared Aimpoint or crosshair.
        if (weaponStat.weaponName == WeaponType.None || weaponStat.weaponName == WeaponType.Knife) return;
        HandleAimingSpread();
        UpdateBreathingSway();
        if (playerController != null && playerController.isCheckInventory) return;
        if (!isReloading)
        {
            Shooting();
        }
        Reload();
        StoreInventoryAmmo();
    }

    private void LateUpdate()
    {
        if (camRotationComposer == null)
            return;

        Vector3 targetOffset = new Vector3(currentRecoil.y, currentRecoil.x, 0);
        camRotationComposer.TargetOffset = Vector3.Lerp(
            camRotationComposer.TargetOffset,
            Vector3.zero,
            Time.deltaTime * weaponStat.recoilRecovery
        ) + targetOffset;

        currentRecoil = Vector2.Lerp(currentRecoil, Vector2.zero, Time.deltaTime * weaponStat.recoilRecovery);
    }
    void HandleAimingSpread()
    {
        bool moving = movementController != null &&
            Vector3.ProjectOnPlane(movementController.velocity, Vector3.up).sqrMagnitude > movementThreshold * movementThreshold;
        movementWeight = Mathf.Lerp(movementWeight, moving ? 1f : 0f,
            1f - Mathf.Exp(-movementBlendSpeed * Time.deltaTime));
        float baseSpread = Mathf.Max(0f, weaponStat.baseSpread);
        float stationarySpread = Mathf.Clamp(weaponStat.minSpread, 0f, baseSpread);
        float movingSpread = Mathf.Max(stationarySpread, baseSpread * movingSpreadFraction);
        float minimumSpread = Mathf.Lerp(stationarySpread, movingSpread, movementWeight);
        if (IsAiming)
        {
            // เพิ่มเวลาที่เล็ง
            aimTimer += Time.deltaTime;

            // คำนวณ progress ของการหด spread (0 → 1)
            float t = weaponStat.aimTime > 0f ? Mathf.Clamp01(aimTimer / weaponStat.aimTime) : 1f;

            // ค่อยๆ ลด spread จาก baseSpread → minSpread
            currentSpread = Mathf.Lerp(baseSpread, minimumSpread, t);
            if(Input.GetMouseButtonDown(1))
            {
                SoundManager.PlaySound(weaponStat.aimingSO, Random.Range(0.1f,0.3f));
            }

        }
        else
        {
            // ถ้าไม่ได้เล็ง ให้รีเซ็ตกลับ
            aimTimer = 0f;
            currentSpread = baseSpread;
        }
        if (crosshairUI != null)
            crosshairUI.SetSpread(baseSpread > 0f ? currentSpread / baseSpread : 0f);
    }

    private void UpdateBreathingSway()
    {
        breathingPhase = Mathf.Repeat(breathingPhase + Time.deltaTime * breathingFrequency * Mathf.PI * 2f, Mathf.PI * 4f);
        breathingWeight = Mathf.Lerp(breathingWeight, IsAiming ? 1f : 0f,
            1f - Mathf.Exp(-swayBlendSpeed * Time.deltaTime));
        aimViewportOffset = new Vector2(Mathf.Sin(breathingPhase * 0.5f) * breathingAmplitude.x,
            Mathf.Sin(breathingPhase) * breathingAmplitude.y) * breathingWeight;
        if (breathingCameraSway == null && playerController != null && playerController.AimCamera != null)
        {
            CinemachineCamera virtualCamera = playerController.AimCamera;
            breathingCameraSway = virtualCamera.GetComponent<BreathingCameraSway>();
            if (breathingCameraSway == null)
                breathingCameraSway = virtualCamera.gameObject.AddComponent<BreathingCameraSway>();
        }
        if (breathingCameraSway != null)
            breathingCameraSway.RotationDegrees = new Vector2(
                Mathf.Sin(breathingPhase * 0.5f) * cameraBreathingAmplitude.x,
                Mathf.Sin(breathingPhase) * cameraBreathingAmplitude.y) * breathingWeight;
        Camera aimCamera = aimPosition != null ? aimPosition.AimCamera : Camera.main;
        if (aimPosition != null)
        {
            aimPosition.ViewportOffset = aimViewportOffset;
            aimPosition.RefreshTarget();
        }
        if (crosshairUI != null && aimCamera != null)
        {
            Vector3 center = aimCamera.ViewportToScreenPoint(new Vector3(0.5f, 0.5f, 0f));
            Vector3 point = aimCamera.ViewportToScreenPoint(new Vector3(0.5f + aimViewportOffset.x, 0.5f + aimViewportOffset.y, 0f));
            crosshairUI.SetScreenOffset((Vector2)(point - center));
        }
    }

    private void OnDisable()
    {
        StoreInventoryAmmo();
        if (weaponStat.weaponName == WeaponType.None || weaponStat.weaponName == WeaponType.Knife) return;
        breathingWeight = 0f;
        movementWeight = 0f;
        aimViewportOffset = Vector2.zero;
        if (breathingCameraSway != null) breathingCameraSway.RotationDegrees = Vector2.zero;
        if (aimPosition != null)
        {
            aimPosition.ViewportOffset = Vector2.zero;
            aimPosition.RefreshTarget();
        }
        if (crosshairUI != null) crosshairUI.SetScreenOffset(Vector2.zero);
    }


    void Shooting()
    {
        if (IsAiming && weaponStat.currentAmmo > 0)
        {
            if (Input.GetMouseButtonDown(0))
            {
                weaponStat.currentAmmo--;
                if (weaponStat.muzzleFlash != null) weaponStat.muzzleFlash.Play();
               
                FireBullet();

                // ยิงแล้วรีเซ็ต spread
                aimTimer = 0f;
                currentSpread = weaponStat.baseSpread;
                if (crosshairUI != null) crosshairUI.SetSpread(weaponStat.baseSpread > 0f ? 1f : 0f);

                Recoil();
                SoundManager.PlaySound(weaponStat.shootSO, 1);
                return;
            }
        }
        if (Input.GetMouseButtonDown(0) && weaponStat.currentAmmo <= 0)
        {
            SoundManager.PlaySound(weaponStat.emptyMagSO, 1);
            Debug.Log("Ammo running out");
        }
    }
    void FireBullet()
    {
        if (weaponStat.bulletPrefab == null || weaponStat.firePoint == null) return;

        Vector3 targetPoint;
        if (aimPosition != null)
        {
            // Use the same camera, mask and fallback as the rig's Aimpoint.
            aimPosition.RefreshTarget();
            targetPoint = aimPosition.TargetPoint;
        }
        else
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null) return;
            Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f + aimViewportOffset.x, 0.5f + aimViewportOffset.y, 0f));
            targetPoint = Physics.Raycast(ray, out RaycastHit hit, 1000f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? hit.point : ray.GetPoint(1000f);
        }

        // 2. คำนวณทิศจาก firePoint → targetPoint
        Vector3 shootDirection = (targetPoint - weaponStat.firePoint.position).normalized;

        // 3. ใส่ Spread เพิ่มเข้าไป (เพื่อให้ไม่ตรง 100% เวลาไม่เล็ง)
        float spreadX = Random.Range(-currentSpread, currentSpread);
        float spreadY = Random.Range(-currentSpread, currentSpread);
        // Scatter around the shot's local axes, including when aiming up/down.
        shootDirection = Quaternion.LookRotation(shootDirection) *
            (Quaternion.Euler(-spreadY, spreadX, 0) * Vector3.forward);

        // 4. สร้างกระสุน
        int count = weaponStat.weaponName == WeaponType.Shotgun ? Mathf.Clamp(weaponStat.pelletCount, 1, 128) : 1;
        float pelletAngle = weaponStat.weaponName == WeaponType.Shotgun ? Mathf.Clamp(weaponStat.pelletSpreadAngle, 0f, 45f) : 0f;
        for (int i = 0; i < count; i++)
        {
            Vector3 pelletDirection = SamplePelletDirection(shootDirection, pelletAngle);
            GameObject bullet = Instantiate(weaponStat.bulletPrefab, weaponStat.firePoint.position, Quaternion.LookRotation(pelletDirection));
            Bullet bulletScript = bullet.GetComponent<Bullet>();
            if (bulletScript != null)
            {
                bulletScript.SetDamage(weaponStat.damage);
                bulletScript.SetSource(playerController != null ? playerController.transform : null);
                bulletScript.SetDirection(pelletDirection, weaponStat.bulletSpeed);
            }
            Debug.DrawRay(weaponStat.firePoint.position, pelletDirection * spreadPreviewDistance, Color.yellow, 1f);
        }

        // Debug ray
        Debug.DrawRay(weaponStat.firePoint.position, shootDirection * 10f, Color.red, 2f);
    }

    public static Vector3 SamplePelletDirection(Vector3 forward, float halfAngle)
    {
        if (forward.sqrMagnitude < 0.000001f) forward = Vector3.forward;
        if (halfAngle <= 0f) return forward.normalized;
        float cosine = Random.Range(Mathf.Cos(Mathf.Clamp(halfAngle, 0f, 45f) * Mathf.Deg2Rad), 1f);
        float sine = Mathf.Sqrt(Mathf.Max(0f, 1f - cosine * cosine));
        float azimuth = Random.Range(0f, Mathf.PI * 2f);
        return Quaternion.LookRotation(forward) * new Vector3(sine * Mathf.Cos(azimuth), sine * Mathf.Sin(azimuth), cosine);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showSpreadGizmos || weaponStat.firePoint == null) return;
        Vector3 origin = weaponStat.firePoint.position;
        Vector3 direction = weaponStat.firePoint.forward;
        if (Application.isPlaying && aimPosition != null && (aimPosition.TargetPoint - origin).sqrMagnitude > 0.000001f)
            direction = (aimPosition.TargetPoint - origin).normalized;
        float angle = weaponStat.weaponName == WeaponType.Shotgun ? Mathf.Clamp(weaponStat.pelletSpreadAngle, 0f, 45f) : 0f;
        DrawSpreadCone(origin, direction, angle, new Color(1f, 0.65f, 0.1f));
        float accuracy = Mathf.Max(0f, Application.isPlaying ? currentSpread : weaponStat.baseSpread);
        DrawSpreadCone(origin, direction, Mathf.Min(80f, angle + accuracy * 1.414214f), Color.cyan);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(origin + direction * spreadPreviewDistance,
            "Spread @ " + spreadPreviewDistance.ToString("0.#") + "m | orange: pellets, cyan: accuracy envelope");
#endif
    }
    private void DrawSpreadCone(Vector3 origin, Vector3 forward, float halfAngle, Color color)
    {
        float distance = Mathf.Max(0.1f, spreadPreviewDistance);
        float radius = Mathf.Tan(halfAngle * Mathf.Deg2Rad) * distance;
        Quaternion axes = Quaternion.LookRotation(forward);
        Vector3 centre = origin + forward * distance;
        Gizmos.color = color;
        Gizmos.DrawLine(origin, centre);
        Vector3 previous = centre + axes * Vector3.right * radius;
        for (int i = 1; i <= 32; i++)
        {
            float phase = i * Mathf.PI * 2f / 32f;
            Vector3 point = centre + axes * new Vector3(Mathf.Cos(phase) * radius, Mathf.Sin(phase) * radius, 0f);
            Gizmos.DrawLine(previous, point);
            if (i % 8 == 0) Gizmos.DrawLine(origin, point);
            previous = point;
        }
    }


    void Recoil()
    {
        camRotationComposer = FindFirstObjectByType<CinemachineRotationComposer>();

        float verticalRecoil = weaponStat.recoil;
        float horizontalRecoil = Random.Range(-weaponStat.recoil / 2f, weaponStat.recoil / 2f);

        currentRecoil += new Vector2(verticalRecoil, horizontalRecoil);
    }

    void Reload()
    {
        if (!isReloading && Input.GetKeyDown(KeyCode.R) && weaponStat.ammoReserve > 0 && weaponStat.currentAmmo < weaponStat.magazineSize)
        {
            StartCoroutine(Reload(weaponStat.reloadTime));
        }
    }

    IEnumerator Reload(float duration)
    {
       Animator animator = playerController.gameObject.GetComponent<Animator>();
        int reloadingLayerIndex;

        reloadingLayerIndex = animator.GetLayerIndex("Reloading");
        animator.SetLayerWeight(reloadingLayerIndex, 1f);

        isReloading = true;
        SoundManager.PlaySound(weaponStat.reloadSO, 0.4f);

        yield return new WaitForSeconds(duration);
        animator.SetLayerWeight(reloadingLayerIndex, 0f);
        int ammoNeeded = weaponStat.magazineSize - weaponStat.currentAmmo;
        int ammoToReload = Mathf.Min(ammoNeeded, weaponStat.ammoReserve);

        weaponStat.currentAmmo += ammoToReload;
        weaponStat.ammoReserve -= ammoToReload;

        isReloading = false;
        StoreInventoryAmmo();
    }
    public void AddAmmo(int amount)
    {
        weaponStat.ammoReserve += amount;
        StoreInventoryAmmo();
        Debug.Log($"Ammo Reserve: {weaponStat.ammoReserve}");
    }
}
