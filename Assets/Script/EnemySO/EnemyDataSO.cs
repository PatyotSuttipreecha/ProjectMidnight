using UnityEngine;

[CreateAssetMenu(fileName = "EnemyDataSO", menuName = "Scriptable Objects/EnemyDataSO")]
public class EnemyDataSO : ScriptableObject
{
   [Header("Detection Settings")]
    [Range(1f, 50f)] public float detectionRadius = 12f;
    [Range(0f, 180f)] public float fieldOfView = 90f;
    public LayerMask obstacleMask;

    [Header("Patrol Settings")]
    [Range(0.5f, 10f)] public float waitTimeAtPoint = 2f;
    [Range(1f, 10f)] public float lostSightCooldown = 5f;

    [Header("Attack Settings")]
    [Range(1f, 100f)] public float minAttack = 1f;
    [Range(1f, 100f)] public float maxAttack = 10f;
    [Range(1f, 5f)] public float attackRange = 2f;
    [Range(0.5f, 5f)] public float attackCooldown = 2f;
}
