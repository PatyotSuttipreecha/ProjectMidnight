using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

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

    private void Update()
    {
        if (!isReloading)
        {
            HandleAimingSpread();
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
        if (playerController.isAiming)
        {
            // เพิ่มเวลาที่เล็ง
            aimTimer += Time.deltaTime;

            // คำนวณ progress ของการหด spread (0 → 1)
            float t = Mathf.Clamp01(aimTimer / weaponStat.aimTime);

            // ค่อยๆ ลด spread จาก baseSpread → minSpread
            currentSpread = Mathf.Lerp(weaponStat.baseSpread, weaponStat.minSpread, t);
            if(Input.GetMouseButtonDown(1))
            {
                SoundManager.PlaySound(weaponStat.aimingSO, Random.Range(0.1f,0.3f));
            }

        }
        else
        {
            // ถ้าไม่ได้เล็ง ให้รีเซ็ตกลับ
            aimTimer = 0f;
            currentSpread = weaponStat.baseSpread;
        }
        if (crosshairUI != null)
            crosshairUI.SetSpread(currentSpread / weaponStat.baseSpread); // normalize
    }


    void Shooting()
    {
        if (playerController.isAiming && weaponStat.currentAmmo > 0)
        {
            if (Input.GetMouseButtonDown(0))
            {
                weaponStat.currentAmmo--;
                weaponStat.muzzleFlash.Play();
               
                FireBullet();

                // ยิงแล้วรีเซ็ต spread
                aimTimer = 0f;
                currentSpread = weaponStat.baseSpread;

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

        // 1. ยิง Ray ออกไปจากกลางจอกล้อง (Crosshair)
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        Vector3 targetPoint;

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
        {
            targetPoint = hit.point; // ถ้ามี object
        }
        else
        {
            targetPoint = ray.GetPoint(1000f); // ถ้าไม่ชน → ยิงไปไกลๆ
        }

        // 2. คำนวณทิศจาก firePoint → targetPoint
        Vector3 shootDirection = (targetPoint - weaponStat.firePoint.position).normalized;

        // 3. ใส่ Spread เพิ่มเข้าไป (เพื่อให้ไม่ตรง 100% เวลาไม่เล็ง)
        float spreadX = Random.Range(-currentSpread, currentSpread);
        float spreadY = Random.Range(-currentSpread, currentSpread);
        shootDirection = Quaternion.Euler(spreadY, spreadX, 0) * shootDirection;

        // 4. สร้างกระสุน
        GameObject bullet = Instantiate(weaponStat.bulletPrefab, weaponStat.firePoint.position, Quaternion.LookRotation(shootDirection));

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = shootDirection * weaponStat.bulletSpeed;
        }

        Bullet bulletScript = bullet.GetComponent<Bullet>();
        if (bulletScript != null)
        {
            bulletScript.SetDamage(weaponStat.damage);
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
}
