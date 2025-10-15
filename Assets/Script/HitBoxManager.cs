using UnityEngine;

public class HitBoxManager : MonoBehaviour
{

    [Header("Enemy Settings")]
    public float health = 100f;

    public void TakeDamage(HitBoxPart hitPart)
    {
        float damage = 10f;

        switch (hitPart)
        {
            case HitBoxPart.Head:
                damage = Random.Range(30f,45f); //�ԧ 3-4 �Ѵ���
                break;
            case HitBoxPart.Body:
                damage = Random.Range(20f, 25f); //�ԧ 4-5 �Ѵ���
                break;
            case HitBoxPart.Hip:
                damage = Random.Range(15f, 20f); //�ԧ 5-7 �Ѵ���
                break;
            case HitBoxPart.UpperArm:
                damage = Random.Range(15f, 20f); //�ԧ 5-7 �Ѵ���
                break;
            case HitBoxPart.LowerArm:
                damage = Random.Range(10f, 15f); //�ԧ 5-7 �Ѵ���
                break;
            case HitBoxPart.UpperLeg:
                damage = Random.Range(15f, 20f); //�ԧ 5-7 �Ѵ���
                break;
            case HitBoxPart.LowerLeg:
                damage = Random.Range(10f, 15f); //�ԧ 7-10 �Ѵ���
                break;
            case HitBoxPart.Foot:
                damage = Random.Range(10f, 15f); //�ԧ 7-10 �Ѵ���
                break;
            case HitBoxPart.Hand:
                damage = Random.Range(10f, 15f); //�ԧ 7-10 �Ѵ���
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


