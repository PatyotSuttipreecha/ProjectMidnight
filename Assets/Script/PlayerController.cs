using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.Animations.Rigging;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    #region Variable
    public static PlayerController instance;

    [Header("Movement Settings")]
    public float moveSpeed = 2f;
    public float sprintBonus = 2f;
    public Rig rigBuilder;

    [Header("Cinemachine")]
    [Tooltip("Main character camera that used cinemachine")]
    [SerializeField] private CinemachineCamera cineCamera; // Cinemachine 3.x
    public CinemachineCamera AimCamera => cineCamera;

    [Header("Weapon Settings")]
    [Tooltip("0 = NoWeapon, 1 = Knife, 2 = Pistol, 3 = Shotgun")]
    public int weaponType = 0;
    [Tooltip("Time to raise or lower the aiming animation layer. Weapon selection remains immediate.")]
    [SerializeField, Min(0f)] private float aimBlendDuration = 0.2f;

    [Header("Weapon Switcher")]
    [Tooltip("0 = NoWeapon, 1 = Knife, 2 = Pistol, 3 = Shotgun")]
    [SerializeField] private GameObject[] weapons;
    private int currentweaponIndex = 4;
    public Guns EquippedGun => weapons != null && currentweaponIndex >= 0 && currentweaponIndex < weapons.Length
        && weapons[currentweaponIndex] != null && weapons[currentweaponIndex].activeInHierarchy
        ? weapons[currentweaponIndex].GetComponentInChildren<Guns>() : null;

    [Header("Footstep Sound")]
    public SoundSO footstepWalkSO;
    public SoundSO footstepRunSO;
    private float lastStepTime = 0f;

    [Header("Health Setting")]
    public float maxHealth = 100f;
    public float currentHealth;
    private bool isDeath;

    [Header("InventorySetting")]
    [Tooltip("Used inventory Laout grid in UI canvas")]
    [SerializeField] private GameObject inventoryLayout;

    private EnemyController enemy;
    private CharacterController controller;
    private Animator animator;
    private int aimingLayerIndex;
    private int GunHoldingLayerIndex;
    private RigBuilder animationRigBuilder;
    private RigLayer[] suspendedRigLayers;
    private bool[] savedRigLayerActive;
    private bool inventoryClosing;
    [HideInInspector]public bool isAiming;
    [HideInInspector]public bool isCheckInventory;

    #endregion

    private void Awake()
    {
        instance = this;
    }
    void Start()
    {
        controller = GetComponent<CharacterController>();

        if (cineCamera == null)
            cineCamera = FindFirstObjectByType<CinemachineCamera>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        animator = GetComponent<Animator>();
        animationRigBuilder = GetComponent<RigBuilder>();

        WeaponSwitcherIndex(currentweaponIndex);

        // หา Layer Index ของ Aiming
        aimingLayerIndex = animator.GetLayerIndex("Aiming");
        GunHoldingLayerIndex = animator.GetLayerIndex("GunHolding");

        currentHealth = maxHealth;
    }

    void Update()
    {
        CheckInventory();
        if(isCheckInventory)
        {
            isAiming = false;
            SuppressWeaponPose();
            animator.SetFloat("Speed", 0f);
            return;
        }
        isAiming = Input.GetMouseButton(1); // เล็งอยู่หรือไม่

        HandleMovement();
        HandleAiming();
        HandleCharacterRotation();
        WeaponSwitcher();
        
    }

    void HandleMovement()
    {
        if (cineCamera == null || isCheckInventory) return;

        Transform camTransform = cineCamera.transform;

        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        // เดินอิงจากมุมกล้อง
        Vector3 move = camTransform.right * moveX + camTransform.forward * moveZ;
        move.y = 0;

        float currentSpeed = moveSpeed;
        bool isMoving = moveX != 0 || moveZ != 0;

        
        bool isRunning = Input.GetKey(KeyCode.LeftShift) && isMoving && !isAiming;

        float speedPercent = 0f;

        if (isAiming && isMoving)
        {
            // เดินตอนเล็ง → ช้าลง
            currentSpeed *= 0.5f;
            speedPercent = 0.3f;   // Animator: Aiming เดิน
            
        }
        else if (isRunning)
        {
            currentSpeed += sprintBonus;
            speedPercent = 1f;     // Animator: Run
        }
        else if (isMoving)
        {
            speedPercent = 0.5f;   // Animator: Walk
        }


        if (Input.GetKey(KeyCode.S) && Input.GetMouseButton(1)) // ถอยหลังช้า
            currentSpeed -= 0.2f;

        controller.Move(move.normalized * currentSpeed * Time.deltaTime);

        // ส่งค่าพารามิเตอร์ไปที่ Animator
        animator.SetFloat("Horizontal", moveX);
        animator.SetFloat("Vertical", moveZ);
        animator.SetFloat("Speed", speedPercent, 0.3f, Time.deltaTime);
    }

    void HandleAiming()
    {
        if (animator == null) return;
        Guns gun = EquippedGun;
        bool aiming = Input.GetMouseButton(1) && (gun == null || !gun.isReloading) && !isCheckInventory;
        float aimType = weaponType == 3 ? 1f : weaponType == 2 ? 0.6f : weaponType == 1 ? 0.4f : 0.2f;
        float holdingType = weaponType == 2 ? 0.6f : 0.4f;
        // Select the weapon immediately and keep the selected pose while its layer fades out.
        animator.SetFloat("AimType", aimType);
        animator.SetFloat("GunHolding", holdingType);
        if (GunHoldingLayerIndex >= 0) animator.SetLayerWeight(GunHoldingLayerIndex, weaponType == 0 ? 0f : 1f);
        float targetWeight = aiming ? 1f : 0f;
        float currentWeight = aimingLayerIndex >= 0 ? animator.GetLayerWeight(aimingLayerIndex) : 0f;
        float nextWeight = aimBlendDuration > 0f ? Mathf.MoveTowards(currentWeight, targetWeight, Time.deltaTime / aimBlendDuration) : targetWeight;
        if (aimingLayerIndex >= 0) animator.SetLayerWeight(aimingLayerIndex, nextWeight);
        if (rigBuilder != null) rigBuilder.weight = weaponType == 0 ? 0f : nextWeight;
        if (cineCamera == null) return;
        cineCamera.Lens.FieldOfView = Mathf.Lerp(cineCamera.Lens.FieldOfView, aiming ? 30f : 60f, Time.deltaTime * 5f);
        CinemachineCameraOffset cameraOffset = FindFirstObjectByType<CinemachineCameraOffset>();
        if (cameraOffset != null)
        {
            cameraOffset.Offset.y = aiming ? 1.7f : 1.2f;
            cameraOffset.Offset.x = aiming ? 0.4f : 0.6f;
        }
    }

    void HandleCharacterRotation()
    {
        if (cineCamera == null) return;

        Transform camTransform = cineCamera.transform;

        if (Input.GetMouseButton(1)) // Aim Mode
        {
            Vector3 cameraForward = camTransform.forward;
            cameraForward.y = 0;
            if (cameraForward.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(cameraForward);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
            }
        }
        else
        {
            float moveX = Input.GetAxis("Horizontal");
            float moveZ = Input.GetAxis("Vertical");

            Vector3 moveDir = new Vector3(moveX, 0, moveZ);
            if (moveDir.sqrMagnitude > 0.01f)
            {
                moveDir = camTransform.right * moveX + camTransform.forward * moveZ;
                moveDir.y = 0;

                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
            }
        }
    }
    void WeaponSwitcher()
    {
        Guns weapon = FindAnyObjectByType<Guns>();
        //Check Input 1-4 
        if (Input.GetKeyDown(KeyCode.Alpha1)) 
        { 
            WeaponSwitcherIndex(2);
            weaponType = 2;
            if (weapon != null)
            {
                weapon.crosshairUI.gameObject.SetActive(true);
            }

        }
        if (Input.GetKeyDown(KeyCode.Alpha2)) 
        { 
            WeaponSwitcherIndex(3); 
            weaponType = 3;
            if (weapon != null)
            {
                weapon.crosshairUI.gameObject.SetActive(true);
            }
        }
        if (Input.GetKeyDown(KeyCode.Alpha3)) 
        { 
            WeaponSwitcherIndex(1);
            weaponType=1;
            if (weapon != null)
            {
                weapon.crosshairUI.gameObject.SetActive(true);
            }
        }
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            WeaponSwitcherIndex(0);
            weaponType = 0;
            if (weapon != null)
            {
                weapon.crosshairUI.gameObject.SetActive(false);
            }
        }
       
    }
    void WeaponSwitcherIndex(int index)
    {
        //Check Error
        if (index < 0 || index >= weapons.Length)
        {
            return;
        }
        //set hide weapon
        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] != null)
            {
                weapons[i].SetActive(false);
            }
        }
        //check index weapon check
        if (weapons[index] != null)
        {
            currentweaponIndex = index;
            weapons[index].SetActive(true);
            
        }
        else
        {
            return ;
        }
    }
    public void Footstep()
    {
        if (Time.time - lastStepTime < 0.3f) return; // กันเสียงซ้อนจาก Layer/Frame
        lastStepTime = Time.time;

        float speed = animator.GetFloat("Speed");
        if (speed >= 0.9f)
            SoundManager.PlaySound(footstepRunSO, Random.Range(0.03f,0.05f));
        else
            SoundManager.PlaySound(footstepWalkSO, Random.Range(0.03f, 0.06f));
    }
    public void TakeDamage(float damage)
    {   
        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);
    }
    private void Die()
    {
        if (currentHealth <= 0)
        {
            Destroy(this.gameObject);
        }
    }

    private void CheckInventory()
    {
        float inventoryFOV = 30f;
        float normalFOV = 60f;
        Vector3 inventoryOffset = new Vector3(0.4f, 0.5f, 0f);
        Vector3 normalOffset = new Vector3(0.6f, 1.2f, 1.2f);
        float transitionDuration = 0.5f; // ครึ่งวินาที

        CinemachineCameraOffset cameraOffset = FindFirstObjectByType<CinemachineCameraOffset>();

        if (inventoryClosing) return;

        if (Input.GetKeyDown(KeyCode.I) && !isCheckInventory)
        {
            if(inventoryLayout == null)
            {
                inventoryLayout = GameObject.Find("InventoryLayout");
            }

            if (inventoryLayout != null)
            {
                inventoryLayout.SetActive(true);
                isCheckInventory = true;
                SuspendInventoryRigs();
                SuppressWeaponPose();
                animator.Play("SeatInventory", 0, 0f);

                if (cineCamera != null && cameraOffset != null) StartCoroutine(SmoothCameraTransition(inventoryFOV, inventoryOffset, transitionDuration));

                // 🖱️ ปลดล็อกเมาส์เพื่อให้จัดของในช่องได้
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

        }
        else if(Input.GetKeyDown(KeyCode.I) && isCheckInventory)
        {
            inventoryClosing = true;
            animator.Play("UnseatInventory", 0, 0f);
            StartCoroutine(FinishInventoryExit());

            inventoryLayout.SetActive(false);
            if (cineCamera != null && cameraOffset != null) StartCoroutine(SmoothCameraTransition(normalFOV, normalOffset, transitionDuration));

            // 🔒 ล็อกเมาส์กลับ
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

    }

    private void SuppressWeaponPose()
    {
        if (aimingLayerIndex >= 0) animator.SetLayerWeight(aimingLayerIndex, 0f);
        if (GunHoldingLayerIndex >= 0) animator.SetLayerWeight(GunHoldingLayerIndex, 0f);
        if (rigBuilder != null) rigBuilder.weight = 0f;
    }

    private void SuspendInventoryRigs()
    {
        if (animationRigBuilder == null) return;
        suspendedRigLayers = animationRigBuilder.layers.ToArray();
        savedRigLayerActive = new bool[suspendedRigLayers.Length];
        for (int i = 0; i < suspendedRigLayers.Length; i++)
        {
            savedRigLayerActive[i] = suspendedRigLayers[i].active;
            // Gate the rig layer: animation curves can overwrite Rig.weight.
            suspendedRigLayers[i].active = false;
        }
        animationRigBuilder.SyncLayers();
    }

    private IEnumerator FinishInventoryExit()
    {
        // Animator.Play is evaluated after Update; inspect the state on the next frame.
        yield return null;
        while (animator != null)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            bool inInventoryState = state.IsName("UnseatInventory") || state.IsName("SeatInventory")
                || state.IsName("SeatInventoryIdle");
            if (!inInventoryState && !animator.IsInTransition(0)) break;
            yield return null;
        }
        if (suspendedRigLayers != null)
        {
            for (int i = 0; i < suspendedRigLayers.Length; i++)
                suspendedRigLayers[i].active = savedRigLayerActive[i];
            suspendedRigLayers = null;
            savedRigLayerActive = null;
            if (animationRigBuilder != null) animationRigBuilder.SyncLayers();
        }
        isCheckInventory = false;
        inventoryClosing = false;
    }

   private IEnumerator SmoothCameraTransition(float targetFOV, Vector3 targetOffset, float duration)
    {
        CinemachineCameraOffset cameraOffset = FindFirstObjectByType<CinemachineCameraOffset>();
        float startFOV = cineCamera.Lens.FieldOfView;
        Vector3 startOffset = cameraOffset.Offset;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;

            cineCamera.Lens.FieldOfView = Mathf.Lerp(startFOV, targetFOV, t);
            cameraOffset.Offset = Vector3.Lerp(startOffset, targetOffset, t);

            yield return null;
        }

        cineCamera.Lens.FieldOfView = targetFOV;
        cameraOffset.Offset = targetOffset;
    }

}

