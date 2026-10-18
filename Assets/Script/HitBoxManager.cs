using UnityEngine;

public class HitBoxManager : MonoBehaviour
{
    [Header("Enemy Settings")]
    public float health = 100f;
    private bool isDead;

    public void TakeDamage(HitBoxPart hitPart, float baseDamage = 0f, Transform source = null,
        Collider hitCollider = null, Vector3 hitPoint = default, Vector3 hitDirection = default, int shotId = 0, float impactPower = 0)
    {
        if (isDead || float.IsNaN(baseDamage) || float.IsInfinity(baseDamage) || baseDamage <= 0f) return;
        float multiplier = 0.5f;
        switch (hitPart)
        {
            case HitBoxPart.Head:
                multiplier = 1.75f;
                break;
            case HitBoxPart.Body:
                multiplier = 1f;
                break;
            case HitBoxPart.Hip:
            case HitBoxPart.UpperArm:
            case HitBoxPart.UpperLeg:
                multiplier = 0.75f;
                break;
        }
        float damage = baseDamage * multiplier;
        health = Mathf.Max(0f, health - damage);
        Debug.Log($"{hitPart} hit! -{damage} HP (Remaining: {health})");
        if (TryGetComponent(out EnemyController enemy)) enemy.AlertTo(source);
        if (health <= 0f)
        {
            isDead = true;
            if (enemy != null) enemy.BeginDeath();
            else Destroy(gameObject);
        }
        if (enemy != null) enemy.ReceiveBulletImpact(hitCollider, hitPoint, hitDirection, shotId, impactPower);
    }
}
