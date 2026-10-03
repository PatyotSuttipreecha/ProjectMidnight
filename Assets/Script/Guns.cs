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
        if (!isReloading)
        {
            Shooting();
        }
        Reload();
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
                weaponStat.muzzleFlash.Play();
               
                FireBullet();

                // ยิงแล้วรีเซ็ต spread
                aimTimer = 0f;
                currentSpread = weaponStat.baseSpread;
                if (crosshairUI != null) crosshairUI.SetSpread(weaponStat.baseSpread > 0f ? 1f : 0f);

                Recoil();
                SoundManager.PlaySound(weaponStat.shootSO, 1);
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
        GameObject bullet = Instantiate(weaponStat.bulletPrefab, weaponStat.firePoint.position, Quaternion.LookRotation(shootDirection));

        Rigidbody rb = bullet.GetComponent<Rigidbody>();

        Bullet bulletScript = bullet.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            bulletScript.SetDamage(weaponStat.damage);
            bulletScript.SetSource(playerController != null ? playerController.transform : null);
            bulletScript.SetDirection(shootDirection);
        }

        // Debug ray
        Debug.DrawRay(weaponStat.firePoint.position, shootDirection * 10f, Color.red, 2f);
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
        if (Input.GetKeyDown(KeyCode.R) && weaponStat.currentAmmo < weaponStat.magazineSize)
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
    }
    public void AddAmmo(int amount)
    {
        weaponStat.ammoReserve += amount;
        Debug.Log($"Ammo Reserve: {weaponStat.ammoReserve}");
    }
}
