using UnityEngine;

[DisallowMultipleComponent]
public class EnemyWeaponRuntime : MonoBehaviour
{
    [Tooltip("Optional explicit attachment. Empty finds Enemy_GripPoint in this enemy, then uses the Humanoid right hand or enemy root. Module offsets are relative to the attachment.")]
    public Transform weaponAnchor;
    public EnemyWeaponSO Current { get; private set; }
    private GameObject heldVisual;
    private float nextDropRoll;
    public void Equip(EnemyWeaponSO module)
    {
        if (heldVisual != null) Destroy(heldVisual);
        heldVisual = null;
        Current = module;
        nextDropRoll = 0;
        if (module == null || module.heldModel == null) return;
        Transform anchor = weaponAnchor;
        if (anchor == null)
            foreach (Transform candidate in GetComponentsInChildren<Transform>(true))
                if (candidate.name == "Enemy_GripPoint" && candidate.GetComponentInParent<EnemyWeaponRuntime>() == this)
                { anchor = candidate; break; }
        var animator = GetComponent<Animator>();
        if (anchor == null && animator != null && animator.isHuman) anchor = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (anchor == null) anchor = transform;
        heldVisual = Instantiate(module.heldModel, anchor);
        heldVisual.transform.localPosition = module.localPosition;
        heldVisual.transform.localEulerAngles = module.localEulerAngles;
        heldVisual.transform.localScale = module.localScale;
        foreach (Collider collider in heldVisual.GetComponentsInChildren<Collider>()) collider.enabled = false;
        foreach (Rigidbody body in heldVisual.GetComponentsInChildren<Rigidbody>()) body.isKinematic = true;
    }
    public bool TryDisarm(Collider struck)
    {
        if (Current == null || struck == null || Time.time < nextDropRoll) return false;
        nextDropRoll = Time.time + Mathf.Max(0, Current.disarmRollInterval);
        var box = struck.GetComponent<HitBox>();
        bool arm = box != null && (box.hitBox == HitBoxPart.Hand || box.hitBox == HitBoxPart.LowerArm || box.hitBox == HitBoxPart.UpperArm);
        float chance = arm ? Current.armDropChance : Current.otherDropChance;
        if (chance <= 0 || Random.value >= Mathf.Clamp01(chance)) return false;
        Drop(); return true;
    }
    public void Drop()
    {
        var module = Current;
        Current = null;
        if (heldVisual == null) return;
        heldVisual.transform.SetParent(null, true);
        // Drop is a visual prop, not a pickup. Keep its colliders disabled so it cannot block enemies.
        if (module != null && module.droppedLifetime > 0) Destroy(heldVisual, module.droppedLifetime);
        var fall = heldVisual.AddComponent<EnemyDroppedWeapon>();
        fall.obstacles = GetComponent<EnemyController>()?.enemyData?.obstacleMask ?? (LayerMask)~0;
        fall.owner = transform;
        heldVisual = null;
    }
    public void Consume()
    {
        Current = null;
        if (heldVisual != null) Destroy(heldVisual);
        heldVisual = null;
    }
}

// Visual-only falling prop. Sweep against the configured world mask and settle; no pickup logic.
public class EnemyDroppedWeapon : MonoBehaviour
{
    public LayerMask obstacles;
    public Transform owner;
    private float speed, elapsed;
    private void Update()
    {
        elapsed += Time.deltaTime;
        speed += 9.81f * Time.deltaTime;
        float distance = speed * Time.deltaTime;
        RaycastHit? nearest = null;
        foreach (var hit in Physics.RaycastAll(transform.position, Vector3.down, distance + .04f, obstacles, QueryTriggerInteraction.Ignore))
            if (!hit.collider.transform.IsChildOf(transform) && (owner == null || !hit.collider.transform.IsChildOf(owner)) &&
                (nearest == null || hit.distance < nearest.Value.distance)) nearest = hit;
        if (nearest.HasValue)
        { transform.position = nearest.Value.point + Vector3.up * .04f; enabled = false; }
        else transform.position += Vector3.down * distance;
        if (elapsed > 3f) enabled = false;
    }
}
