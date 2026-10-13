using UnityEngine;

// Optional attack module. Losing this module returns to EnemyDataSO's base attack.
[CreateAssetMenu(fileName = "EnemyWeapon", menuName = "System Modification/Enemy Weapon Module")]
public class EnemyWeaponSO : ScriptableObject
{
    public enum Loadout { Machete, Log, HeavyAxe, Stone, Torch }
    public Loadout loadout;
    [Tooltip("Visual-only prefab. Attach position is local to the right hand or assigned weapon anchor.")]
    public GameObject heldModel;
    public Vector3 localPosition, localEulerAngles;
    public Vector3 localScale = Vector3.one;
    [Tooltip("Optional layer-0 Animator state. Empty or missing uses the enemy's existing attack state. Machete, Log and Torch can share one sweep state.")]
    public string attackAnimation;
    [Tooltip("Maximum attack distance. Stone uses this as its throwing range.")]
    [Min(.1f)] public float range = 2.8f;
    [Tooltip("Melee hit cone around the facing locked at windup start. Does not affect Stone.")]
    [Range(1, 180)] public float hitArc = 110f;
    [Min(0)] public float minDamage = 12, maxDamage = 18;
    [Tooltip("Stopped anticipation before playing the attack animation.")]
    [Min(0)] public float preparation = .45f;
    [Tooltip("Delay from attack animation start to the damage check or stone release.")]
    [Min(0)] public float windup = .5f;
    [Tooltip("Stopped recovery after the hit attempt. Measured independently of preparation and windup.")]
    [Min(.05f)] public float recovery = .8f;
    [Header("Disarm")]
    [Tooltip("Chance per bullet impact on Hand, LowerArm or UpperArm HitBoxes. 0 disables disarming on arms.")]
    [Range(0, 1)] public float armDropChance = .35f;
    [Tooltip("Chance per bullet impact on other parts or an unclassified collider.")]
    [Range(0, 1)] public float otherDropChance = .05f;
    [Tooltip("Minimum interval between disarm rolls. Prevents one shotgun volley from rolling once per pellet.")]
    [Min(0)] public float disarmRollInterval = .2f;
    [Tooltip("Visual dropped weapon lifetime. No enemy or player pickup is added by this module. Zero keeps it.")]
    [Min(0)] public float droppedLifetime = 12f;
    [Header("Stone Projectile")]
    [Tooltip("Optional visual-only projectile prefab. Empty uses a small sphere prototype.")]
    public GameObject projectileModel;
    [Min(.1f)] public float projectileSpeed = 14f;
    [Min(0)] public float projectileGravity = 9.81f;
    [Min(.01f)] public float projectileRadius = .12f;
    [Min(.1f)] public float projectileLifetime = 5f;
    [Tooltip("World obstruction layers. Player colliders are included independently.")]
    public LayerMask projectileObstacles = ~0;
    [Header("Torch Burn")]
    [Tooltip("One burn instance per player. Repeated hits refresh expiry instead of stacking instances.")]
    [Min(0)] public float burnDuration = 3f;
    [Min(0)] public float burnDamagePerSecond = 2f;
    [Tooltip("Optional visual-only burning effect prefab placed on the player. Does not lock movement.")]
    public GameObject burnEffect;
}
