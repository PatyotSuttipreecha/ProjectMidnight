using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    [Header("References")]
    public EnemyDataSO enemyData;
    private NavMeshAgent agent;
    private Animator animator;
    [SerializeField] private Transform player;

    [Header("Patrol Settings")]
    [SerializeField] private Transform[] patrolPoints;
    private int lastPatrolIndex = -1;
    private float waitTimer = 0f;

    private bool playerDetected = false;
    public bool isAttacking = false;
    public bool isAlert = false;
    private float lostSightTimer = 0f;
    private float attackTimer = 0f;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null)
                player = p.transform;
        }

        if (patrolPoints.Length > 0)
        {
            int randomStart = Random.Range(0, patrolPoints.Length);
            agent.SetDestination(patrolPoints[randomStart].position);
            lastPatrolIndex = randomStart;
            animator.Play("zombie_walk2");
        }
    }

    private void Update()
    {
        if (player == null || enemyData == null) return;

        DetectPlayer();

        if (playerDetected)
        {
            ChasePlayer();
            lostSightTimer = 0f;
            isAlert = true;

            // ตรวจระยะเพื่อโจมตี
            TryAttack();
        }
        else
        {
            if (isAlert)
            {
                lostSightTimer += Time.deltaTime;
                if (lostSightTimer >= enemyData.lostSightCooldown)
                {
                    isAlert = false;
                    lostSightTimer = 0f;
                }
            }

            if (!isAlert)
            {
                Patrol();
            }
        }

        if (attackTimer > 0f)
            attackTimer -= Time.deltaTime;
    }

    private void Patrol()
    {
        if (patrolPoints.Length == 0) return;

        if (!agent.pathPending && agent.remainingDistance < 1f)
        {
            waitTimer += Time.deltaTime;
            animator.Play("zombie_idle1");
            if (waitTimer >= enemyData.waitTimeAtPoint)
            {
                int nextIndex;
                do
                {
                    nextIndex = Random.Range(0, patrolPoints.Length);
                    animator.Play("zombie_walk2");
                }
                while (nextIndex == lastPatrolIndex && patrolPoints.Length > 1);

                lastPatrolIndex = nextIndex;
                agent.SetDestination(patrolPoints[nextIndex].position);
                waitTimer = 0f;
            }
        }
    }

    private void ChasePlayer()
    {
        agent.SetDestination(player.position);
        animator.Play("zombie_walk2");
    }

    private void TryAttack()
    {
        if (isAttacking || attackTimer > 0f) return;

        float distance = Vector3.Distance(transform.position, player.position);
        if (distance <= enemyData.attackRange)
        {
            animator.Play("zombie_attack2_1");
            StartCoroutine(PerformAttack());
        }
    }

    private System.Collections.IEnumerator PerformAttack()
    {
        isAttacking = true;
        agent.isStopped = true;

        // โจมตี (ระหว่างแอนิเมชัน)
        yield return new WaitForSeconds(0.5f);
        float damage = Random.Range(enemyData.minAttack, enemyData.maxAttack);
        Debug.Log($"👹 Enemy attacks for {damage} damage!");
        player.GetComponent<PlayerController>().TakeDamage(damage);

        // 🕒 รอคูลดาวน์
        yield return new WaitForSeconds(enemyData.attackCooldown);

        agent.isStopped = false;
        isAttacking = false;
        attackTimer = enemyData.attackCooldown;
    }


    private void DetectPlayer()
    {
        Vector3 directionToPlayer = (player.position - transform.position).normalized;
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= enemyData.detectionRadius)
        {
            float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);
            if (angleToPlayer <= enemyData.fieldOfView / 2f)
            {
                if (!Physics.Raycast(transform.position + Vector3.up, directionToPlayer, distanceToPlayer, enemyData.obstacleMask))
                {
                    playerDetected = true;
                    return;
                }
            }
        }
        playerDetected = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (enemyData == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, enemyData.detectionRadius);

        Vector3 leftLimit = Quaternion.Euler(0, -enemyData.fieldOfView / 2, 0) * transform.forward;
        Vector3 rightLimit = Quaternion.Euler(0, enemyData.fieldOfView / 2, 0) * transform.forward;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + leftLimit * enemyData.detectionRadius);
        Gizmos.DrawLine(transform.position, transform.position + rightLimit * enemyData.detectionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, enemyData.attackRange);
    }
}
