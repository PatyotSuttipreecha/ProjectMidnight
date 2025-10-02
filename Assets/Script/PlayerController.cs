using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.Animations.Rigging;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 2f;
    public float sprintBonus = 2f;

    [Header("Cinemachine")]
    [SerializeField] private CinemachineCamera cineCamera; // Cinemachine 3.x

    [Header("Weapon Settings")]
    [Tooltip("0 = NoWeapon, 1 = Knife, 2 = Pistol, 3 = Shotgun")]
    public int weaponType = 0;

    [Header("Weapon Switcher")]
    [Tooltip("0 = NoWeapon, 1 = Knife, 2 = Pistol, 3 = Shotgun")]
    [SerializeField] private GameObject[] weapons;
    private int currentweaponIndex = 4;

    [Header("Footstep Sound")]
    public SoundSO footstepWalkSO;
    public SoundSO footstepRunSO;
    private float lastStepTime = 0f;

    private CharacterController controller;
    private Animator animator;
    private int aimingLayerIndex;
    private int GunHoldingLayerIndex;
    public bool isAiming;
    public Rig rigBuilder;
    void Start()
    {
        controller = GetComponent<CharacterController>();

        if (cineCamera == null)
            cineCamera = FindFirstObjectByType<CinemachineCamera>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        animator = GetComponent<Animator>();

        WeaponSwitcherIndex(currentweaponIndex);

        // หา Layer Index ของ Aiming
        aimingLayerIndex = animator.GetLayerIndex("Aiming");
        GunHoldingLayerIndex = animator.GetLayerIndex("GunHolding");
    }

    void Update()
    {
        isAiming = Input.GetMouseButton(1); // เล็งอยู่หรือไม่

        HandleMovement();
        HandleAiming();
        HandleCharacterRotation();
        WeaponSwitcher();
        
    }

    void HandleMovement()
    {
        if (cineCamera == null) return;

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
        if (cineCamera == null) return;

        float normalFOV = 60f;
        float targetFOV = 30f;
        float zoomSpeed = 5f;
        
        CinemachineCameraOffset cameraOffset = FindFirstObjectByType<CinemachineCameraOffset>();

        bool isAiming = Input.GetMouseButton(1);

        //float target = isAiming ? targetFOV : normalFOV;
       

        // เปิด/ปิด Aiming Layer
        animator.SetLayerWeight(aimingLayerIndex, isAiming ? 1f : 0f);

        // กำหนดประเภทอาวุธไปยัง Blend Tree
        Guns gun = FindAnyObjectByType<Guns>();
        if (isAiming && !gun.isReloading)
        {
            cineCamera.Lens.FieldOfView = Mathf.Lerp(cineCamera.Lens.FieldOfView, targetFOV, Time.deltaTime * zoomSpeed);
            cameraOffset.Offset.y = 1.5f;
            cameraOffset.Offset.x = 0.4f;
            switch (weaponType)
            {
                case 0: animator.SetFloat("AimType", 0.2f, 0.3f, Time.deltaTime);
                    rigBuilder.weight = 0f;                   
                    break;     // NoWeapon
                case 1: animator.SetFloat("AimType", 0.4f, 0.3f, Time.deltaTime); 
                    rigBuilder.weight += 3f * Time.deltaTime;                  
                    break;   // Knife
                case 2: animator.SetFloat("AimType", 0.6f, 0.3f, Time.deltaTime); 
                    rigBuilder.weight += 3f * Time.deltaTime;
                    break;   // Pistol
                case 3: animator.SetFloat("AimType", 1f, 0.3f, Time.deltaTime); 
                    rigBuilder.weight += 3f * Time.deltaTime;
                    break;     // Shotgun
            }
            
        }
        else
        {
            isAiming = false;
            cineCamera.Lens.FieldOfView = Mathf.Lerp(cineCamera.Lens.FieldOfView, normalFOV, Time.deltaTime * zoomSpeed);
            animator.SetFloat("AimType", 0f, 0.3f, Time.deltaTime);
            switch (weaponType)
            {
                case 0:
                    animator.SetLayerWeight(GunHoldingLayerIndex, 0f);
                    break;
                case 1:
                    animator.SetLayerWeight(GunHoldingLayerIndex, 1f);
                    animator.SetFloat("GunHolding", 0.4f, 0.3f, Time.deltaTime);
                    break;
                case 2:
                    animator.SetLayerWeight(GunHoldingLayerIndex, 1f);
                    animator.SetFloat("GunHolding", 0.6f, 0.3f, Time.deltaTime);
                    break;
                case 3:
                    animator.SetLayerWeight(GunHoldingLayerIndex, 1f);
                    animator.SetFloat("GunHolding", 0.4f, 0.3f, Time.deltaTime);
                    break;

            }
            rigBuilder.weight -= 3f * Time.deltaTime;
            cameraOffset.Offset.y = 1.2f ;
            cameraOffset.Offset.x = 0.6f;
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
        //Check Input 1-4 
        if (Input.GetKeyDown(KeyCode.Alpha1)) 
        { 
            WeaponSwitcherIndex(2);
            weaponType = 2;

        }
        if (Input.GetKeyDown(KeyCode.Alpha2)) 
        { 
            WeaponSwitcherIndex(3); 
            weaponType = 3;
        }
        if (Input.GetKeyDown(KeyCode.Alpha3)) 
        { 
            WeaponSwitcherIndex(1);
            weaponType=1;
        }
        if (Input.GetKeyDown(KeyCode.Alpha4)) 
        { 
            WeaponSwitcherIndex(0); 
            weaponType=0;
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
            SoundManager.PlaySound(footstepRunSO, 0.1f);
        else
            SoundManager.PlaySound(footstepWalkSO, 0.07f);
    }
}

