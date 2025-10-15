using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    [Header("References")]
    private NavMeshAgent agent;
    [SerializeField] private Transform player;

    [Header("Detection Settings")]
    [SerializeField] private float detectionRadius = 12f;
    [SerializeField, Range(0, 180)] private float fieldOfView = 90f;
    [SerializeField] private LayerMask obstacleMask;

    private bool playerDetected = false;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null)
                player = p.transform;
        }
    }

    private void Update()
    {
        if (player == null) return;

        DetectPlayer();
        EnemyMovement();
    }
    private void EnemyMovement()
    {
        if (playerDetected)
            agent.SetDestination(player.position);
        else
            agent.ResetPath(); // หยุดถ้าไม่เห็นผู้เล่น
    }
    private void DetectPlayer()
    {
        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // ✅ ตรวจว่าผู้เล่นอยู่ในระยะ
        if (distanceToPlayer <= detectionRadius)
        {
            float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);

            // ✅ ตรวจว่าผู้เล่นอยู่ในมุมมอง
            if (angleToPlayer <= fieldOfView / 2f)
            {
                // ✅ ตรวจว่ามีสิ่งกีดขวางไหม
                if (!Physics.Raycast(transform.position + Vector3.up, directionToPlayer, distanceToPlayer, obstacleMask))
                {
                    playerDetected = true;
                    return;
                }
            }
        }

        playerDetected = false; // ถ้าไม่เข้าเงื่อนไขทั้งหมด
    }

    private void OnDrawGizmosSelected()
    {
        // Debug: แสดงระยะการมองเห็นใน Scene
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Debug: แสดงมุมมองสายตา
        Vector3 leftLimit = Quaternion.Euler(0, -fieldOfView / 2, 0) * transform.forward;
        Vector3 rightLimit = Quaternion.Euler(0, fieldOfView / 2, 0) * transform.forward;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + leftLimit * detectionRadius);
        Gizmos.DrawLine(transform.position, transform.position + rightLimit * detectionRadius);
    }
}
