using UnityEngine;

public class HitBoxManager : MonoBehaviour
{

    [Header("Enemy Settings")]
    public float health = 100f;

    public void TakeDamage(HitBoxPart hitPart)
    {
        EnemyController enemyController = GetComponent<EnemyController>();
        float damage = 10f;
        switch (hitPart)
        {
            case HitBoxPart.Head:
                damage = Random.Range(30f,45f); //ยิง 3-4 นัดตาย
                enemyController.isAlert = true;
                break;
            case HitBoxPart.Body:
                damage = Random.Range(20f, 25f); //ยิง 4-5 นัดตาย
                enemyController.isAlert = true;
                break;
            case HitBoxPart.Hip:
                damage = Random.Range(15f, 20f); //ยิง 5-7 นัดตาย
                enemyController.isAlert = true;
                break;
            case HitBoxPart.UpperArm:
                damage = Random.Range(15f, 20f); //ยิง 5-7 นัดตาย
                enemyController.isAlert = true;
                break;
            case HitBoxPart.LowerArm:
                damage = Random.Range(10f, 15f); //ยิง 5-7 นัดตาย
                enemyController.isAlert = true;
                break;
            case HitBoxPart.UpperLeg:
                damage = Random.Range(15f, 20f); //ยิง 5-7 นัดตาย
                enemyController.isAlert = true;
                break;
            case HitBoxPart.LowerLeg:
                damage = Random.Range(10f, 15f); //ยิง 7-10 นัดตาย
                enemyController.isAlert = true;
                break;
            case HitBoxPart.Foot:
                damage = Random.Range(10f, 15f); //ยิง 7-10 นัดตาย
                enemyController.isAlert = true;
                break;
            case HitBoxPart.Hand:
                damage = Random.Range(10f, 15f); //ยิง 7-10 นัดตาย
                enemyController.isAlert = true;
                break;

        }

        health -= damage;
        Debug.Log($"{hitPart} hit! -{damage} HP (Remaining: {health})");

        if (health <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("Enemy Died!");
        Destroy(this.gameObject);
    }
}


